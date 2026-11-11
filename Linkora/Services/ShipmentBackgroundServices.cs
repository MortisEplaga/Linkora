using Linkora.Models;
using Linkora.Repositories;
using System.Text.Json;

namespace Linkora.Services
{

    public abstract class PeriodicShipmentJob(IServiceScopeFactory scopeFactory, ILogger logger, TimeSpan interval, TimeSpan initialDelay) : BackgroundService
    {
        protected readonly ILogger Logger = logger;
        protected abstract string JobName { get; }
        protected abstract Task RunOnceAsync(IServiceProvider services, CancellationToken ct);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logger.LogInformation("{Job} started", JobName);
            try { await Task.Delay(initialDelay, stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    await RunOnceAsync(scope.ServiceProvider, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { Logger.LogError(ex, "{Job} run failed", JobName); }

                try { await Task.Delay(interval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        protected static async Task NotifyAsync(INotificationService notifications, ILogger logger, int userId, int? fromUserId, int productId, string type, int requestId)
        {
            try
            {
                await notifications.CreateAsync(userId, fromUserId, productId, JsonSerializer.Serialize(new { type, requestId }));
            }
            catch (Exception ex) { logger.LogError(ex, "Could not notify user {UserId} about {Type} for request {RequestId}", userId, type, requestId); }
        }
    }

    public sealed class ShipmentRegistrationRetryService(IServiceScopeFactory scopeFactory, ILogger<ShipmentRegistrationRetryService> logger)
        : PeriodicShipmentJob(scopeFactory, logger, TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(30))
    {
        protected override string JobName => nameof(ShipmentRegistrationRetryService);

        protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
        {
            var repo = services.GetRequiredService<IShipmentRepository>();
            var shipping = services.GetRequiredService<IShippingService>();

            var pending = await repo.GetPendingShipmentsAsync(2);
            foreach (var shipment in pending)
            {
                ct.ThrowIfCancellationRequested();
                try { await shipping.TryRegisterAsync(shipment.Id, ct); }
                catch (Exception ex) when (ex is not OperationCanceledException) { Logger.LogError(ex, "Retry of shipment {ShipmentId} failed", shipment.Id); }
            }
        }
    }

    public sealed class ShipmentTrackingSyncService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<ShipmentTrackingSyncService> logger)
        : PeriodicShipmentJob(scopeFactory, logger, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(45))
    {
        private const string Provider = "Omniva";
        private const int PageSize = 100;
        private const int MaxPagesPerRun = 4;
        private readonly IReadOnlyDictionary<string, ShipmentStatus> _statusMap = LoadStatusMap(configuration, logger);

        protected override string JobName => nameof(ShipmentTrackingSyncService);

        public static IReadOnlyDictionary<string, ShipmentStatus> LoadStatusMap(IConfiguration configuration, ILogger logger)
        {
            var map = new Dictionary<string, ShipmentStatus>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in OmnivaEventCodes.DefaultStatusMap) map[pair.Key] = pair.Value;

            foreach (var child in configuration.GetSection("Omniva:EventStatusMap").GetChildren())
            {
                if (Enum.TryParse<ShipmentStatus>(child.Value, true, out var status) && status is not ShipmentStatus.Pending)
                    map[child.Key] = status;
                else
                    logger.LogWarning("Omniva:EventStatusMap:{Code} has an unusable status '{Value}' and is ignored", child.Key, child.Value);
            }
            return map;
        }

        protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
        {
            var repo = services.GetRequiredService<IShipmentRepository>();
            var omniva = services.GetRequiredService<IOmnivaService>();
            var notifications = services.GetRequiredService<INotificationService>();

            var cursor = await repo.GetLastEventIdAsync(Provider);
            for (var page = 0; page < MaxPagesPerRun; page++)
            {
                ct.ThrowIfCancellationRequested();

                OmnivaEventsPage result;
                try { result = await omniva.GetEventsAsync(cursor, PageSize, ct); }
                catch (OmnivaApiException ex) when (ex.IsRateLimited)
                {
                    Logger.LogWarning("Omniva rate limit reached; event sync continues on the next run from event {Cursor}", cursor);
                    return;
                }

                if (result.Events.Count > 0)
                    await ProcessPageAsync(repo, notifications, result.Events);

                if (result.LastEventId > cursor)
                {
                    await repo.SetLastEventIdAsync(Provider, result.LastEventId);
                    cursor = result.LastEventId;
                }
                else if (result.HasMore)
                {

                    Logger.LogWarning("Omniva events page at cursor {Cursor} did not advance the cursor; sync stops until the response format is fixed", cursor);
                    return;
                }

                if (!result.HasMore) break;
            }
        }

        private async Task ProcessPageAsync(IShipmentRepository repo, INotificationService notifications, IReadOnlyList<OmnivaEvent> events)
        {
            var shipmentsByBarcode = new Dictionary<string, Shipment?>(StringComparer.OrdinalIgnoreCase);
            var matched = new List<(Shipment Shipment, OmnivaEvent Source)>();
            foreach (var e in events)
            {
                if (!shipmentsByBarcode.TryGetValue(e.Barcode, out var shipment))
                {
                    shipment = await repo.GetShipmentByBarcodeAsync(e.Barcode);
                    shipmentsByBarcode[e.Barcode] = shipment;
                }

                if (shipment != null) matched.Add((shipment, e));
            }
            if (matched.Count == 0) return;

            await repo.SaveEventsAsync(matched.Select(m => new ShipmentEvent
            {
                ShipmentId = m.Shipment.Id,
                ExternalEventId = m.Source.EventId,
                EventCode = m.Source.Code,
                EventAt = m.Source.OccurredAtUtc,
                Location = m.Source.Location,
                RawJson = m.Source.RawJson
            }));

            foreach (var (shipment, source) in matched.OrderBy(m => m.Source.OccurredAtUtc).ThenBy(m => m.Source.EventId))
            {
                var status = _statusMap.TryGetValue(source.Code, out var mapped) ? (ShipmentStatus?)mapped : null;
                var applied = await repo.ApplyEventAsync(shipment.Id, status, source.Code, source.OccurredAtUtc);
                if (!applied || status is not (ShipmentStatus.ArrivedAtLocker or ShipmentStatus.Delivered or ShipmentStatus.Returned or ShipmentStatus.Failed)) continue;

                var request = await repo.GetRequestAsync(shipment.RequestId);
                if (request == null) continue;

                switch (status)
                {
                    case ShipmentStatus.ArrivedAtLocker:
                        await NotifyAsync(notifications, Logger, request.BuyerId, request.SellerId, request.ProductId, "shipment_arrived", request.Id);
                        break;
                    case ShipmentStatus.Delivered:
                        await NotifyAsync(notifications, Logger, request.BuyerId, request.SellerId, request.ProductId, "shipment_delivered", request.Id);
                        await NotifyAsync(notifications, Logger, request.SellerId, request.BuyerId, request.ProductId, "shipment_delivered", request.Id);
                        break;
                    case ShipmentStatus.Returned:
                        await NotifyAsync(notifications, Logger, request.SellerId, request.BuyerId, request.ProductId, "shipment_returned", request.Id);
                        await NotifyAsync(notifications, Logger, request.BuyerId, request.SellerId, request.ProductId, "shipment_returned", request.Id);
                        break;

                    case ShipmentStatus.Failed:
                        await NotifyAsync(notifications, Logger, request.BuyerId, request.SellerId, request.ProductId, "shipment_cancelled", request.Id);
                        await NotifyAsync(notifications, Logger, request.SellerId, request.BuyerId, request.ProductId, "shipment_cancelled", request.Id);
                        break;
                }
            }
        }
    }

    public sealed class ShipmentAcceptanceExpiryService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<ShipmentAcceptanceExpiryService> logger)
        : PeriodicShipmentJob(scopeFactory, logger, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(1))
    {
        private readonly int _acceptPaymentHours = configuration.GetValue("Shipping:AcceptPaymentHours", 24);

        protected override string JobName => nameof(ShipmentAcceptanceExpiryService);

        protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
        {
            var repo = services.GetRequiredService<IShipmentRepository>();
            var notifications = services.GetRequiredService<INotificationService>();

            var released = await repo.ResetStaleAcceptancesAsync(DateTime.UtcNow.AddHours(-_acceptPaymentHours));
            if (released.Count > 0) Logger.LogInformation("{Count} accepted shipment request(s) were not paid within {Hours} h and returned to Requested", released.Count, _acceptPaymentHours);

            foreach (var r in released)
            {
                ct.ThrowIfCancellationRequested();
                await NotifyAsync(notifications, Logger, r.SellerId, r.BuyerId, r.ProductId, "shipment_accept_expired", r.RequestId);
                await NotifyAsync(notifications, Logger, r.BuyerId, r.SellerId, r.ProductId, "shipment_accept_expired", r.RequestId);
            }
        }
    }

    public sealed class ShipmentHandoverExpiryService(IServiceScopeFactory scopeFactory, ILogger<ShipmentHandoverExpiryService> logger)
        : PeriodicShipmentJob(scopeFactory, logger, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(1))
    {
        protected override string JobName => nameof(ShipmentHandoverExpiryService);

        protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
        {
            var repo = services.GetRequiredService<IShipmentRepository>();
            var omniva = services.GetRequiredService<IOmnivaService>();
            var notifications = services.GetRequiredService<INotificationService>();

            var expired = await repo.GetExpiredHandoverShipmentsAsync();
            foreach (var shipment in expired)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (!await repo.MarkHandoverExpiredAsync(shipment.Id)) continue;

                    var cancelled = shipment.Barcode != null && await omniva.CancelShipmentAsync(shipment.Barcode, ct);
                    if (!cancelled)
                        Logger.LogError("Shipment {ShipmentId} (barcode {Barcode}) expired, but Omniva did not confirm the cancellation; check it manually", shipment.Id, shipment.Barcode);

                    var request = await repo.GetRequestAsync(shipment.RequestId);
                    if (request == null) continue;
                    await NotifyAsync(notifications, Logger, request.SellerId, request.BuyerId, request.ProductId, "shipment_expired", request.Id);
                    await NotifyAsync(notifications, Logger, request.BuyerId, request.SellerId, request.ProductId, "shipment_expired", request.Id);
                }
                catch (Exception ex) when (ex is not OperationCanceledException) { Logger.LogError(ex, "Handover expiry of shipment {ShipmentId} failed", shipment.Id); }
            }
        }
    }

    public sealed class ParcelLockerSyncService(IServiceScopeFactory scopeFactory, ILogger<ParcelLockerSyncService> logger)
        : PeriodicShipmentJob(scopeFactory, logger, TimeSpan.FromHours(6), TimeSpan.FromSeconds(20))
    {
        private const string Provider = "Omniva";

        protected override string JobName => nameof(ParcelLockerSyncService);

        protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
        {
            var omniva = services.GetRequiredService<IOmnivaService>();
            var lockers = services.GetRequiredService<IParcelLockerRepository>();

            var source = await omniva.GetLockersAsync(ct);
            if (source.Count == 0)
            {
                Logger.LogWarning("Omniva returned no parcel machines; the existing locker directory is left unchanged");
                return;
            }

            var rows = source.Select(l => new ParcelLocker
            {
                Provider = Provider,
                Code = l.Code,
                Name = l.Name,
                Country = l.Country,
                City = l.City,
                Address = l.Address,
                Lat = l.Lat,
                Lng = l.Lng,
                IsActive = true
            }).ToList();

            var count = await lockers.UpsertAsync(Provider, rows);
            Logger.LogInformation("Omniva parcel machine directory refreshed: {Count} locations", count);
        }
    }
}

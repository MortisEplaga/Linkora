using Linkora.Repositories;

namespace Linkora.Services
{
    public class PointsLedgerPromotionService(ILogger<PointsLedgerPromotionService> logger, IServiceScopeFactory serviceScopeFactory) : BackgroundService
    {
        private readonly ILogger<PointsLedgerPromotionService> _logger = logger;
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromHours(6);
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PointsLedgerPromotionService started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IPointsLedgerRepository>();
                    var promoted = await repo.PromoteDueEntriesAsync();
                    if (promoted > 0) _logger.LogInformation("Promoted {Count} pending points entries", promoted);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error promoting pending points entries");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
    }
}
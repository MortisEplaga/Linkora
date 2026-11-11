using Linkora.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Linq;

namespace Linkora.Repositories
{
    public class ShipmentRepository(IConfiguration configuration) : SqlRepositoryBase(configuration), IShipmentRepository
    {
        private const string RequestColumns = "Id, ProductId, BuyerId, SellerId, LockerId, Status, ReceiverName, ReceiverPhone, ReceiverEmail, SenderName, SenderPhone, SenderEmail, SenderStreet, SenderCity, SenderPostcode, SenderCountry, Size, WeightKg, Price, PaymentId, CreatedAt, UpdatedAt";
        private const string ShipmentColumns = "Id, RequestId, Provider, PartnerShipmentId, Barcode, Status, LastEventCode, LastEventAt, Attempts, ErrorCode, ErrorMessage, HandoverDeadline, RegisteredAt, DeliveredAt, CreatedAt, UpdatedAt";

        private const int EventCodeMaxLength = 50;
        private const int LocationMaxLength = 200;
        private const string OpenStatuses = "'Registered', 'InTransit', 'ArrivedAtLocker'";

        private static readonly string HandoverEventCodeList =
            string.Join(",", OmnivaEventCodes.HandoverCodes.Select(c => "'" + c.Replace("'", "''") + "'"));
        private const string ViewSelect = @"SELECT r.Id, r.ProductId, p.Name, p.ParcelSize, p.WeightKg, p.Address,
                   r.BuyerId, bu.UserName, r.SellerId, r.Status, l.Name, l.City,
                   r.ReceiverName, r.ReceiverPhone, r.ReceiverEmail, r.Size, r.WeightKg, r.Price, r.PaymentId, r.CreatedAt,
                   s.Id, s.Status, s.Barcode, s.HandoverDeadline, s.ErrorCode
            FROM ShipmentRequests r
            INNER JOIN Products p ON p.Id = r.ProductId
            LEFT JOIN Users bu ON bu.Id = r.BuyerId
            LEFT JOIN ParcelLockers l ON l.Id = r.LockerId
            LEFT JOIN Shipments s ON s.RequestId = r.Id";

        private static ShipmentRequest MapRequest(SqlDataReader r) => new()
        {
            Id = r.GetInt32(0),
            ProductId = r.GetInt32(1),
            BuyerId = r.GetInt32(2),
            SellerId = r.GetInt32(3),
            LockerId = r.GetInt32(4),
            Status = Enum.Parse<ShipmentRequestStatus>(r.GetString(5)),
            ReceiverName = r.GetStringOrDefault(6),
            ReceiverPhone = r.GetStringOrNull(7),
            ReceiverEmail = r.GetStringOrNull(8),
            SenderName = r.GetStringOrNull(9),
            SenderPhone = r.GetStringOrNull(10),
            SenderEmail = r.GetStringOrNull(11),
            SenderStreet = r.GetStringOrNull(12),
            SenderCity = r.GetStringOrNull(13),
            SenderPostcode = r.GetStringOrNull(14),
            SenderCountry = r.GetStringOrNull(15),
            Size = r.GetStringOrNull(16),
            WeightKg = r.GetDecimalOrNull(17),
            Price = r.GetDecimalOrNull(18),
            PaymentId = r.GetInt32OrNull(19),
            CreatedAt = r.GetDateTime(20),
            UpdatedAt = r.GetDateTime(21)
        };
        private static Shipment MapShipment(SqlDataReader r) => new()
        {
            Id = r.GetInt32(0),
            RequestId = r.GetInt32(1),
            Provider = r.GetString(2),
            PartnerShipmentId = r.GetString(3),
            Barcode = r.GetStringOrNull(4),
            Status = Enum.Parse<ShipmentStatus>(r.GetString(5)),
            LastEventCode = r.GetStringOrNull(6),
            LastEventAt = r.GetDateTimeOrNull(7),
            Attempts = r.GetInt32(8),
            ErrorCode = r.GetStringOrNull(9),
            ErrorMessage = r.GetStringOrNull(10),
            HandoverDeadline = r.GetDateTimeOrNull(11),
            RegisteredAt = r.GetDateTimeOrNull(12),
            DeliveredAt = r.GetDateTimeOrNull(13),
            CreatedAt = r.GetDateTime(14),
            UpdatedAt = r.GetDateTime(15)
        };
        private static ShipmentRequestView MapView(SqlDataReader r) => new()
        {
            Id = r.GetInt32(0),
            ProductId = r.GetInt32(1),
            ProductName = r.GetStringOrDefault(2),
            ProductSize = r.GetStringOrNull(3),
            ProductWeightKg = r.GetDecimalOrNull(4),
            ProductAddress = r.GetStringOrNull(5),
            BuyerId = r.GetInt32(6),
            BuyerName = r.GetStringOrDefault(7),
            SellerId = r.GetInt32(8),
            Status = Enum.Parse<ShipmentRequestStatus>(r.GetString(9)),
            LockerName = r.GetStringOrDefault(10),
            LockerCity = r.GetStringOrNull(11),
            ReceiverName = r.GetStringOrDefault(12),
            ReceiverPhone = r.GetStringOrNull(13),
            ReceiverEmail = r.GetStringOrNull(14),
            Size = r.GetStringOrNull(15),
            WeightKg = r.GetDecimalOrNull(16),
            Price = r.GetDecimalOrNull(17),
            PaymentId = r.GetInt32OrNull(18),
            CreatedAt = r.GetDateTime(19),
            ShipmentId = r.GetInt32OrNull(20),
            ShipmentState = r.IsDBNull(21) ? null : Enum.Parse<ShipmentStatus>(r.GetString(21)),
            Barcode = r.GetStringOrNull(22),
            HandoverDeadline = r.GetDateTimeOrNull(23),
            ErrorCode = r.GetStringOrNull(24)
        };

        public async Task<int> CreateRequestAsync(ShipmentRequest x) => (await QueryAsync<int>(
            @"INSERT INTO ShipmentRequests (ProductId, BuyerId, SellerId, LockerId, Status, ReceiverName, ReceiverPhone, ReceiverEmail)
              OUTPUT INSERTED.Id
              VALUES (@ProductId, @BuyerId, @SellerId, @LockerId, 'Requested', @Name, @Phone, @Email)",
            r => r.GetInt32(0),
            p =>
            {
                p.AddWithValue("@ProductId", x.ProductId);
                p.AddWithValue("@BuyerId", x.BuyerId);
                p.AddWithValue("@SellerId", x.SellerId);
                p.AddWithValue("@LockerId", x.LockerId);
                p.AddWithValue("@Name", x.ReceiverName);
                p.AddWithValue("@Phone", (object?)x.ReceiverPhone ?? DBNull.Value);
                p.AddWithValue("@Email", (object?)x.ReceiverEmail ?? DBNull.Value);
            }))[0];
        public async Task<ShipmentRequest?> GetRequestAsync(int id) => await QuerySingleAsync($"SELECT {RequestColumns} FROM ShipmentRequests WHERE Id = @Id", MapRequest, p => p.AddWithValue("@Id", id));
        public async Task<ShipmentRequest?> GetRequestByPaymentAsync(int paymentId) => await QuerySingleAsync($"SELECT {RequestColumns} FROM ShipmentRequests WHERE PaymentId = @P", MapRequest, p => p.AddWithValue("@P", paymentId));
        public async Task<List<ShipmentRequestView>> GetSellerRequestsAsync(int sellerId) => await QueryAsync($"{ViewSelect} WHERE r.SellerId = @U ORDER BY r.CreatedAt DESC", MapView, p => p.AddWithValue("@U", sellerId));
        public async Task<List<ShipmentRequestView>> GetBuyerRequestsAsync(int buyerId) => await QueryAsync($"{ViewSelect} WHERE r.BuyerId = @U ORDER BY r.CreatedAt DESC", MapView, p => p.AddWithValue("@U", buyerId));

        public async Task<bool> AcceptRequestAsync(ShipmentRequest x) => await ExecuteAsync(
            @"UPDATE ShipmentRequests
              SET Status = 'Accepted', SenderName = @SName, SenderPhone = @SPhone, SenderEmail = @SEmail, SenderStreet = @SStreet,
                  SenderCity = @SCity, SenderPostcode = @SPostcode, SenderCountry = @SCountry,
                  Size = @Size, WeightKg = @Weight, Price = @Price, AcceptedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME()
              WHERE Id = @Id AND Status = 'Requested'
                AND NOT EXISTS (SELECT 1 FROM ShipmentRequests o
                                WHERE o.ProductId = (SELECT ProductId FROM ShipmentRequests WHERE Id = @Id)
                                  AND o.Id <> @Id AND o.Status IN ('Accepted', 'Paid'))",
            p =>
            {
                p.AddWithValue("@Id", x.Id);
                p.AddWithValue("@SName", (object?)x.SenderName ?? DBNull.Value);
                p.AddWithValue("@SPhone", (object?)x.SenderPhone ?? DBNull.Value);
                p.AddWithValue("@SEmail", (object?)x.SenderEmail ?? DBNull.Value);
                p.AddWithValue("@SStreet", (object?)x.SenderStreet ?? DBNull.Value);
                p.AddWithValue("@SCity", (object?)x.SenderCity ?? DBNull.Value);
                p.AddWithValue("@SPostcode", (object?)x.SenderPostcode ?? DBNull.Value);
                p.AddWithValue("@SCountry", (object?)x.SenderCountry ?? DBNull.Value);
                p.AddWithValue("@Size", (object?)x.Size ?? DBNull.Value);
                p.AddWithValue("@Weight", (object?)x.WeightKg ?? DBNull.Value);
                p.AddWithValue("@Price", (object?)x.Price ?? DBNull.Value);
            }) > 0;
        public async Task<bool> HasReservedRequestAsync(int productId, int exceptRequestId) => (await QueryAsync<int>(
            "SELECT COUNT(*) FROM ShipmentRequests WHERE ProductId = @P AND Id <> @Id AND Status IN ('Accepted', 'Paid')",
            r => r.GetInt32(0),
            p =>
            {
                p.AddWithValue("@P", productId);
                p.AddWithValue("@Id", exceptRequestId);
            }))[0] > 0;
        public async Task<bool> AttachPaymentAsync(int requestId, int paymentId) => await ExecuteAsync(
            "UPDATE ShipmentRequests SET PaymentId = @P, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND Status = 'Accepted'",
            p =>
            {
                p.AddWithValue("@P", paymentId);
                p.AddWithValue("@Id", requestId);
            }) > 0;

        public async Task<List<ReleasedAcceptance>> ResetStaleAcceptancesAsync(DateTime cutoffUtc) => await ExecuteInTransactionAsync(async (conn, tx) =>
        {
            await using var cmd = new SqlCommand(
                @"DECLARE @released TABLE (RequestId int NOT NULL, BuyerId int NOT NULL, SellerId int NOT NULL, ProductId int NOT NULL);

                  UPDATE r
                  SET Status = 'Requested', AcceptedAt = NULL, Price = NULL, PaymentId = NULL, UpdatedAt = SYSUTCDATETIME()
                  OUTPUT inserted.Id, inserted.BuyerId, inserted.SellerId, inserted.ProductId INTO @released
                  FROM ShipmentRequests r
                  WHERE r.Status = 'Accepted' AND r.AcceptedAt < @Cutoff
                    AND NOT EXISTS (SELECT 1 FROM Payments p
                                    WHERE p.Id = r.PaymentId
                                      AND (p.Status = 'Completed' OR (p.Status <> 'Failed' AND p.CreatedAt >= @Cutoff)));

                  UPDATE p
                  SET Status = 'Failed'
                  FROM Payments p
                  JOIN @released x ON p.PurposeType = N'Shipping' AND p.Reference LIKE N'SHIP' + CAST(x.RequestId AS nvarchar(20)) + N'-%'
                  WHERE p.Status IN ('Created', 'Pending') AND p.CreatedAt < @Cutoff;

                  SELECT RequestId, BuyerId, SellerId, ProductId FROM @released;",
                conn, tx);
            cmd.Parameters.AddWithValue("@Cutoff", cutoffUtc);

            var released = new List<ReleasedAcceptance>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                released.Add(new ReleasedAcceptance(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3)));
            return released;
        });
        public async Task<bool> TransitionRequestAsync(int id, ShipmentRequestStatus from, ShipmentRequestStatus to) => await ExecuteAsync(
            "UPDATE ShipmentRequests SET Status = @To, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND Status = @From",
            p =>
            {
                p.AddWithValue("@Id", id);
                p.AddWithValue("@From", from.ToString());
                p.AddWithValue("@To", to.ToString());
            }) > 0;
        public async Task<ProductShippingInfo?> GetProductShippingAsync(int productId) => await QuerySingleAsync(
            "SELECT Id, UserId, Status, ShippingEnabled, ParcelSize, WeightKg FROM Products WHERE Id = @Id",
            r => new ProductShippingInfo(r.GetInt32(0), r.GetInt32OrNull(1), r.GetStringOrDefault(2), r.GetBooleanOrDefault(3), r.GetStringOrNull(4), r.GetDecimalOrNull(5)),
            p => p.AddWithValue("@Id", productId));
        public async Task<bool> SetProductShippingAsync(int productId, int sellerId, bool enabled, string? size, decimal? weightKg) => await ExecuteAsync(
            "UPDATE Products SET ShippingEnabled = @E, ParcelSize = @S, WeightKg = @W WHERE Id = @Id AND UserId = @U",
            p =>
            {
                p.AddWithValue("@E", enabled);
                p.AddWithValue("@S", (object?)size ?? DBNull.Value);
                p.AddWithValue("@W", (object?)weightKg ?? DBNull.Value);
                p.AddWithValue("@Id", productId);
                p.AddWithValue("@U", sellerId);
            }) > 0;

        public async Task<int> GetOrCreateShipmentAsync(int requestId, string partnerShipmentId) => await ExecuteInTransactionAsync(async (conn, tx) =>
        {
            await using (var insert = new SqlCommand(@"IF NOT EXISTS (SELECT 1 FROM Shipments WITH (UPDLOCK, HOLDLOCK) WHERE RequestId = @R)
                                                      INSERT INTO Shipments (RequestId, PartnerShipmentId) VALUES (@R, @P);", conn, tx))
            {
                insert.Parameters.AddWithValue("@R", requestId);
                insert.Parameters.AddWithValue("@P", partnerShipmentId);
                await insert.ExecuteNonQueryAsync();
            }

            await using var select = new SqlCommand("SELECT Id FROM Shipments WHERE RequestId = @R", conn, tx);
            select.Parameters.AddWithValue("@R", requestId);
            return (int)(await select.ExecuteScalarAsync())!;
        });
        public async Task<Shipment?> GetShipmentAsync(int id) => await QuerySingleAsync($"SELECT {ShipmentColumns} FROM Shipments WHERE Id = @Id", MapShipment, p => p.AddWithValue("@Id", id));
        public async Task<Shipment?> GetShipmentByRequestAsync(int requestId) => await QuerySingleAsync($"SELECT {ShipmentColumns} FROM Shipments WHERE RequestId = @R", MapShipment, p => p.AddWithValue("@R", requestId));
        public async Task<Shipment?> GetShipmentByBarcodeAsync(string barcode) => await QuerySingleAsync($"SELECT {ShipmentColumns} FROM Shipments WHERE Barcode = @B", MapShipment, p => p.AddWithValue("@B", barcode));
        public async Task<List<Shipment>> GetOpenShipmentsAsync() => await QueryAsync($"SELECT {ShipmentColumns} FROM Shipments WHERE Status IN ({OpenStatuses}) AND Barcode IS NOT NULL ORDER BY Id", MapShipment);
        public async Task<List<Shipment>> GetPendingShipmentsAsync(int olderThanMinutes) => await QueryAsync(
            $"SELECT {ShipmentColumns} FROM Shipments WHERE Status = 'Pending' AND Barcode IS NULL AND UpdatedAt <= DATEADD(MINUTE, -@Age, SYSUTCDATETIME()) ORDER BY Id",
            MapShipment,
            p => p.AddWithValue("@Age", olderThanMinutes));

        public async Task<List<Shipment>> GetExpiredHandoverShipmentsAsync() => await QueryAsync(
            $@"SELECT {ShipmentColumns} FROM Shipments s
               WHERE s.Status = 'Registered' AND s.HandoverDeadline < SYSUTCDATETIME()
                 AND NOT EXISTS (SELECT 1 FROM ShipmentEvents e
                                 WHERE e.ShipmentId = s.Id AND e.EventCode IN ({HandoverEventCodeList}))
               ORDER BY s.Id",
            MapShipment);
        public async Task<bool> MarkHandoverExpiredAsync(int shipmentId) => await ExecuteAsync(
            $@"UPDATE Shipments
              SET Status = 'Failed', ErrorCode = 'HANDOVER_EXPIRED', ErrorMessage = 'Handover deadline passed without a handover event', UpdatedAt = SYSUTCDATETIME()
              WHERE Id = @Id AND Status = 'Registered'
                AND NOT EXISTS (SELECT 1 FROM ShipmentEvents e
                                WHERE e.ShipmentId = @Id AND e.EventCode IN ({HandoverEventCodeList}))",
            p => p.AddWithValue("@Id", shipmentId)) > 0;
        public async Task<bool> TryBeginRegistrationAsync(int shipmentId, int expectedAttempts) => await ExecuteAsync(
            "UPDATE Shipments SET Attempts = Attempts + 1, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND Status = 'Pending' AND Barcode IS NULL AND Attempts = @A",
            p =>
            {
                p.AddWithValue("@Id", shipmentId);
                p.AddWithValue("@A", expectedAttempts);
            }) > 0;
        public async Task MarkRegisteredAsync(int shipmentId, string barcode, DateTime handoverDeadlineUtc)
        {
            await ExecuteAsync(
            @"UPDATE Shipments
              SET Barcode = @B, Status = 'Registered', RegisteredAt = SYSUTCDATETIME(), HandoverDeadline = @D,
                  ErrorCode = NULL, ErrorMessage = NULL, UpdatedAt = SYSUTCDATETIME()
              WHERE Id = @Id",
            p =>
            {
                p.AddWithValue("@Id", shipmentId);
                p.AddWithValue("@B", barcode);
                p.AddWithValue("@D", handoverDeadlineUtc);
            });
        }
        public async Task MarkRegistrationFailedAsync(int shipmentId, string? errorCode, string? errorMessage)
        {
            await ExecuteAsync(
            "UPDATE Shipments SET Status = 'Failed', ErrorCode = @C, ErrorMessage = @M, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id",
            p =>
            {
                p.AddWithValue("@Id", shipmentId);
                p.AddWithValue("@C", (object?)Truncate(errorCode, 200) ?? DBNull.Value);
                p.AddWithValue("@M", (object?)Truncate(errorMessage, 1000) ?? DBNull.Value);
            });
        }

        public async Task<bool> ApplyEventAsync(int shipmentId, ShipmentStatus? status, string eventCode, DateTime eventAtUtc) => await ExecuteAsync(
            @"UPDATE Shipments
              SET Status = COALESCE(@S, Status), LastEventCode = @C, LastEventAt = @At,
                  DeliveredAt = CASE WHEN @S = 'Delivered' THEN @At ELSE DeliveredAt END,
                  UpdatedAt = SYSUTCDATETIME()
              WHERE Id = @Id AND Status <> 'Failed' AND (LastEventAt IS NULL OR LastEventAt < @At)",
            p =>
            {
                p.Add("@Id", SqlDbType.Int).Value = shipmentId;
                p.Add("@S", SqlDbType.NVarChar, 20).Value = (object?)status?.ToString() ?? DBNull.Value;
                p.Add("@C", SqlDbType.NVarChar, EventCodeMaxLength).Value = Truncate(eventCode, EventCodeMaxLength)!;
                p.Add("@At", SqlDbType.DateTime2).Value = eventAtUtc;
            }) > 0;

        public async Task<List<ShipmentEvent>> SaveEventsAsync(IEnumerable<ShipmentEvent> events)
        {
            var inserted = new List<ShipmentEvent>();
            await ExecuteInTransactionAsync(async (conn, tx) =>
            {
                foreach (var e in events)
                {
                    await using var cmd = new SqlCommand(@"INSERT INTO ShipmentEvents (ShipmentId, ExternalEventId, EventCode, EventAt, Location, RawJson)
                          SELECT @S, @E, @C, @At, @L, @Raw
                          WHERE NOT EXISTS (SELECT 1 FROM ShipmentEvents WHERE ShipmentId = @S AND ExternalEventId = @E)", conn, tx);
                    cmd.Parameters.AddWithValue("@S", e.ShipmentId);
                    cmd.Parameters.AddWithValue("@E", e.ExternalEventId);
                    cmd.Parameters.AddWithValue("@C", Truncate(e.EventCode, EventCodeMaxLength)!);
                    cmd.Parameters.AddWithValue("@At", e.EventAt);
                    cmd.Parameters.AddWithValue("@L", (object?)Truncate(e.Location, LocationMaxLength) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Raw", (object?)e.RawJson ?? DBNull.Value);
                    if (await cmd.ExecuteNonQueryAsync() > 0) inserted.Add(e);
                }
            });
            return inserted;
        }
        public async Task<List<ShipmentEvent>> GetEventsAsync(int shipmentId) => await QueryAsync(
            "SELECT Id, ShipmentId, ExternalEventId, EventCode, EventAt, Location, CreatedAt FROM ShipmentEvents WHERE ShipmentId = @S ORDER BY EventAt, Id",
            r => new ShipmentEvent
            {
                Id = r.GetInt64(0),
                ShipmentId = r.GetInt32(1),
                ExternalEventId = r.GetInt64(2),
                EventCode = r.GetString(3),
                EventAt = r.GetDateTime(4),
                Location = r.GetStringOrNull(5),
                CreatedAt = r.GetDateTime(6)
            },
            p => p.AddWithValue("@S", shipmentId));
        public async Task<long> GetLastEventIdAsync(string provider) => (await QueryAsync<long>("SELECT LastEventId FROM CarrierSyncState WHERE Provider = @P", r => r.GetInt64(0), p => p.AddWithValue("@P", provider))).FirstOrDefault();
        public async Task SetLastEventIdAsync(string provider, long eventId)
        {
            await ExecuteAsync(
            @"MERGE CarrierSyncState AS t
              USING (SELECT @P AS Provider) AS s ON t.Provider = s.Provider
              WHEN MATCHED AND t.LastEventId < @I THEN UPDATE SET LastEventId = @I, UpdatedAt = SYSUTCDATETIME()
              WHEN NOT MATCHED THEN INSERT (Provider, LastEventId, UpdatedAt) VALUES (@P, @I, SYSUTCDATETIME());",
            p =>
            {
                p.AddWithValue("@P", provider);
                p.AddWithValue("@I", eventId);
            });
        }
        private static string? Truncate(string? s, int max) => s == null || s.Length <= max ? s : s[..max];
    }
}

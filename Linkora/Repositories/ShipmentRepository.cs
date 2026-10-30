using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class ShipmentRepository(IConfiguration configuration) : SqlRepositoryBase(configuration), IShipmentRepository
    {
        private const string RequestColumns = "Id, ProductId, BuyerId, SellerId, LockerId, Status, ReceiverName, ReceiverPhone, ReceiverEmail, SenderName, SenderPhone, SenderEmail, SenderStreet, SenderCity, SenderPostcode, SenderCountry, Size, WeightKg, Price, PaymentId, CreatedAt, UpdatedAt";
        private const string ShipmentColumns = "Id, RequestId, Provider, PartnerShipmentId, Barcode, Status, LastEventCode, LastEventAt, Attempts, ErrorCode, ErrorMessage, HandoverDeadline, RegisteredAt, DeliveredAt, CreatedAt, UpdatedAt";
        private const string OpenStatuses = "'Registered', 'InTransit', 'ArrivedAtLocker'";
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
        public async Task<List<ShipmentRequest>> GetRequestsForSellerAsync(int sellerId) => await QueryAsync($"SELECT TOP (200) {RequestColumns} FROM ShipmentRequests WHERE SellerId = @U ORDER BY CreatedAt DESC", MapRequest, p => p.AddWithValue("@U", sellerId));
        public async Task<List<ShipmentRequest>> GetRequestsForBuyerAsync(int buyerId) => await QueryAsync($"SELECT TOP (200) {RequestColumns} FROM ShipmentRequests WHERE BuyerId = @U ORDER BY CreatedAt DESC", MapRequest, p => p.AddWithValue("@U", buyerId));
        public async Task<bool> AcceptRequestAsync(ShipmentRequest x) => await ExecuteAsync(
            @"UPDATE ShipmentRequests
              SET Status = 'Accepted', SenderName = @SName, SenderPhone = @SPhone, SenderEmail = @SEmail, SenderStreet = @SStreet,
                  SenderCity = @SCity, SenderPostcode = @SPostcode, SenderCountry = @SCountry,
                  Size = @Size, WeightKg = @Weight, Price = @Price, UpdatedAt = SYSUTCDATETIME()
              WHERE Id = @Id AND Status = 'Requested'",
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
        public async Task<bool> AttachPaymentAsync(int requestId, int paymentId) => await ExecuteAsync(
            "UPDATE ShipmentRequests SET PaymentId = @P, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND Status = 'Accepted'",
            p =>
            {
                p.AddWithValue("@P", paymentId);
                p.AddWithValue("@Id", requestId);
            }) > 0;
        public async Task<bool> TransitionRequestAsync(int id, ShipmentRequestStatus from, ShipmentRequestStatus to) => await ExecuteAsync(
            "UPDATE ShipmentRequests SET Status = @To, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND Status = @From",
            p =>
            {
                p.AddWithValue("@Id", id);
                p.AddWithValue("@From", from.ToString());
                p.AddWithValue("@To", to.ToString());
            }) > 0;
        public async Task<int> GetOrCreateShipmentAsync(int requestId, string partnerShipmentId) => (await QueryAsync<int>(@"SET XACT_ABORT ON;
              BEGIN TRAN;
              IF NOT EXISTS (SELECT 1 FROM Shipments WITH (UPDLOCK, HOLDLOCK) WHERE RequestId = @R)
                  INSERT INTO Shipments (RequestId, PartnerShipmentId) VALUES (@R, @P);
              SELECT Id FROM Shipments WHERE RequestId = @R;
              COMMIT;",
            r => r.GetInt32(0),
            p =>
            {
                p.AddWithValue("@R", requestId);
                p.AddWithValue("@P", partnerShipmentId);
            }))[0];
        public async Task<Shipment?> GetShipmentAsync(int id) => await QuerySingleAsync($"SELECT {ShipmentColumns} FROM Shipments WHERE Id = @Id", MapShipment, p => p.AddWithValue("@Id", id));
        public async Task<Shipment?> GetShipmentByRequestAsync(int requestId) => await QuerySingleAsync($"SELECT {ShipmentColumns} FROM Shipments WHERE RequestId = @R", MapShipment, p => p.AddWithValue("@R", requestId));
        public async Task<Shipment?> GetShipmentByBarcodeAsync(string barcode) => await QuerySingleAsync($"SELECT {ShipmentColumns} FROM Shipments WHERE Barcode = @B", MapShipment, p => p.AddWithValue("@B", barcode));
        public async Task<List<Shipment>> GetOpenShipmentsAsync() => await QueryAsync($"SELECT {ShipmentColumns} FROM Shipments WHERE Status IN ({OpenStatuses}) AND Barcode IS NOT NULL ORDER BY Id", MapShipment);
        public async Task<List<Shipment>> GetPendingShipmentsAsync() => await QueryAsync($"SELECT {ShipmentColumns} FROM Shipments WHERE Status = 'Pending' AND Barcode IS NULL ORDER BY Id", MapShipment);
        public async Task<bool> TryBeginRegistrationAsync(int shipmentId, int expectedAttempts) => await ExecuteAsync(
            "UPDATE Shipments SET Attempts = Attempts + 1, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND Status = 'Pending' AND Barcode IS NULL AND Attempts = @A",
            p =>
            {
                p.AddWithValue("@Id", shipmentId);
                p.AddWithValue("@A", expectedAttempts);
            }) > 0;
        public async Task MarkRegisteredAsync(int shipmentId, string barcode, DateTime handoverDeadlineUtc) => await ExecuteAsync(
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
        public async Task MarkRegistrationFailedAsync(int shipmentId, string? errorCode, string? errorMessage) => await ExecuteAsync(
            "UPDATE Shipments SET Status = 'Failed', ErrorCode = @C, ErrorMessage = @M, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id",
            p =>
            {
                p.AddWithValue("@Id", shipmentId);
                p.AddWithValue("@C", (object?)Truncate(errorCode, 200) ?? DBNull.Value);
                p.AddWithValue("@M", (object?)Truncate(errorMessage, 1000) ?? DBNull.Value);
            });
        public async Task<bool> ResetFailedShipmentAsync(int shipmentId) => await ExecuteAsync(
            "UPDATE Shipments SET Status = 'Pending', ErrorCode = NULL, ErrorMessage = NULL, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND Status = 'Failed' AND Barcode IS NULL",
            p => p.AddWithValue("@Id", shipmentId)) > 0;
        public async Task ApplyStatusAsync(int shipmentId, ShipmentStatus status, string eventCode, DateTime eventAtUtc) => await ExecuteAsync(
            @"UPDATE Shipments
              SET Status = @S, LastEventCode = @C, LastEventAt = @At,
                  DeliveredAt = CASE WHEN @S = 'Delivered' THEN @At ELSE DeliveredAt END,
                  UpdatedAt = SYSUTCDATETIME()
              WHERE Id = @Id AND (LastEventAt IS NULL OR LastEventAt <= @At)",
            p =>
            {
                p.AddWithValue("@Id", shipmentId);
                p.AddWithValue("@S", status.ToString());
                p.AddWithValue("@C", eventCode);
                p.AddWithValue("@At", eventAtUtc);
            });
        public async Task<int> SaveEventsAsync(IEnumerable<ShipmentEvent> events)
        {
            var inserted = 0;
            await ExecuteInTransactionAsync(async (conn, tx) =>
            {
                foreach (var e in events)
                {
                    await using var cmd = new SqlCommand(@"INSERT INTO ShipmentEvents (ShipmentId, ExternalEventId, EventCode, EventAt, Location, RawJson)
                          SELECT @S, @E, @C, @At, @L, @Raw
                          WHERE NOT EXISTS (SELECT 1 FROM ShipmentEvents WHERE ShipmentId = @S AND ExternalEventId = @E)", conn, tx);
                    cmd.Parameters.AddWithValue("@S", e.ShipmentId);
                    cmd.Parameters.AddWithValue("@E", e.ExternalEventId);
                    cmd.Parameters.AddWithValue("@C", e.EventCode);
                    cmd.Parameters.AddWithValue("@At", e.EventAt);
                    cmd.Parameters.AddWithValue("@L", (object?)e.Location ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Raw", (object?)e.RawJson ?? DBNull.Value);
                    inserted += await cmd.ExecuteNonQueryAsync();
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
        public async Task SetLastEventIdAsync(string provider, long eventId) => await ExecuteAsync(
            "UPDATE CarrierSyncState SET LastEventId = @I, UpdatedAt = SYSUTCDATETIME() WHERE Provider = @P AND LastEventId < @I",
            p =>
            {
                p.AddWithValue("@P", provider);
                p.AddWithValue("@I", eventId);
            });
        private static string? Truncate(string? s, int max) => s == null || s.Length <= max ? s : s[..max];
    }
}
using Microsoft.Data.SqlClient;

namespace Linkora.Services
{
    public class ArchiveOldProductsService : BackgroundService
    {
        private readonly ILogger<ArchiveOldProductsService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromHours(24);

        public ArchiveOldProductsService(
            ILogger<ArchiveOldProductsService> logger,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ArchiveOldProductsService started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ArchiveExpiredProducts();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error archiving expired products");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
        private async Task ArchiveExpiredProducts()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var sql = @"
UPDATE Products
SET Status = 'Archived'
WHERE Status = 'Active'
  AND (
        (ExpiresAt IS NOT NULL AND ExpiresAt < GETDATE())
        OR
        (ExpiresAt IS NULL AND DATEADD(DAY, PublishDurationDays, CreatedAt) < GETDATE())
      )";

            using var command = new SqlCommand(sql, connection);
            var affected = await command.ExecuteNonQueryAsync();

            _logger.LogInformation("Archived {Count} expired products", affected);
        }
    }
}

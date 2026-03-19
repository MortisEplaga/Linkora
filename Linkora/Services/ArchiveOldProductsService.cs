using Microsoft.Data.SqlClient;

namespace Linkora.Services
{
    public class ArchiveOldProductsService : BackgroundService
    {
        private readonly ILogger<ArchiveOldProductsService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromHours(24); // Раз в сутки

        public ArchiveOldProductsService(
            ILogger<ArchiveOldProductsService> logger,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ArchiveOldProductsService запущен");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ArchiveOldProducts();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при архивации старых продуктов");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task ArchiveOldProducts()
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
                AND CreatedAt < DATEADD(month, -1, GETDATE())";

            using var command = new SqlCommand(sql, connection);
            var affectedRows = await command.ExecuteNonQueryAsync();

            _logger.LogInformation("Архивировано {Count} старых продуктов", affectedRows);
        }
    }
}
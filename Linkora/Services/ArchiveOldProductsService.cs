using Linkora.Repositories;

namespace Linkora.Services
{
    public class ArchiveOldProductsService : BackgroundService
    {
        private readonly ILogger<ArchiveOldProductsService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromHours(24);

        public ArchiveOldProductsService(ILogger<ArchiveOldProductsService> logger, IServiceScopeFactory serviceScopeFactory)
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
        private async Task ArchiveExpiredProducts() => _logger.LogInformation("Archived {Count} expired products", await _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<IProductRepository>().ArchiveExpiredProductsAsync());
    }
}

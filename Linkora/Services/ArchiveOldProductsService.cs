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
                    await CleanupAsync();
                    await CleanupOldSessionsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error archiving expired products");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
        private async Task ArchiveExpiredProducts() => _logger.LogInformation("Archived {Count} expired products", await _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<IProductRepository>().ArchiveExpiredProductsAsync());
        private async Task CleanupAsync()
        {
            int deleted = await _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<IProductRepository>().ProcessMediaDeletionQueueAsync();
            if (deleted > 0) _logger.LogInformation("Processed {Count} media files for deletion", deleted);
            else _logger.LogInformation("No pending media files to delete.");
        }
        private async Task CleanupOldSessionsAsync()
        {
            var deleted = await _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<IUserSessionRepository>().DeleteOldSessionsAsync(30);
            if (deleted > 0) _logger.LogInformation("Deleted {Count} old user session records", deleted);
        }
    }
}

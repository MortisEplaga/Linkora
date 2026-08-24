using Linkora.Models;
using Linkora.Repositories;

namespace Linkora.Services
{
    public interface INotificationService
    {
        Task<int> CreateAsync(int userId, int? fromUserId, int? productId, string text);
        Task<List<NotificationViewModel>> GetByUserAsync(int userId, int count = 20);
        Task<int> GetUnreadCountAsync(int userId);
        Task MarkReadAsync(int notificationId, int userId);
        Task MarkAllReadAsync(int userId);
        Task NotifySubscribersAsync(int authorId, int productId, string productName, string authorName);
    }
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _repository;
        private readonly INotificationPreferencesRepository _preferencesRepository;
        private readonly INotificationRealTimeSender _realTimeSender;

        public NotificationService(
            INotificationRepository repository,
            INotificationPreferencesRepository preferencesRepository,
            INotificationRealTimeSender realTimeSender)
        {
            _repository = repository;
            _preferencesRepository = preferencesRepository;
            _realTimeSender = realTimeSender;
        }
        public async Task<int> CreateAsync(int userId, int? fromUserId, int? productId, string text)
        {
            var id = await _repository.CreateAsync(userId, fromUserId, productId, text);

            await _realTimeSender.SendAsync(new NotificationDispatch
            {
                Id = id,
                TargetUserId = userId,
                FromUserId = fromUserId,
                ProductId = productId,
                Text = text,
                CreatedAt = DateTime.UtcNow,
            });

            return id;
        }
        public async Task<List<NotificationViewModel>> GetByUserAsync(int userId, int count = 20)
        {
            var prefs = await _preferencesRepository.GetAsync(userId);
            var notifications = await _repository.GetByUserAsync(userId);

            return notifications
                .Where(n => IsAllowed(n.Text, prefs))
                .Take(count)
                .ToList();
        }
        public async Task<int> GetUnreadCountAsync(int userId)
        {
            var prefs = await _preferencesRepository.GetAsync(userId);
            var texts = await _repository.GetUnreadTextsAsync(userId);

            return texts.Count(t => IsAllowed(t, prefs));
        }
        public Task MarkReadAsync(int notificationId, int userId) => _repository.MarkReadAsync(notificationId, userId);
        public Task MarkAllReadAsync(int userId) => _repository.MarkAllReadAsync(userId);
        public async Task NotifySubscribersAsync(int authorId, int productId, string productName, string authorName)
        {
            var text = $"{authorName} posted a new listing: {productName}";

            var created = await _repository.CreateForSubscribersAsync(authorId, productId, text);
            if (created.Count == 0) return;

            var createdAt = DateTime.UtcNow;

            foreach (var (notificationId, followerId) in created)
                await _realTimeSender.SendAsync(new NotificationDispatch
                {
                    Id = notificationId,
                    TargetUserId = followerId,
                    FromUserId = authorId,
                    ProductId = productId,
                    Text = text,
                    ProductName = productName,
                    CreatedAt = createdAt,
                });
        }
        private static bool IsAllowed(string text, NotificationPreferences prefs) => NotificationCategorizer.Categorize(text) switch
            {
                "Deals" => prefs.Deals,
                "Reviews" => prefs.Reviews,
                "Moderation" => prefs.Moderation,
                "Account" => prefs.Account,
                "Favourites" => prefs.Favourites,
                _ => prefs.NewListings,
            };
    }
}
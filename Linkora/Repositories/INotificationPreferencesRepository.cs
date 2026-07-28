using Linkora.Models;

namespace Linkora.Repositories
{
    public interface INotificationPreferencesRepository
    {
        Task<NotificationPreferences> GetAsync(int userId);
        Task SaveAsync(NotificationPreferences prefs);
    }
}
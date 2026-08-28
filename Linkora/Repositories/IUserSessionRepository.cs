namespace Linkora.Repositories
{
    public interface IUserSessionRepository
    {
        Task<int> StartSessionAsync(int userId);
        Task CloseSessionAsync(int sessionId);
        Task CloseOpenSessionsAsync(int userId);
        Task<int> DeleteOldSessionsAsync(int retentionDays = 30);
    }
}
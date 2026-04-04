namespace Linkora.Repositories
{
    public interface ISubscriptionRepository
    {
        Task<bool> IsSubscribedAsync(int followerId, int sellerId);
        Task<bool> ToggleAsync(int followerId, int sellerId);
        Task<int> GetSubscriberCountAsync(int sellerId);
    }
}
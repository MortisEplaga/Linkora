using Linkora.Models;

namespace Linkora.Repositories
{
    public interface ISubscriptionRepository
    {
        Task<bool> IsSubscribedAsync(int followerId, int FollowingId);
        Task<bool> ToggleAsync(int followerId, int FollowingId);
        Task<int> GetSubscriberCountAsync(int FollowingId);
        Task<List<SellerViewModel>> GetFollowingAsync(int followerId);
    }
}
using Linkora.Models;
using Linkora.Repositories;

namespace Linkora.Services
{
    public interface IAdminService
    {
        Task<(string? OldRole, BanUserResult? BanData)> SetUserRoleAsync(int id, string role);
        Task DeleteUserCascadeAsync(int id);
        Task<ApproveOptionResult> ApproveOptionAsync(int optionId);
        Task<RejectOptionResult> RejectProductByOptionAsync(int optionId, int productId);
    }
    public class AdminService : IAdminService
    {
        private readonly IAdminRepository _admin;
        private readonly IProductRepository _products;

        public AdminService(IAdminRepository admin, IProductRepository products)
            => (_admin, _products) = (admin, products);

        public async Task<(string? OldRole, BanUserResult? BanData)> SetUserRoleAsync(int id, string role)
        {
            var oldRole = await _admin.UpdateUserRoleAsync(id, role);

            if (role != "banned" || oldRole == "banned")
                return (oldRole, null);

            await _products.ArchiveProductsByUserAsync(id);

            var banData = new BanUserResult();
            banData.SubscriberIds.AddRange(await _admin.GetSubscriberIdsAsync(id));
            banData.FavouriteUsers.AddRange(await _admin.GetFavouriteUsersBySellerAsync(id));
            return (oldRole, banData);
        }

        public async Task DeleteUserCascadeAsync(int id)
        {
            var productIds = await _admin.GetUserProductIdsAsync(id);
            foreach (var productId in productIds)
                await _products.DeleteAsync(productId);
            await _admin.DeleteUserAsync(id);
        }

        public async Task<ApproveOptionResult> ApproveOptionAsync(int id)
        {
            var result = await _admin.GetApproveOptionContextAsync(id);
            result.Success = await _products.ApproveSelectOptionAsync(id);
            if (result.Success && result.UserId.HasValue && result.ProductId.HasValue)
                await _admin.DecrementModerationScoreAsync(result.ProductId.Value);
            return result;
        }

        public async Task<RejectOptionResult> RejectProductByOptionAsync(int optionId, int productId)
        {
            var result = await _admin.GetRejectOptionContextAsync(optionId, productId);
            result.Success = await _products.RejectProductAndOptionAsync(optionId, productId);
            return result;
        }
    }
}

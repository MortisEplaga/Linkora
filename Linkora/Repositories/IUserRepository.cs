using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetByIdAsync(int id);
        Task<int> CreateAsync(User user, string passwordHash);
        Task<User?> GetByEmailAsync(string email);
        Task<int> CreateGoogleUserAsync(User user);
        Task UpdateAvatarAsync(int userId, string avatarUrl);
        Task<string> EnsureUniqueUsernameAsync(string baseUsername);
        Task<User?> GetByConfirmationTokenAsync(string token);
        Task ConfirmEmailAsync(string token);
        Task<int> CreateExternalUserAsync(User user);
        Task<User?> GetByFacebookIdAsync(string facebookId);
        Task UpdateFacebookIdAsync(int userId, string facebookId);
        Task MarkForDeletionAsync(int userId, string deletionRequestCode);
        Task<User?> GetByDeletionCodeAsync(string code);
        Task<User?> GetByPhoneAsync(string phone);
        Task SetPasswordResetTokenAsync(int userId, string token, DateTime expiry);
        Task<User?> GetByPasswordResetTokenAsync(string token);
        Task ClearPasswordResetTokenAsync(int userId);
        Task UpdatePasswordHashAsync(int userId, string passwordHash);
        Task AdjustPromotionPointsAsync(int userId, int delta);
        Task<bool> IsBannedAsync(int userId);
    }
}
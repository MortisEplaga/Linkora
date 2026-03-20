using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetByIdAsync(int id);
        Task<int> CreateAsync(User user, string passwordHash);

        // Google OAuth additions
        Task<User?> GetByEmailAsync(string email);
        Task<int> CreateGoogleUserAsync(User user);
        Task UpdateAvatarAsync(int userId, string avatarPath);
        Task<string> EnsureUniqueUsernameAsync(string baseUsername);
    }
}
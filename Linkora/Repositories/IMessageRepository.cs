using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IMessageRepository
    {
        Task<List<Conversation>> GetConversationsAsync(int userId);
        Task<Conversation?> GetConversationAsync(int conversationId, int userId);
        Task<int> GetOrCreateConversationAsync(int productId, int buyerId, int sellerId);
        Task<List<Message>> GetMessagesAsync(int conversationId, int userId);
        Task<int> SendMessageAsync(int conversationId, int senderId, string text);
        Task MarkReadAsync(int conversationId, int userId);
        Task<int> GetUnreadCountAsync(int userId);
        Task<List<User>> GetConversationPartnersAsync(int productId, int userId);
        Task<int> CreateSystemConversationAsync(int productId, int user1Id, int user2Id);
        Task<int> SendSystemMessageAsync(int conversationId, string text);
        Task<bool> CanReviewAsync(int conversationId, int userId);
        Task<int?> GetReviewTargetIdAsync(int conversationId, int userId);
        Task<bool> HasUserReviewedAsync(int conversationId, int userId);
        Task<int> CreateReviewAsync(int authorId, int targetUserId, int productId, int rating, string? comment);
        Task<string> GetUserStatusAsync(int userId);
        Task<int> GetOrCreateSupportConversationAsync(int userId);
    }
}
namespace Linkora.Repositories
{
    public interface ISupportRepository
    {
        Task<int> CreateRequestAsync(string name, string email, string? phone, string message, string? userId);
    }
}
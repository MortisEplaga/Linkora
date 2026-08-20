namespace Linkora.Repositories
{
    public interface ISelectOptionRepository
    {
        Task<int?> FindIdAsync(int paramId, string text, string lang);
        Task<int> CreateAsync(int paramId, string text);
        Task<List<(int Id, string Text)>> GetConfirmedAsync(int paramId, string lang);
    }
}
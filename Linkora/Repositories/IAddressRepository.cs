namespace Linkora.Repositories
{
    public interface IAddressRepository
    {
        Task<List<(int Id, string Name)>> GetCitiesAsync();
        Task<List<(int Id, string Name)>> GetStreetsAsync(int cityId);
        Task<List<(int Id, string Name)>> GetHousesAsync(int streetId);
    }
}

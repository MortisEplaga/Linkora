namespace Linkora.Repositories
{
    public class AddressRepository : SqlRepositoryBase, IAddressRepository
    {
        public AddressRepository(IConfiguration config) : base(config) { }
        public async Task<List<(int Id, string Name)>> GetCitiesAsync() => await QueryAsync("SELECT Id, Name FROM Cities ORDER BY Name", r => (Id: r.GetInt32(0), Name: r.GetString(1)));
        public async Task<List<(int Id, string Name)>> GetStreetsAsync(int cityId) => await QueryAsync("SELECT Id, Name FROM Streets WHERE CityId = @P ORDER BY Name",
                                                        r => (Id: r.GetInt32(0), Name: r.GetString(1)),
                                                        p => p.AddWithValue("@P", cityId));
        public async Task<List<(int Id, string Name)>> GetHousesAsync(int streetId) => await QueryAsync("SELECT Id, Name FROM Houses WHERE StreetId = @P ORDER BY Name",
                                                        r => (Id: r.GetInt32(0), Name: r.GetString(1)),
                                                        p => p.AddWithValue("@P", streetId));
    }
}
using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IShippingTariffRepository
    {
        Task<ShippingTariff?> GetAsync(string provider, string destinationCountry, string size);
        Task<List<ShippingTariff>> GetAllAsync(string provider = "Omniva");
        Task SaveAsync(ShippingTariff tariff);
    }
}
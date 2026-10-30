using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class ShippingTariffRepository(IConfiguration configuration) : SqlRepositoryBase(configuration), IShippingTariffRepository
    {
        private const string Columns = "Id, Provider, DestinationCountry, Size, OmnivaPrice, Markup, Price, MaxWeightKg, IsActive, UpdatedAt";
        private static ShippingTariff Map(SqlDataReader r) => new()
        {
            Id = r.GetInt32(0),
            Provider = r.GetString(1),
            DestinationCountry = r.GetString(2),
            Size = r.GetString(3),
            OmnivaPrice = r.GetDecimal(4),
            Markup = r.GetDecimal(5),
            Price = r.GetDecimal(6),
            MaxWeightKg = r.GetDecimalOrNull(7),
            IsActive = r.GetBoolean(8),
            UpdatedAt = r.GetDateTime(9)
        };
        public async Task<ShippingTariff?> GetAsync(string provider, string destinationCountry, string size) => await QuerySingleAsync(
            $"SELECT {Columns} FROM ShippingTariffs WHERE Provider = @Provider AND DestinationCountry = @Country AND Size = @Size AND IsActive = 1",
            Map, p =>
            {
                p.AddWithValue("@Provider", provider);
                p.AddWithValue("@Country", destinationCountry);
                p.AddWithValue("@Size", size);
            });
        public async Task<List<ShippingTariff>> GetAllAsync(string provider = "Omniva") => await QueryAsync(
            $"SELECT {Columns} FROM ShippingTariffs WHERE Provider = @Provider ORDER BY DestinationCountry, Size",
            Map, p => p.AddWithValue("@Provider", provider));
        public async Task SaveAsync(ShippingTariff t) => await ExecuteAsync(@"MERGE ShippingTariffs AS target
            USING (SELECT @Provider AS Provider, @Country AS DestinationCountry, @Size AS Size) AS source
            ON target.Provider = source.Provider AND target.DestinationCountry = source.DestinationCountry AND target.Size = source.Size
            WHEN MATCHED THEN UPDATE SET OmnivaPrice = @OmnivaPrice, Markup = @Markup, MaxWeightKg = @MaxWeight, IsActive = @IsActive, UpdatedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT (Provider, DestinationCountry, Size, OmnivaPrice, Markup, MaxWeightKg, IsActive)
                                  VALUES (@Provider, @Country, @Size, @OmnivaPrice, @Markup, @MaxWeight, @IsActive);",
            p =>
            {
                p.AddWithValue("@Provider", t.Provider);
                p.AddWithValue("@Country", t.DestinationCountry);
                p.AddWithValue("@Size", t.Size);
                p.AddWithValue("@OmnivaPrice", t.OmnivaPrice);
                p.AddWithValue("@Markup", t.Markup);
                p.AddWithValue("@MaxWeight", (object?)t.MaxWeightKg ?? DBNull.Value);
                p.AddWithValue("@IsActive", t.IsActive);
            });
    }
}
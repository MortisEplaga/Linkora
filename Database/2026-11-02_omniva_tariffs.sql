/* Omniva shipping tariffs: idempotent preparation of the price matrix.
   Run after 2026-10-30_omniva_shipping.sql. Re-runnable: never overwrites prices edited in the admin panel.

   The rows are deliberately created INACTIVE with zero prices. Real OmnivaPrice / Markup come from the
   contract and must be entered (and the tariff activated) in /Admin/ShippingTariffs.

   XL is absent on purpose: Omniva does not deliver XL parcels to parcel machines, and this integration
   always uses deliveryChannel = PARCEL_MACHINE. */

/* 1. Create the 3 countries x 4 sizes matrix if it is not there yet. */
IF OBJECT_ID(N'dbo.ShippingTariffs', N'U') IS NOT NULL
    INSERT INTO dbo.ShippingTariffs (Provider, DestinationCountry, Size, OmnivaPrice, Markup, MaxWeightKg, IsActive)
    SELECT N'Omniva', C.Country, S.Size, 0.00, 0.00, 30.00, 0
    FROM (VALUES ('LV'), ('LT'), ('EE')) AS C(Country)
    CROSS JOIN (VALUES ('XS'), ('S'), ('M'), ('L')) AS S(Size)
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.ShippingTariffs t
        WHERE t.Provider = N'Omniva'
          AND t.DestinationCountry = C.Country
          AND t.Size = S.Size
    );
GO

/* 2. XL is no longer an available size. Disable delivery on legacy listings that still use it
      so that the seller has to pick a valid size again (no silent recalculation of price/weight). */
IF COL_LENGTH('dbo.Products', 'ParcelSize') IS NOT NULL
    UPDATE dbo.Products
       SET ShippingEnabled = 0, ParcelSize = NULL
     WHERE ParcelSize = N'XL';
GO

/* 3. Remove an obsolete XL tariff row, if one was ever created. */
IF OBJECT_ID(N'dbo.ShippingTariffs', N'U') IS NOT NULL
    DELETE FROM dbo.ShippingTariffs WHERE Provider = N'Omniva' AND Size = N'XL';
GO

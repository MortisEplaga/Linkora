/* Shipping (Omniva): schema for SQL Server. The script can be run repeatedly. */

/* ---------- Products: shipping fields ---------- */
IF COL_LENGTH('dbo.Products', 'ShippingEnabled') IS NULL
    ALTER TABLE dbo.Products ADD ShippingEnabled bit NOT NULL CONSTRAINT DF_Products_ShippingEnabled DEFAULT 0;
GO
IF COL_LENGTH('dbo.Products', 'ParcelSize') IS NULL
    ALTER TABLE dbo.Products ADD ParcelSize nvarchar(10) NULL;
GO
IF COL_LENGTH('dbo.Products', 'WeightKg') IS NULL
    ALTER TABLE dbo.Products ADD WeightKg decimal(6,3) NULL;
GO
/* If HIS_Products is filled by a trigger that lists Products columns explicitly,
   add ShippingEnabled, ParcelSize and WeightKg to HIS_Products and to the trigger. */

/* ---------- ParcelLockers: cache of the carrier locker list ---------- */
IF OBJECT_ID(N'dbo.ParcelLockers', N'U') IS NULL
CREATE TABLE dbo.ParcelLockers
(
    Id        int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ParcelLockers PRIMARY KEY,
    Provider  nvarchar(20)  NOT NULL CONSTRAINT DF_ParcelLockers_Provider DEFAULT N'Omniva',
    Code      nvarchar(50)  NOT NULL,   -- value sent as receiverAddressee.address.offloadPostcode
    Name      nvarchar(200) NOT NULL,
    Country   char(2)       NOT NULL,
    City      nvarchar(120) NULL,
    Address   nvarchar(300) NULL,
    Lat       decimal(9,6)  NULL,
    Lng       decimal(9,6)  NULL,
    IsActive  bit           NOT NULL CONSTRAINT DF_ParcelLockers_IsActive DEFAULT 1,
    UpdatedAt datetime2     NOT NULL CONSTRAINT DF_ParcelLockers_UpdatedAt DEFAULT SYSUTCDATETIME()
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ParcelLockers_Provider_Code' AND object_id = OBJECT_ID(N'dbo.ParcelLockers'))
    CREATE UNIQUE INDEX UX_ParcelLockers_Provider_Code ON dbo.ParcelLockers (Provider, Code);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ParcelLockers_Country_City' AND object_id = OBJECT_ID(N'dbo.ParcelLockers'))
    CREATE INDEX IX_ParcelLockers_Country_City ON dbo.ParcelLockers (Country, City) INCLUDE (Name, IsActive);
GO

/* ---------- ShippingTariffs: price list in EUR, managed in the admin panel ---------- */
IF OBJECT_ID(N'dbo.ShippingTariffs', N'U') IS NULL
CREATE TABLE dbo.ShippingTariffs
(
    Id                 int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShippingTariffs PRIMARY KEY,
    Provider           nvarchar(20) NOT NULL CONSTRAINT DF_ShippingTariffs_Provider DEFAULT N'Omniva',
    DestinationCountry char(2)      NOT NULL,
    Size               nvarchar(10) NOT NULL,
    OmnivaPrice        decimal(9,2) NOT NULL,   -- amount invoiced by the carrier
    Markup             decimal(9,2) NOT NULL CONSTRAINT DF_ShippingTariffs_Markup DEFAULT 0,
    Price              AS (OmnivaPrice + Markup) PERSISTED,   -- amount charged to the seller
    MaxWeightKg        decimal(6,2) NULL,
    IsActive           bit          NOT NULL CONSTRAINT DF_ShippingTariffs_IsActive DEFAULT 1,
    UpdatedAt          datetime2    NOT NULL CONSTRAINT DF_ShippingTariffs_UpdatedAt DEFAULT SYSUTCDATETIME()
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ShippingTariffs_Key' AND object_id = OBJECT_ID(N'dbo.ShippingTariffs'))
    CREATE UNIQUE INDEX UX_ShippingTariffs_Key ON dbo.ShippingTariffs (Provider, DestinationCountry, Size);
GO

/* ---------- ShipmentRequests: buyer request, seller confirmation and payment ---------- */
IF OBJECT_ID(N'dbo.ShipmentRequests', N'U') IS NULL
CREATE TABLE dbo.ShipmentRequests
(
    Id             int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShipmentRequests PRIMARY KEY,
    ProductId      int NOT NULL CONSTRAINT FK_ShipmentRequests_Products REFERENCES dbo.Products (Id),
    BuyerId        int NOT NULL CONSTRAINT FK_ShipmentRequests_Buyer REFERENCES dbo.Users (Id),
    SellerId       int NOT NULL CONSTRAINT FK_ShipmentRequests_Seller REFERENCES dbo.Users (Id),
    LockerId       int NOT NULL CONSTRAINT FK_ShipmentRequests_Lockers REFERENCES dbo.ParcelLockers (Id),
    Status         nvarchar(20) NOT NULL CONSTRAINT DF_ShipmentRequests_Status DEFAULT N'Requested',

    ReceiverName   nvarchar(100) NOT NULL,
    ReceiverPhone  nvarchar(20)  NULL,
    ReceiverEmail  nvarchar(200) NULL,

    SenderName     nvarchar(100) NULL,
    SenderPhone    nvarchar(20)  NULL,
    SenderEmail    nvarchar(200) NULL,
    SenderStreet   nvarchar(120) NULL,
    SenderCity     nvarchar(80)  NULL,
    SenderPostcode nvarchar(10)  NULL,
    SenderCountry  char(2)       NULL,

    Size           nvarchar(10)  NULL,
    WeightKg       decimal(6,3)  NULL,
    Price          decimal(9,2)  NULL,      -- price quoted to the seller at confirmation
    PaymentId      int NULL CONSTRAINT FK_ShipmentRequests_Payments REFERENCES dbo.Payments (Id),

    CreatedAt      datetime2 NOT NULL CONSTRAINT DF_ShipmentRequests_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt      datetime2 NOT NULL CONSTRAINT DF_ShipmentRequests_UpdatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT CK_ShipmentRequests_Status CHECK (Status IN (N'Requested', N'Accepted', N'Paid', N'Declined', N'Cancelled'))
);
GO
/* One open request per buyer and product */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ShipmentRequests_OpenPerBuyer' AND object_id = OBJECT_ID(N'dbo.ShipmentRequests'))
    CREATE UNIQUE INDEX UX_ShipmentRequests_OpenPerBuyer ON dbo.ShipmentRequests (ProductId, BuyerId)
    WHERE Status <> N'Declined' AND Status <> N'Cancelled';
GO
/* At most one Accepted/Paid request per listing (the rule "one accepted request per product").
   Accept and payment rely on this index: a conflicting transition fails with SqlException 2601/2627,
   which the application maps to product_reserved. The predicate lists the excluded statuses instead of
   Status IN ('Accepted','Paid') because the statuses are fixed by the CHECK constraint above. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ShipmentRequests_ActiveProduct' AND object_id = OBJECT_ID(N'dbo.ShipmentRequests'))
    CREATE UNIQUE INDEX UX_ShipmentRequests_ActiveProduct ON dbo.ShipmentRequests (ProductId)
    WHERE Status <> N'Requested' AND Status <> N'Declined' AND Status <> N'Cancelled';
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ShipmentRequests_Seller' AND object_id = OBJECT_ID(N'dbo.ShipmentRequests'))
    CREATE INDEX IX_ShipmentRequests_Seller ON dbo.ShipmentRequests (SellerId, Status);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ShipmentRequests_Buyer' AND object_id = OBJECT_ID(N'dbo.ShipmentRequests'))
    CREATE INDEX IX_ShipmentRequests_Buyer ON dbo.ShipmentRequests (BuyerId, Status);
GO

/* ---------- Shipments: one carrier shipment per paid request ---------- */
IF OBJECT_ID(N'dbo.Shipments', N'U') IS NULL
CREATE TABLE dbo.Shipments
(
    Id                int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Shipments PRIMARY KEY,
    RequestId         int NOT NULL CONSTRAINT FK_Shipments_Requests REFERENCES dbo.ShipmentRequests (Id),
    Provider          nvarchar(20) NOT NULL CONSTRAINT DF_Shipments_Provider DEFAULT N'Omniva',
    PartnerShipmentId nvarchar(30) NOT NULL,   -- partnerShipmentId sent to the carrier (max 30 characters)
    Barcode           nvarchar(30) NULL,       -- assigned by the carrier after registration
    Status            nvarchar(20) NOT NULL CONSTRAINT DF_Shipments_Status DEFAULT N'Pending',
    LastEventCode     nvarchar(50) NULL,
    LastEventAt       datetime2 NULL,
    Attempts          int NOT NULL CONSTRAINT DF_Shipments_Attempts DEFAULT 0,
    ErrorCode         nvarchar(200)  NULL,
    ErrorMessage      nvarchar(1000) NULL,
    HandoverDeadline  datetime2 NULL,
    RegisteredAt      datetime2 NULL,
    DeliveredAt       datetime2 NULL,
    CreatedAt         datetime2 NOT NULL CONSTRAINT DF_Shipments_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt         datetime2 NOT NULL CONSTRAINT DF_Shipments_UpdatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT CK_Shipments_Status CHECK (Status IN (N'Pending', N'Registered', N'InTransit', N'ArrivedAtLocker', N'Delivered', N'Returned', N'Failed'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Shipments_Request' AND object_id = OBJECT_ID(N'dbo.Shipments'))
    CREATE UNIQUE INDEX UX_Shipments_Request ON dbo.Shipments (RequestId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Shipments_PartnerShipmentId' AND object_id = OBJECT_ID(N'dbo.Shipments'))
    CREATE UNIQUE INDEX UX_Shipments_PartnerShipmentId ON dbo.Shipments (Provider, PartnerShipmentId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Shipments_Barcode' AND object_id = OBJECT_ID(N'dbo.Shipments'))
    CREATE UNIQUE INDEX UX_Shipments_Barcode ON dbo.Shipments (Barcode) WHERE Barcode IS NOT NULL;
GO
/* Shipments that the status poller still has to follow */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Shipments_Status' AND object_id = OBJECT_ID(N'dbo.Shipments'))
    CREATE INDEX IX_Shipments_Status ON dbo.Shipments (Status) INCLUDE (Barcode);
GO

/* ---------- ShipmentEvents: tracking history for the buyer and seller screens ---------- */
IF OBJECT_ID(N'dbo.ShipmentEvents', N'U') IS NULL
CREATE TABLE dbo.ShipmentEvents
(
    Id              bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ShipmentEvents PRIMARY KEY,
    ShipmentId      int NOT NULL CONSTRAINT FK_ShipmentEvents_Shipments REFERENCES dbo.Shipments (Id),
    ExternalEventId bigint NOT NULL,
    EventCode       nvarchar(50) NOT NULL,
    EventAt         datetime2 NOT NULL,
    Location        nvarchar(200) NULL,
    RawJson         nvarchar(max) NULL,
    CreatedAt       datetime2 NOT NULL CONSTRAINT DF_ShipmentEvents_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ShipmentEvents_Shipment_Event' AND object_id = OBJECT_ID(N'dbo.ShipmentEvents'))
    CREATE UNIQUE INDEX UX_ShipmentEvents_Shipment_Event ON dbo.ShipmentEvents (ShipmentId, ExternalEventId);
GO

/* ---------- CarrierSyncState: cursor for event polling ---------- */
IF OBJECT_ID(N'dbo.CarrierSyncState', N'U') IS NULL
CREATE TABLE dbo.CarrierSyncState
(
    Provider    nvarchar(20) NOT NULL CONSTRAINT PK_CarrierSyncState PRIMARY KEY,
    LastEventId bigint       NOT NULL CONSTRAINT DF_CarrierSyncState_LastEventId DEFAULT 0,
    UpdatedAt   datetime2    NOT NULL CONSTRAINT DF_CarrierSyncState_UpdatedAt DEFAULT SYSUTCDATETIME()
);
GO
IF NOT EXISTS (SELECT 1 FROM dbo.CarrierSyncState WHERE Provider = N'Omniva')
    INSERT INTO dbo.CarrierSyncState (Provider, LastEventId) VALUES (N'Omniva', 0);
GO
GO

/* ---------- ShipmentRequests.AcceptedAt: start of the payment window (Shipping:AcceptPaymentHours, default 24 h) ---------- */
IF COL_LENGTH('dbo.ShipmentRequests', 'AcceptedAt') IS NULL
    ALTER TABLE dbo.ShipmentRequests ADD AcceptedAt datetime2 NULL;
GO
/* Requests accepted before this column existed: the acceptance time is approximated by UpdatedAt. */
UPDATE dbo.ShipmentRequests SET AcceptedAt = UpdatedAt WHERE Status = N'Accepted' AND AcceptedAt IS NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ShipmentRequests_AcceptedAt' AND object_id = OBJECT_ID(N'dbo.ShipmentRequests'))
    CREATE INDEX IX_ShipmentRequests_AcceptedAt ON dbo.ShipmentRequests (AcceptedAt) WHERE Status = N'Accepted';
GO

/* ---------- HIS_Products: shipping fields in the product history ---------- */
/* TR_Products_His lists the Products columns explicitly, so the new columns must be in the history table
   and in the trigger; otherwise changes of the shipping settings are not recorded. */
IF COL_LENGTH('dbo.HIS_Products', 'ShippingEnabled') IS NULL
    ALTER TABLE dbo.HIS_Products ADD ShippingEnabled bit NULL;
GO
IF COL_LENGTH('dbo.HIS_Products', 'ParcelSize') IS NULL
    ALTER TABLE dbo.HIS_Products ADD ParcelSize nvarchar(10) NULL;
GO
IF COL_LENGTH('dbo.HIS_Products', 'WeightKg') IS NULL
    ALTER TABLE dbo.HIS_Products ADD WeightKg decimal(6,3) NULL;
GO
CREATE OR ALTER TRIGGER dbo.TR_Products_His ON dbo.Products
AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL(OBJECT_ID('dbo.TR_Products_His'), 'AFTER', 'DML') > 1 RETURN;

    -- UPDATE, который менял только ViewCount, в историю не попадает
    IF EXISTS (SELECT 1 FROM deleted) AND NOT UPDATE(UpdatedAt)
       AND NOT EXISTS (
            SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
            WHERE EXISTS (
                SELECT i.Name, i.Description, i.Qty, i.Address, i.CreatedAt, i.UserId, i.AvatarUrl, i.CategoryId, i.Status,
                       i.ArchivedAt, i.PublishDurationDays, i.ExpiresAt, i.ModerationScore, i.Lat, i.Lng, i.Price,
                       i.SubscriptionBoostLevel, i.SubscriptionBoostExpiresAt, i.PaidBoostLevel, i.PaidBoostExpiresAt, i.PaidBoostPoints,
                       i.ShippingEnabled, i.ParcelSize, i.WeightKg
                EXCEPT
                SELECT d.Name, d.Description, d.Qty, d.Address, d.CreatedAt, d.UserId, d.AvatarUrl, d.CategoryId, d.Status,
                       d.ArchivedAt, d.PublishDurationDays, d.ExpiresAt, d.ModerationScore, d.Lat, d.Lng, d.Price,
                       d.SubscriptionBoostLevel, d.SubscriptionBoostExpiresAt, d.PaidBoostLevel, d.PaidBoostExpiresAt, d.PaidBoostPoints,
                       d.ShippingEnabled, d.ParcelSize, d.WeightKg))
        RETURN;

    UPDATE p SET UpdatedAt = SYSUTCDATETIME()
    FROM dbo.Products p JOIN inserted i ON i.Id = p.Id;

    DECLARE @tx bigint = CURRENT_TRANSACTION_ID();

    -- одна строка на (товар, транзакция): последнее срабатывание в транзакции содержит итоговые параметры
    DELETE h FROM dbo.HIS_Products h JOIN inserted i ON i.Id = h.ProductId WHERE h.TxId = @tx;

    INSERT INTO dbo.HIS_Products
        (ProductId, TxId, Name, Description, Qty, Address, CreatedAt, UserId, AvatarUrl, CategoryId, Status, ArchivedAt,
         ViewCount, PublishDurationDays, ExpiresAt, ModerationScore, Lat, Lng, Price,
         SubscriptionBoostLevel, SubscriptionBoostExpiresAt, PaidBoostLevel, PaidBoostExpiresAt, PaidBoostPoints, UpdatedAt,
         ShippingEnabled, ParcelSize, WeightKg, PKeys, PVals)
    SELECT p.Id, @tx, p.Name, p.Description, p.Qty, p.Address, p.CreatedAt, p.UserId, p.AvatarUrl, p.CategoryId, p.Status, p.ArchivedAt,
           p.ViewCount, p.PublishDurationDays, p.ExpiresAt, p.ModerationScore, p.Lat, p.Lng, p.Price,
           p.SubscriptionBoostLevel, p.SubscriptionBoostExpiresAt, p.PaidBoostLevel, p.PaidBoostExpiresAt, p.PaidBoostPoints, p.UpdatedAt,
           p.ShippingEnabled, p.ParcelSize, p.WeightKg, k.PKeys, k.PVals
    FROM dbo.Products p
    JOIN inserted i ON i.Id = p.Id
    OUTER APPLY (
        SELECT '[' + STRING_AGG(CAST(m.ParamId AS nvarchar(max)), ',') WITHIN GROUP (ORDER BY m.ParamId) + ']' AS PKeys,
               '[' + STRING_AGG(CAST('"' + STRING_ESCAPE(ISNULL(m.Value, N''), 'json') + '"' AS nvarchar(max)), ',') WITHIN GROUP (ORDER BY m.ParamId) + ']' AS PVals
        FROM dbo.MapperProductParam m WHERE m.ProductId = p.Id
    ) k;
END
GO

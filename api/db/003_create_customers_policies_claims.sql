-- 003: Customers, Policies, Claims and the Claim-number sequence (ADR 0004). Idempotent.
IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customers
    (
        Id        bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
        FullName  nvarchar(200) NOT NULL,
        Email     nvarchar(256) NOT NULL,
        CreatedAt datetime2(3)  NOT NULL CONSTRAINT DF_Customers_CreatedAt DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF OBJECT_ID(N'dbo.Policies', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Policies
    (
        Id            bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Policies PRIMARY KEY,
        PolicyNumber  nvarchar(30)  NOT NULL,
        CustomerId    bigint        NOT NULL CONSTRAINT FK_Policies_Customers REFERENCES dbo.Customers (Id),
        PlateNumber   nvarchar(20)  NOT NULL,
        VehicleMake   nvarchar(100) NOT NULL,
        VehicleModel  nvarchar(100) NOT NULL,
        CoverageLimit decimal(18,2) NOT NULL,   -- Coverage Limit: max payout per Claim
        CreatedAt     datetime2(3)  NOT NULL CONSTRAINT DF_Policies_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_Policies_PolicyNumber UNIQUE (PolicyNumber),
        CONSTRAINT CK_Policies_CoverageLimit CHECK (CoverageLimit > 0)
    );
    CREATE INDEX IX_Policies_PlateNumber ON dbo.Policies (PlateNumber);
    CREATE INDEX IX_Policies_CustomerId ON dbo.Policies (CustomerId);
END
GO

-- Source of the numeric part of CLM-yyyy-000001. Gaps are possible (sequences are not transactional); repeats are not.
IF OBJECT_ID(N'dbo.ClaimNumberSeq', N'SO') IS NULL
    CREATE SEQUENCE dbo.ClaimNumberSeq AS bigint START WITH 1 INCREMENT BY 1 NO CYCLE;
GO

IF OBJECT_ID(N'dbo.Claims', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Claims
    (
        Id                bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Claims PRIMARY KEY,
        ClaimNumber       nvarchar(20)   NOT NULL,
        PolicyId          bigint         NOT NULL CONSTRAINT FK_Claims_Policies REFERENCES dbo.Policies (Id),
        Status            nvarchar(30)   NOT NULL,
        VehiclePlate      nvarchar(20)   NULL,
        VehicleMake       nvarchar(100)  NULL,
        VehicleModel      nvarchar(100)  NULL,
        CustomerEmail     nvarchar(256)  NULL,
        DamageDescription nvarchar(2000) NULL,
        CreatedByUserId   bigint         NOT NULL CONSTRAINT FK_Claims_Users REFERENCES dbo.Users (Id),
        CreatedAt         datetime2(3)   NOT NULL,
        UpdatedAt         datetime2(3)   NOT NULL,
        RowVersion        rowversion     NOT NULL,   -- optimistic concurrency token (enforced from ticket 12)
        CONSTRAINT UQ_Claims_ClaimNumber UNIQUE (ClaimNumber),
        CONSTRAINT CK_Claims_Status CHECK (Status IN (N'Draft', N'Submitted', N'UnderReview', N'ChangesRequested',
            N'Approved', N'Rejected', N'InRepair', N'Repaired', N'InvoiceSubmitted', N'Paid', N'Cancelled'))
    );
    CREATE INDEX IX_Claims_Status ON dbo.Claims (Status);
    CREATE INDEX IX_Claims_PolicyId ON dbo.Claims (PolicyId);
END
GO

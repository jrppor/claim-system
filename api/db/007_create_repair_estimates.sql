-- 007: Repair Estimates (every revision kept) and their parts/labour lines. Idempotent.
-- A revision is only ever inserted; the latest Revision of a Claim is the one under review.
-- EstimatedAmount is stored as the server-computed sum of the line totals, never taken from a client.
IF OBJECT_ID(N'dbo.RepairEstimates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RepairEstimates
    (
        Id                bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RepairEstimates PRIMARY KEY,
        ClaimId           bigint        NOT NULL CONSTRAINT FK_RepairEstimates_Claims REFERENCES dbo.Claims (Id),
        Revision          int           NOT NULL,
        EstimatedAmount   decimal(18,2) NOT NULL,
        SubmittedByUserId bigint        NOT NULL CONSTRAINT FK_RepairEstimates_Users REFERENCES dbo.Users (Id),
        SubmittedAt       datetime2(3)  NOT NULL,
        CONSTRAINT UQ_RepairEstimates_ClaimRevision UNIQUE (ClaimId, Revision),
        CONSTRAINT CK_RepairEstimates_Revision CHECK (Revision >= 1),
        CONSTRAINT CK_RepairEstimates_Amount CHECK (EstimatedAmount >= 0)
    );
END
GO

IF OBJECT_ID(N'dbo.RepairEstimateLines', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RepairEstimateLines
    (
        Id               bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RepairEstimateLines PRIMARY KEY,
        RepairEstimateId bigint         NOT NULL CONSTRAINT FK_RepairEstimateLines_RepairEstimates REFERENCES dbo.RepairEstimates (Id),
        LineNumber       int            NOT NULL,
        Kind             nvarchar(10)   NOT NULL,   -- Part | Labour
        Description      nvarchar(200)  NOT NULL,
        Quantity         decimal(9,2)   NOT NULL,
        UnitPrice        decimal(18,2)  NOT NULL,
        LineTotal        decimal(18,2)  NOT NULL,
        CONSTRAINT UQ_RepairEstimateLines_Number UNIQUE (RepairEstimateId, LineNumber),
        CONSTRAINT CK_RepairEstimateLines_Kind CHECK (Kind IN (N'Part', N'Labour')),
        CONSTRAINT CK_RepairEstimateLines_Quantity CHECK (Quantity > 0),
        CONSTRAINT CK_RepairEstimateLines_UnitPrice CHECK (UnitPrice >= 0)
    );
END
GO

-- Revisions are history: once written they may not be changed or removed.
IF OBJECT_ID(N'dbo.TR_RepairEstimates_AppendOnly', N'TR') IS NULL
    EXEC(N'CREATE TRIGGER dbo.TR_RepairEstimates_AppendOnly ON dbo.RepairEstimates INSTEAD OF UPDATE, DELETE AS
           BEGIN
               RAISERROR(N''RepairEstimates is append-only.'', 16, 1);
               ROLLBACK TRANSACTION;
           END');
GO

IF OBJECT_ID(N'dbo.TR_RepairEstimateLines_AppendOnly', N'TR') IS NULL
    EXEC(N'CREATE TRIGGER dbo.TR_RepairEstimateLines_AppendOnly ON dbo.RepairEstimateLines INSTEAD OF UPDATE, DELETE AS
           BEGIN
               RAISERROR(N''RepairEstimateLines is append-only.'', 16, 1);
               ROLLBACK TRANSACTION;
           END');
GO

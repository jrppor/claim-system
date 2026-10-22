-- 009: Invoices, the garage's bill for the actual repair cost, one per Claim. Idempotent.
-- An Invoice is only ever inserted (append-only); the Approved Amount stays on Claims so the two can be compared.
IF OBJECT_ID(N'dbo.Invoices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Invoices
    (
        Id                bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Invoices PRIMARY KEY,
        ClaimId           bigint         NOT NULL CONSTRAINT FK_Invoices_Claims REFERENCES dbo.Claims (Id),
        ActualAmount      decimal(18,2)  NOT NULL,
        Note              nvarchar(2000) NULL,
        SubmittedByUserId bigint         NOT NULL CONSTRAINT FK_Invoices_Users REFERENCES dbo.Users (Id),
        SubmittedAt       datetime2(3)   NOT NULL,
        CONSTRAINT UQ_Invoices_Claim UNIQUE (ClaimId),
        CONSTRAINT CK_Invoices_Amount CHECK (ActualAmount > 0)
    );
END
GO

IF OBJECT_ID(N'dbo.TR_Invoices_AppendOnly', N'TR') IS NULL
    EXEC(N'CREATE TRIGGER dbo.TR_Invoices_AppendOnly ON dbo.Invoices INSTEAD OF UPDATE, DELETE AS
           BEGIN
               RAISERROR(N''Invoices is append-only.'', 16, 1);
               ROLLBACK TRANSACTION;
           END');
GO

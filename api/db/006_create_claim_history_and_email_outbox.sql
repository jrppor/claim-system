-- 006: ClaimHistory (append-only) and EmailOutbox (ADR 0002). Idempotent.
IF OBJECT_ID(N'dbo.ClaimHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ClaimHistory
    (
        Id          bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClaimHistory PRIMARY KEY,
        ClaimId     bigint        NOT NULL CONSTRAINT FK_ClaimHistory_Claims REFERENCES dbo.Claims (Id),
        FromStatus  nvarchar(30)  NULL,             -- NULL only for a creation line, if one is ever written
        ToStatus    nvarchar(30)  NOT NULL,
        ActorUserId bigint        NOT NULL CONSTRAINT FK_ClaimHistory_Users REFERENCES dbo.Users (Id),
        OccurredAt  datetime2(3)  NOT NULL,
        Reason      nvarchar(2000) NULL
    );
    CREATE INDEX IX_ClaimHistory_ClaimId ON dbo.ClaimHistory (ClaimId, Id);
END
GO

-- Claim History is append-only: no row may be changed or removed once written.
IF OBJECT_ID(N'dbo.TR_ClaimHistory_AppendOnly', N'TR') IS NULL
    EXEC(N'CREATE TRIGGER dbo.TR_ClaimHistory_AppendOnly ON dbo.ClaimHistory INSTEAD OF UPDATE, DELETE AS
           BEGIN
               RAISERROR(N''ClaimHistory is append-only.'', 16, 1);
               ROLLBACK TRANSACTION;
           END');
GO

IF OBJECT_ID(N'dbo.EmailOutbox', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailOutbox
    (
        Id            bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmailOutbox PRIMARY KEY,
        ClaimId       bigint         NOT NULL CONSTRAINT FK_EmailOutbox_Claims REFERENCES dbo.Claims (Id),
        ToAddress     nvarchar(256)  NOT NULL,
        Subject       nvarchar(300)  NOT NULL,
        Body          nvarchar(max)  NOT NULL,
        CreatedAt     datetime2(3)   NOT NULL,
        Attempts      int            NOT NULL CONSTRAINT DF_EmailOutbox_Attempts DEFAULT (0),
        NextAttemptAt datetime2(3)   NOT NULL,      -- not before this time; pushed out after each failure (backoff)
        SentAt        datetime2(3)   NULL,          -- NULL = still to send
        LastError     nvarchar(2000) NULL
    );
    CREATE INDEX IX_EmailOutbox_Pending ON dbo.EmailOutbox (NextAttemptAt) WHERE SentAt IS NULL;
END
GO

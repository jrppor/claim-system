-- 011: ClaimPhotos (ADR 0006). Metadata only; the bytes live in file storage under StoredName. Idempotent.
IF OBJECT_ID(N'dbo.ClaimPhotos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ClaimPhotos
    (
        Id               bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClaimPhotos PRIMARY KEY,
        ClaimId          bigint         NOT NULL CONSTRAINT FK_ClaimPhotos_Claims REFERENCES dbo.Claims (Id),
        OriginalFileName nvarchar(255)  NOT NULL,   -- shown to people only, never used as a path
        StoredName       nvarchar(100)  NOT NULL,   -- GUID + extension; the key in file storage
        ContentType      nvarchar(50)   NOT NULL,   -- detected from the file's bytes at upload
        SizeBytes        bigint         NOT NULL,
        Caption          nvarchar(200)  NULL,
        UploadedByUserId bigint         NOT NULL CONSTRAINT FK_ClaimPhotos_Users REFERENCES dbo.Users (Id),
        UploadedAt       datetime2(3)   NOT NULL,
        CONSTRAINT UQ_ClaimPhotos_StoredName UNIQUE (StoredName)
    );
    CREATE INDEX IX_ClaimPhotos_ClaimId ON dbo.ClaimPhotos (ClaimId, Id);
END
GO

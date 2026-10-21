-- 001: Users (ADR 0004). Runs against database [Claim]. Idempotent.
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id           bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        Email        nvarchar(256)  NOT NULL,
        PasswordHash nvarchar(512)  NOT NULL,
        DisplayName  nvarchar(200)  NOT NULL,
        Role         nvarchar(30)   NOT NULL,
        IsActive     bit            NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
        CreatedAt    datetime2(3)   NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_Users_Email UNIQUE (Email),
        CONSTRAINT CK_Users_Role CHECK (Role IN (N'GarageStaff', N'InsuranceAdjuster', N'Admin'))
    );
END
GO

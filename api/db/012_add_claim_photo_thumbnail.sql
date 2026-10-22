-- 012: key of each Claim Photo's thumbnail in file storage (ADR 0006). NULL for photos stored before thumbnails existed. Idempotent.
IF COL_LENGTH(N'dbo.ClaimPhotos', N'ThumbnailStoredName') IS NULL
    ALTER TABLE dbo.ClaimPhotos ADD ThumbnailStoredName nvarchar(100) NULL;
GO

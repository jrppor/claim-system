using Claim.Application.Claims;
using Claim.Domain.Claims;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Claim.Infrastructure.Claims;

public sealed class SqlClaimPhotoRepository(string connectionString) : IClaimPhotoRepository
{
    private sealed record Row(long Id, long ClaimId, string OriginalFileName, string StoredName, string? ThumbnailStoredName, string ContentType,
        long SizeBytes, string? Caption, long UploadedByUserId, DateTime UploadedAt);

    public async Task<IReadOnlyList<ClaimPhoto>> ListAsync(long claimId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition(
            """
            SELECT Id, ClaimId, OriginalFileName, StoredName, ThumbnailStoredName, ContentType, SizeBytes, Caption, UploadedByUserId, UploadedAt
            FROM dbo.ClaimPhotos WHERE ClaimId = @claimId ORDER BY Id
            """, new { claimId }, cancellationToken: ct));
        return rows.Select(r => new ClaimPhoto(r.ClaimId, r.OriginalFileName, r.StoredName, r.ContentType, r.SizeBytes, r.Caption, r.UploadedByUserId, r.UploadedAt)
            { Id = r.Id, ThumbnailStoredName = r.ThumbnailStoredName }).ToList();
    }
}

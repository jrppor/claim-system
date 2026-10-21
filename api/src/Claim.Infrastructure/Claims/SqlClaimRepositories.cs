using Claim.Application.Claims;
using Claim.Domain.Claims;
using Dapper;
using Microsoft.Data.SqlClient;
using ClaimEntity = Claim.Domain.Claims.Claim;

namespace Claim.Infrastructure.Claims;

public sealed class SqlPolicyRepository(string connectionString) : IPolicyRepository
{
    private const string Select = """
        SELECT p.Id, p.PolicyNumber, p.PlateNumber, p.VehicleMake, p.VehicleModel, p.CoverageLimit,
               c.Id AS CustomerId, c.FullName, c.Email
        FROM dbo.Policies p JOIN dbo.Customers c ON c.Id = p.CustomerId
        """;

    private sealed record Row(long Id, string PolicyNumber, string PlateNumber, string VehicleMake, string VehicleModel,
        decimal CoverageLimit, long CustomerId, string FullName, string Email)
    {
        public Policy ToPolicy() => new(Id, PolicyNumber, new Customer(CustomerId, FullName, Email),
            PlateNumber, VehicleMake, VehicleModel, CoverageLimit);
    }

    public async Task<IReadOnlyList<Policy>> SearchAsync(string text, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        var pattern = "%" + text.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
        var rows = await conn.QueryAsync<Row>(new CommandDefinition(
            $"{Select} WHERE p.PolicyNumber LIKE @pattern OR p.PlateNumber LIKE @pattern ORDER BY p.PolicyNumber",
            new { pattern }, cancellationToken: ct));
        return rows.Select(r => r.ToPolicy()).Take(50).ToList();
    }

    public async Task<Policy?> FindByIdAsync(long id, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            $"{Select} WHERE p.Id = @id", new { id }, cancellationToken: ct));
        return row?.ToPolicy();
    }
}

public sealed class SqlClaimRepository(string connectionString) : IClaimRepository
{
    private sealed class Row
    {
        public long Id { get; set; }
        public string ClaimNumber { get; set; } = "";
        public long PolicyId { get; set; }
        public string Status { get; set; } = "";
        public string? VehiclePlate { get; set; }
        public string? VehicleMake { get; set; }
        public string? VehicleModel { get; set; }
        public string? CustomerEmail { get; set; }
        public string? DamageDescription { get; set; }
        public long CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];

        public ClaimEntity ToClaim() =>
            new(Enum.Parse<ClaimStatus>(Status), new ClaimDetails(VehiclePlate, VehicleMake, VehicleModel, CustomerEmail, DamageDescription), UpdatedAt)
            {
                Id = Id, ClaimNumber = ClaimNumber, PolicyId = PolicyId, CreatedByUserId = CreatedByUserId,
                CreatedAt = CreatedAt, RowVersion = RowVersion,
            };
    }

    public async Task<long> NextClaimSequenceAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT NEXT VALUE FOR dbo.ClaimNumberSeq", cancellationToken: ct));
    }

    public async Task<long> AddAsync(ClaimEntity claim, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition("""
            INSERT INTO dbo.Claims (ClaimNumber, PolicyId, Status, VehiclePlate, VehicleMake, VehicleModel,
                                    CustomerEmail, DamageDescription, CreatedByUserId, CreatedAt, UpdatedAt)
            OUTPUT INSERTED.Id
            VALUES (@ClaimNumber, @PolicyId, @Status, @VehiclePlate, @VehicleMake, @VehicleModel,
                    @CustomerEmail, @DamageDescription, @CreatedByUserId, @CreatedAt, @UpdatedAt)
            """,
            new
            {
                claim.ClaimNumber, claim.PolicyId, Status = claim.Status.ToString(),
                claim.Details.VehiclePlate, claim.Details.VehicleMake, claim.Details.VehicleModel,
                claim.Details.CustomerEmail, claim.Details.DamageDescription,
                claim.CreatedByUserId, claim.CreatedAt, claim.UpdatedAt,
            }, cancellationToken: ct));
    }

    public async Task<ClaimEntity?> FindByIdAsync(long id, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            "SELECT * FROM dbo.Claims WHERE Id = @id", new { id }, cancellationToken: ct));
        return row?.ToClaim();
    }

    public async Task UpdateAsync(ClaimEntity claim, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.Claims
            SET Status = @Status, VehiclePlate = @VehiclePlate, VehicleMake = @VehicleMake, VehicleModel = @VehicleModel,
                CustomerEmail = @CustomerEmail, DamageDescription = @DamageDescription, UpdatedAt = @UpdatedAt
            WHERE Id = @Id
            """,
            new
            {
                claim.Id, Status = claim.Status.ToString(),
                claim.Details.VehiclePlate, claim.Details.VehicleMake, claim.Details.VehicleModel,
                claim.Details.CustomerEmail, claim.Details.DamageDescription, claim.UpdatedAt,
            }, cancellationToken: ct));
    }
}

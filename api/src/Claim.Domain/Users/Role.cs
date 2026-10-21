namespace Claim.Domain.Users;

/// <summary>Role names as stored in Users.Role and carried in the JWT role claim.</summary>
public static class Role
{
    public const string GarageStaff = "GarageStaff";
    public const string InsuranceAdjuster = "InsuranceAdjuster";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [GarageStaff, InsuranceAdjuster, Admin];
}

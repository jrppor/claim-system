namespace Claim.Domain.Claims;

/// <summary>Reference data a Claim points at: the insured vehicle and the Coverage Limit per Claim.</summary>
public sealed record Policy(
    long Id,
    string PolicyNumber,
    Customer Customer,
    string PlateNumber,
    string VehicleMake,
    string VehicleModel,
    decimal CoverageLimit);

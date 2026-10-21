-- 005: Seed mock Customers and Policies (with insured vehicles). DEMO ONLY. Idempotent.
-- Customers are keyed by email, Policies by PolicyNumber.
INSERT INTO dbo.Customers (FullName, Email)
SELECT v.FullName, v.Email
FROM (VALUES
    (N'สมศักดิ์ ใจดี',     N'somsak@customer.local'),
    (N'มาลี รักษาธรรม',    N'malee@customer.local'),
    (N'วิชัย เจริญสุข',    N'wichai@customer.local'),
    (N'นภา ศรีสวัสดิ์',    N'napa@customer.local')
) AS v (FullName, Email)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Customers c WHERE c.Email = v.Email);
GO

INSERT INTO dbo.Policies (PolicyNumber, CustomerId, PlateNumber, VehicleMake, VehicleModel, CoverageLimit)
SELECT v.PolicyNumber, c.Id, v.PlateNumber, v.VehicleMake, v.VehicleModel, v.CoverageLimit
FROM (VALUES
    (N'POL-2026-0001', N'somsak@customer.local', N'1กก 1234', N'Toyota', N'Camry',  500000.00),
    (N'POL-2026-0002', N'malee@customer.local',  N'2ขข 5678', N'Honda',  N'City',   300000.00),
    (N'POL-2026-0003', N'wichai@customer.local', N'3คค 9012', N'Mazda',  N'CX-5',   800000.00),
    (N'POL-2026-0004', N'napa@customer.local',   N'4งง 3456', N'Isuzu',  N'D-Max',  400000.00),
    (N'POL-2026-0005', N'somsak@customer.local', N'5จจ 7890', N'Nissan', N'Almera', 250000.00)
) AS v (PolicyNumber, CustomerEmail, PlateNumber, VehicleMake, VehicleModel, CoverageLimit)
JOIN dbo.Customers c ON c.Email = v.CustomerEmail
WHERE NOT EXISTS (SELECT 1 FROM dbo.Policies p WHERE p.PolicyNumber = v.PolicyNumber);
GO

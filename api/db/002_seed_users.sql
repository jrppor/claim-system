-- 002: Seed one demo user per role. Idempotent (skips emails that already exist).
-- DEMO ONLY. Passwords (hashed below with PBKDF2-HMAC-SHA256, 100k iterations):
--   staff@claim.local     / Garage123!    (GarageStaff)
--   adjuster@claim.local  / Adjuster123!  (InsuranceAdjuster)
--   admin@claim.local     / Admin123!     (Admin)
INSERT INTO dbo.Users (Email, PasswordHash, DisplayName, Role)
SELECT v.Email, v.PasswordHash, v.DisplayName, v.Role
FROM (VALUES
    (N'staff@claim.local',    N'PBKDF2$SHA256$100000$2XFHd9zzegA8/z9YQUW+0Q==$E4G8P55yfeFnVexu9sKKDVguzv8bJt82VAs3N2cY8HM=', N'สมชาย อู่ซ่อม',     N'GarageStaff'),
    (N'adjuster@claim.local', N'PBKDF2$SHA256$100000$JT2b9oUcfnhpkvm8/m0zaQ==$9tS/v7LV8yANm1wFh/cjS1A5Uq5oDj/Ryft9wuse5tM=', N'สมหญิง ประกันภัย', N'InsuranceAdjuster'),
    (N'admin@claim.local',    N'PBKDF2$SHA256$100000$vhOc5D+p08r6WxJHmxR+8Q==$cMF3izpBDcfeB5IxqMA22Dw6qFoLodCV5VCJ8kRhDu8=', N'ผู้ดูแลระบบ',       N'Admin')
) AS v (Email, PasswordHash, DisplayName, Role)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Users u WHERE u.Email = v.Email);
GO

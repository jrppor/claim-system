-- 010: Seed demo Claims in every stage of the lifecycle, with consistent Claim History, Repair Estimates and Invoices. DEMO ONLY.
-- Idempotent: a demo Claim (ClaimNumber CLM-DEMO-nnn) is only inserted, with all its rows, when it does not exist yet.
-- No EmailOutbox rows are seeded (nothing should be mailed for demo data). Needs 002 (users) and 005 (policies).
-- Actors: S = staff@claim.local (Garage Staff), A = adjuster@claim.local (Insurance Adjuster).
SET NOCOUNT ON;

CREATE TABLE #C (ClaimNumber nvarchar(20) PRIMARY KEY, PolicyNumber nvarchar(30), Status nvarchar(30), Damage nvarchar(2000),
                 DaysAgo int, ApprovedAmount decimal(18,2) NULL, ApprovedRevision int NULL);
CREATE TABLE #H (ClaimNumber nvarchar(20), Seq int, FromStatus nvarchar(30), ToStatus nvarchar(30), Actor char(1), MinutesAfter int, Reason nvarchar(2000) NULL);
CREATE TABLE #E (ClaimNumber nvarchar(20), Revision int, MinutesAfter int);
CREATE TABLE #L (ClaimNumber nvarchar(20), Revision int, LineNumber int, Kind nvarchar(10), Description nvarchar(200), Quantity decimal(9,2), UnitPrice decimal(18,2));
CREATE TABLE #I (ClaimNumber nvarchar(20), ActualAmount decimal(18,2), Note nvarchar(2000) NULL, MinutesAfter int);

INSERT INTO #C VALUES
    (N'CLM-DEMO-001', N'POL-2026-0001', N'Draft',            N'ชนท้ายที่สี่แยก กันชนหลังแตก ยังไม่ได้ถ่ายรูปเพิ่ม', 1,  NULL,     NULL),
    (N'CLM-DEMO-002', N'POL-2026-0002', N'Submitted',        N'ถูกเฉี่ยวขณะจอดในห้าง ประตูซ้ายบุบ',                 2,  NULL,     NULL),
    (N'CLM-DEMO-003', N'POL-2026-0003', N'UnderReview',      N'ชนเสาที่จอดรถ ไฟหน้าขวาและกันชนหน้าเสียหาย',          3,  NULL,     NULL),
    (N'CLM-DEMO-004', N'POL-2026-0004', N'ChangesRequested', N'กระจกหน้าร้าวจากหินกระเด็น',                        4,  NULL,     NULL),
    (N'CLM-DEMO-005', N'POL-2026-0001', N'Approved',         N'ชนด้านข้างขณะเปลี่ยนเลน ประตูและบังโคลนหน้าซ้ายเสียหาย', 8,  42000.00, 2),
    (N'CLM-DEMO-006', N'POL-2026-0002', N'InRepair',         N'น้ำท่วมขังใต้ท้องรถ ต้องเปลี่ยนชิ้นส่วนช่วงล่าง',      12, 28500.00, 1),
    (N'CLM-DEMO-007', N'POL-2026-0003', N'InvoiceSubmitted', N'ถูกชนท้ายที่ไฟแดง กันชนหลังและฝากระโปรงท้ายเสียหาย',    18, 36000.00, 1),
    (N'CLM-DEMO-008', N'POL-2026-0005', N'Paid',             N'ชนสุนัขข้ามถนน กันชนหน้าและกระจังหน้าแตก',            30, 15000.00, 1),
    (N'CLM-DEMO-009', N'POL-2026-0004', N'Rejected',         N'รอยขีดข่วนเก่าที่มีอยู่ก่อนทำประกัน',                  15, NULL,     NULL),
    (N'CLM-DEMO-010', N'POL-2026-0005', N'Cancelled',        N'บันทึกเคลมซ้ำโดยไม่ตั้งใจ',                           6,  NULL,     NULL);

INSERT INTO #H VALUES
    -- 002 Submitted
    (N'CLM-DEMO-002', 1, N'Draft', N'Submitted', 'S', 10, NULL),
    -- 003 UnderReview
    (N'CLM-DEMO-003', 1, N'Draft', N'Submitted', 'S', 10, NULL),
    (N'CLM-DEMO-003', 2, N'Submitted', N'UnderReview', 'A', 120, NULL),
    -- 004 ChangesRequested
    (N'CLM-DEMO-004', 1, N'Draft', N'Submitted', 'S', 10, NULL),
    (N'CLM-DEMO-004', 2, N'Submitted', N'UnderReview', 'A', 90, NULL),
    (N'CLM-DEMO-004', 3, N'UnderReview', N'ChangesRequested', 'A', 150, N'กรุณาแนบรายการค่าแรงแยกจากค่าอะไหล่'),
    -- 005 Approved (one revision after changes were requested)
    (N'CLM-DEMO-005', 1, N'Draft', N'Submitted', 'S', 10, NULL),
    (N'CLM-DEMO-005', 2, N'Submitted', N'UnderReview', 'A', 60, NULL),
    (N'CLM-DEMO-005', 3, N'UnderReview', N'ChangesRequested', 'A', 120, N'ราคาอะไหล่สูงกว่าราคากลาง กรุณาตรวจสอบอีกครั้ง'),
    (N'CLM-DEMO-005', 4, N'ChangesRequested', N'UnderReview', 'S', 400, N'ส่ง Repair Estimate ฉบับแก้ไขใหม่'),
    (N'CLM-DEMO-005', 5, N'UnderReview', N'Approved', 'A', 520, N'อนุมัติตามราคาที่แก้ไขแล้ว'),
    -- 006 InRepair
    (N'CLM-DEMO-006', 1, N'Draft', N'Submitted', 'S', 10, NULL),
    (N'CLM-DEMO-006', 2, N'Submitted', N'UnderReview', 'A', 60, NULL),
    (N'CLM-DEMO-006', 3, N'UnderReview', N'Approved', 'A', 180, N'อนุมัติตามใบประเมิน'),
    (N'CLM-DEMO-006', 4, N'Approved', N'InRepair', 'S', 1500, NULL),
    -- 007 InvoiceSubmitted
    (N'CLM-DEMO-007', 1, N'Draft', N'Submitted', 'S', 10, NULL),
    (N'CLM-DEMO-007', 2, N'Submitted', N'UnderReview', 'A', 60, NULL),
    (N'CLM-DEMO-007', 3, N'UnderReview', N'Approved', 'A', 180, N'อนุมัติตามใบประเมิน'),
    (N'CLM-DEMO-007', 4, N'Approved', N'InRepair', 'S', 1500, NULL),
    (N'CLM-DEMO-007', 5, N'InRepair', N'Repaired', 'S', 5000, NULL),
    (N'CLM-DEMO-007', 6, N'Repaired', N'InvoiceSubmitted', 'S', 5200, N'ค่าซ่อมจริงสูงกว่าที่อนุมัติเล็กน้อย'),
    -- 008 Paid
    (N'CLM-DEMO-008', 1, N'Draft', N'Submitted', 'S', 10, NULL),
    (N'CLM-DEMO-008', 2, N'Submitted', N'UnderReview', 'A', 60, NULL),
    (N'CLM-DEMO-008', 3, N'UnderReview', N'Approved', 'A', 180, N'อนุมัติตามใบประเมิน'),
    (N'CLM-DEMO-008', 4, N'Approved', N'InRepair', 'S', 1200, NULL),
    (N'CLM-DEMO-008', 5, N'InRepair', N'Repaired', 'S', 4000, NULL),
    (N'CLM-DEMO-008', 6, N'Repaired', N'InvoiceSubmitted', 'S', 4100, NULL),
    (N'CLM-DEMO-008', 7, N'InvoiceSubmitted', N'Paid', 'A', 6000, N'โอนเงินให้อู่แล้ว'),
    -- 009 Rejected
    (N'CLM-DEMO-009', 1, N'Draft', N'Submitted', 'S', 10, NULL),
    (N'CLM-DEMO-009', 2, N'Submitted', N'UnderReview', 'A', 60, NULL),
    (N'CLM-DEMO-009', 3, N'UnderReview', N'Rejected', 'A', 100, N'ความเสียหายเกิดก่อนวันเริ่มคุ้มครองตามกรมธรรม์'),
    -- 010 Cancelled
    (N'CLM-DEMO-010', 1, N'Draft', N'Submitted', 'S', 10, NULL),
    (N'CLM-DEMO-010', 2, N'Submitted', N'Cancelled', 'S', 30, N'สร้างเคลมซ้ำ');

-- Repair Estimates: a revision exists from the moment the Claim was Submitted (and a second one for 005 after changes were requested).
INSERT INTO #E VALUES
    (N'CLM-DEMO-002', 1, 20), (N'CLM-DEMO-003', 1, 20), (N'CLM-DEMO-004', 1, 20),
    (N'CLM-DEMO-005', 1, 20), (N'CLM-DEMO-005', 2, 400),
    (N'CLM-DEMO-006', 1, 20), (N'CLM-DEMO-007', 1, 20), (N'CLM-DEMO-008', 1, 20),
    (N'CLM-DEMO-009', 1, 20);

INSERT INTO #L VALUES
    (N'CLM-DEMO-002', 1, 1, N'Part',   N'บานประตูซ้ายหน้า', 1, 14000.00),
    (N'CLM-DEMO-002', 1, 2, N'Labour', N'ค่าแรงเปลี่ยนและทำสีประตู', 1, 6500.00),
    (N'CLM-DEMO-003', 1, 1, N'Part',   N'ไฟหน้าขวา', 1, 12500.00),
    (N'CLM-DEMO-003', 1, 2, N'Part',   N'กันชนหน้า', 1, 9800.00),
    (N'CLM-DEMO-003', 1, 3, N'Labour', N'ค่าแรงเปลี่ยนไฟหน้าและกันชน', 4, 700.00),
    (N'CLM-DEMO-004', 1, 1, N'Part',   N'กระจกบังลมหน้า', 1, 9500.00),
    (N'CLM-DEMO-005', 1, 1, N'Part',   N'ประตูหน้าซ้ายและบังโคลน', 1, 52000.00),
    (N'CLM-DEMO-005', 1, 2, N'Labour', N'ค่าแรงเปลี่ยนและทำสี', 1, 9000.00),
    (N'CLM-DEMO-005', 2, 1, N'Part',   N'ประตูหน้าซ้าย', 1, 21000.00),
    (N'CLM-DEMO-005', 2, 2, N'Part',   N'บังโคลนหน้าซ้าย', 1, 12000.00),
    (N'CLM-DEMO-005', 2, 3, N'Labour', N'ค่าแรงเปลี่ยนและทำสี', 1, 9000.00),
    (N'CLM-DEMO-006', 1, 1, N'Part',   N'โช้คอัพหน้า', 2, 6500.00),
    (N'CLM-DEMO-006', 1, 2, N'Part',   N'ลูกหมากปีกนก', 2, 2750.00),
    (N'CLM-DEMO-006', 1, 3, N'Labour', N'ค่าแรงเปลี่ยนช่วงล่าง', 5, 2000.00),
    (N'CLM-DEMO-007', 1, 1, N'Part',   N'กันชนหลัง', 1, 15000.00),
    (N'CLM-DEMO-007', 1, 2, N'Part',   N'ฝากระโปรงท้าย', 1, 16000.00),
    (N'CLM-DEMO-007', 1, 3, N'Labour', N'ค่าแรงและทำสี', 1, 5000.00),
    (N'CLM-DEMO-008', 1, 1, N'Part',   N'กันชนหน้า', 1, 8000.00),
    (N'CLM-DEMO-008', 1, 2, N'Part',   N'กระจังหน้า', 1, 4000.00),
    (N'CLM-DEMO-008', 1, 3, N'Labour', N'ค่าแรงเปลี่ยนกันชนและกระจังหน้า', 3, 1000.00),
    (N'CLM-DEMO-009', 1, 1, N'Labour', N'ขัดสีรอยขีดข่วน', 1, 3500.00);

INSERT INTO #I VALUES
    (N'CLM-DEMO-007', 36800.00, N'ค่าซ่อมจริงสูงกว่าที่อนุมัติเล็กน้อย', 5200),
    (N'CLM-DEMO-008', 15000.00, NULL, 4100);

DECLARE @staff bigint = (SELECT Id FROM dbo.Users WHERE Email = N'staff@claim.local');
DECLARE @adjuster bigint = (SELECT Id FROM dbo.Users WHERE Email = N'adjuster@claim.local');
IF @staff IS NULL OR @adjuster IS NULL THROW 50000, N'Demo users are missing: run 002_seed_users.sql first.', 1;

-- Only Claims that do not exist yet are seeded; every dependent row below is limited to these.
SELECT c.ClaimNumber INTO #New FROM #C c WHERE NOT EXISTS (SELECT 1 FROM dbo.Claims x WHERE x.ClaimNumber = c.ClaimNumber);
IF NOT EXISTS (SELECT 1 FROM #New) PRINT N'Demo Claims already present.';

BEGIN TRANSACTION;

INSERT INTO dbo.Claims (ClaimNumber, PolicyId, Status, VehiclePlate, VehicleMake, VehicleModel, CustomerEmail, DamageDescription,
                        CreatedByUserId, CreatedAt, UpdatedAt)
SELECT c.ClaimNumber, p.Id, c.Status, p.PlateNumber, p.VehicleMake, p.VehicleModel, cu.Email, c.Damage,
       @staff, DATEADD(DAY, -c.DaysAgo, SYSUTCDATETIME()), DATEADD(DAY, -c.DaysAgo, SYSUTCDATETIME())
FROM #C c
JOIN #New n ON n.ClaimNumber = c.ClaimNumber
JOIN dbo.Policies p ON p.PolicyNumber = c.PolicyNumber
JOIN dbo.Customers cu ON cu.Id = p.CustomerId;

-- ORDER BY makes the identity (and so the History order) follow the lifecycle sequence.
INSERT INTO dbo.ClaimHistory (ClaimId, FromStatus, ToStatus, ActorUserId, OccurredAt, Reason)
SELECT cl.Id, h.FromStatus, h.ToStatus, CASE h.Actor WHEN 'A' THEN @adjuster ELSE @staff END,
       DATEADD(MINUTE, h.MinutesAfter, cl.CreatedAt), h.Reason
FROM #H h
JOIN #New n ON n.ClaimNumber = h.ClaimNumber
JOIN dbo.Claims cl ON cl.ClaimNumber = h.ClaimNumber
ORDER BY h.ClaimNumber, h.Seq;

INSERT INTO dbo.RepairEstimates (ClaimId, Revision, EstimatedAmount, SubmittedByUserId, SubmittedAt)
SELECT cl.Id, e.Revision,
       (SELECT SUM(l.Quantity * l.UnitPrice) FROM #L l WHERE l.ClaimNumber = e.ClaimNumber AND l.Revision = e.Revision),
       @staff, DATEADD(MINUTE, e.MinutesAfter, cl.CreatedAt)
FROM #E e
JOIN #New n ON n.ClaimNumber = e.ClaimNumber
JOIN dbo.Claims cl ON cl.ClaimNumber = e.ClaimNumber
ORDER BY e.ClaimNumber, e.Revision;

INSERT INTO dbo.RepairEstimateLines (RepairEstimateId, LineNumber, Kind, Description, Quantity, UnitPrice, LineTotal)
SELECT re.Id, l.LineNumber, l.Kind, l.Description, l.Quantity, l.UnitPrice, l.Quantity * l.UnitPrice
FROM #L l
JOIN #New n ON n.ClaimNumber = l.ClaimNumber
JOIN dbo.Claims cl ON cl.ClaimNumber = l.ClaimNumber
JOIN dbo.RepairEstimates re ON re.ClaimId = cl.Id AND re.Revision = l.Revision;

INSERT INTO dbo.Invoices (ClaimId, ActualAmount, Note, SubmittedByUserId, SubmittedAt)
SELECT cl.Id, i.ActualAmount, i.Note, @staff, DATEADD(MINUTE, i.MinutesAfter, cl.CreatedAt)
FROM #I i
JOIN #New n ON n.ClaimNumber = i.ClaimNumber
JOIN dbo.Claims cl ON cl.ClaimNumber = i.ClaimNumber;

-- Approval (Approved Amount and the revision it approves) and UpdatedAt = time of the last History line.
UPDATE cl
SET ApprovedAmount = c.ApprovedAmount,
    ApprovedRepairEstimateId = re.Id,
    UpdatedAt = ISNULL((SELECT MAX(h.OccurredAt) FROM dbo.ClaimHistory h WHERE h.ClaimId = cl.Id), cl.CreatedAt)
FROM dbo.Claims cl
JOIN #New n ON n.ClaimNumber = cl.ClaimNumber
JOIN #C c ON c.ClaimNumber = cl.ClaimNumber
LEFT JOIN dbo.RepairEstimates re ON re.ClaimId = cl.Id AND re.Revision = c.ApprovedRevision;

COMMIT TRANSACTION;

DECLARE @seeded int = (SELECT COUNT(*) FROM #New);
PRINT CONCAT(N'Seeded ', @seeded, N' demo Claims.');
DROP TABLE #New, #C, #H, #E, #L, #I;
GO

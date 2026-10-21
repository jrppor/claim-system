-- 008: Approved Amount of a Claim, stored apart from any Estimated Amount, and the Repair Estimate revision it approves. Idempotent.
-- Both stay NULL until an Insurance Adjuster approves. Approved Amount <= Coverage Limit is enforced by the application
-- (the limit lives on Policies, which a CHECK constraint cannot reach).
IF COL_LENGTH(N'dbo.Claims', N'ApprovedAmount') IS NULL
    ALTER TABLE dbo.Claims ADD ApprovedAmount decimal(18,2) NULL;
GO

IF COL_LENGTH(N'dbo.Claims', N'ApprovedRepairEstimateId') IS NULL
    ALTER TABLE dbo.Claims ADD ApprovedRepairEstimateId bigint NULL
        CONSTRAINT FK_Claims_ApprovedRepairEstimate REFERENCES dbo.RepairEstimates (Id);
GO

IF OBJECT_ID(N'dbo.CK_Claims_Approval', N'C') IS NULL
    EXEC(N'ALTER TABLE dbo.Claims ADD CONSTRAINT CK_Claims_Approval CHECK (
        (ApprovedAmount IS NULL AND ApprovedRepairEstimateId IS NULL) OR (ApprovedAmount > 0 AND ApprovedRepairEstimateId IS NOT NULL))');
GO

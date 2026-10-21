using Claim.Domain.Claims;

namespace Claim.Application.Claims;

/// <summary>Thai wording of the notification a Customer receives when a Claim changes status.</summary>
public static class StatusEmailComposer
{
    public static string ThaiLabel(ClaimStatus status) => status switch
    {
        ClaimStatus.Draft => "แบบร่าง",
        ClaimStatus.Submitted => "ส่งแล้ว",
        ClaimStatus.UnderReview => "กำลังพิจารณา",
        ClaimStatus.ChangesRequested => "ขอให้แก้ไข",
        ClaimStatus.Approved => "อนุมัติ",
        ClaimStatus.Rejected => "ปฏิเสธ",
        ClaimStatus.InRepair => "กำลังซ่อม",
        ClaimStatus.Repaired => "ซ่อมเสร็จ",
        ClaimStatus.InvoiceSubmitted => "ส่งใบเรียกเก็บแล้ว",
        ClaimStatus.Paid => "จ่ายแล้ว",
        ClaimStatus.Cancelled => "ยกเลิก",
        _ => status.ToString(),
    };

    /// <summary>Returns null when the Claim has no Customer email to write to.</summary>
    public static EmailMessage? Compose(Domain.Claims.Claim claim, Policy policy, ClaimHistoryEntry change)
    {
        var to = claim.Details.CustomerEmail;
        if (string.IsNullOrWhiteSpace(to)) return null;

        var label = ThaiLabel(change.ToStatus);
        var body = $"""
            เรียนคุณ {policy.Customer.FullName}

            เคลมเลขที่ {claim.ClaimNumber} เปลี่ยนสถานะเป็น "{label}"
            """;
        if (!string.IsNullOrWhiteSpace(change.Reason)) body += $"\nเหตุผล: {change.Reason}";
        body += "\n\nขอบคุณที่ใช้บริการ";

        return new EmailMessage(claim.Id, to, $"[{claim.ClaimNumber}] สถานะเคลมเปลี่ยนเป็น {label}", body);
    }
}

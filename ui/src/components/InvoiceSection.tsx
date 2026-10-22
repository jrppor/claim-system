"use client";

import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Grid from "@mui/material/Grid";
import Typography from "@mui/material/Typography";
import { useQuery } from "@tanstack/react-query";
import { invoiceStatuses, proxyFetch, type ClaimDetail, type Invoice } from "@/lib/claims";

const money = (n: number) => n.toLocaleString("th-TH", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** The Invoice shown next to the Approved Amount, so the Insurance Adjuster can spot a difference before paying. */
export function InvoiceSection({ claim }: { claim: ClaimDetail }) {
  const hasInvoice = invoiceStatuses.includes(claim.status);
  const invoice = useQuery({
    queryKey: ["claim", String(claim.id), "invoice"],
    queryFn: () => proxyFetch<Invoice>(`/claims/${claim.id}/invoice`),
    enabled: hasInvoice,
  });

  if (!hasInvoice || !invoice.data) return null;
  const i = invoice.data;
  const diff = i.difference;

  return (
    <Card>
      <CardContent>
        <Typography variant="h6" gutterBottom>
          ใบเรียกเก็บ (Invoice)
        </Typography>
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, sm: 4 }}>
            <Typography variant="caption" color="text.secondary">
              ยอดอนุมัติ (Approved Amount)
            </Typography>
            <Typography>{i.approvedAmount === null ? "-" : `${money(i.approvedAmount)} บาท`}</Typography>
          </Grid>
          <Grid size={{ xs: 12, sm: 4 }}>
            <Typography variant="caption" color="text.secondary">
              ค่าซ่อมจริงตาม Invoice
            </Typography>
            <Typography>{money(i.actualAmount)} บาท</Typography>
          </Grid>
          <Grid size={{ xs: 12, sm: 4 }}>
            <Typography variant="caption" color="text.secondary">
              ส่วนต่าง
            </Typography>
            <Typography color={diff !== null && diff > 0 ? "error" : "text.primary"}>
              {diff === null ? "-" : diff === 0 ? "ตรงกับยอดอนุมัติ" : `${diff > 0 ? "เกินยอดอนุมัติ" : "ต่ำกว่ายอดอนุมัติ"} ${money(Math.abs(diff))} บาท`}
            </Typography>
          </Grid>
          {i.note && (
            <Grid size={12}>
              <Typography variant="caption" color="text.secondary">
                หมายเหตุ
              </Typography>
              <Typography sx={{ whiteSpace: "pre-wrap" }}>{i.note}</Typography>
            </Grid>
          )}
        </Grid>
      </CardContent>
    </Card>
  );
}

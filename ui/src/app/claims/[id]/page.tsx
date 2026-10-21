"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import Grid from "@mui/material/Grid";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  claimStatusLabels,
  describeStatusChangeError,
  ProxyError,
  proxyFetch,
  type ClaimDetail,
  type ClaimHistoryEntry,
} from "@/lib/claims";
import { useSession } from "@/lib/useSession";

function Field({ label, value }: { label: string; value: string | null | undefined }) {
  return (
    <Grid size={{ xs: 12, sm: 6 }}>
      <Typography variant="caption" color="text.secondary">
        {label}
      </Typography>
      <Typography sx={{ whiteSpace: "pre-wrap" }}>{value || "-"}</Typography>
    </Grid>
  );
}

const dateFormat = new Intl.DateTimeFormat("th-TH", { dateStyle: "medium", timeStyle: "short" });
// The API sends UTC without a zone suffix.
const formatDate = (iso: string) => dateFormat.format(new Date(iso.endsWith("Z") ? iso : `${iso}Z`));

export default function ClaimDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { data: user } = useSession();
  const claim = useQuery({
    queryKey: ["claim", id],
    queryFn: () => proxyFetch<ClaimDetail>(`/claims/${id}`),
    retry: false,
  });

  const history = useQuery({
    queryKey: ["claim", id, "history"],
    queryFn: () => proxyFetch<ClaimHistoryEntry[]>(`/claims/${id}/history`),
  });
  const queryClient = useQueryClient();
  const submit = useMutation({
    mutationFn: () => proxyFetch<ClaimDetail>(`/claims/${id}/submit`, { method: "POST" }),
    onSuccess: (updated) => {
      queryClient.setQueryData(["claim", id], updated);
      void queryClient.invalidateQueries({ queryKey: ["claim", id, "history"] });
    },
  });

  if (claim.isPending) return <CircularProgress size={24} />;
  if (claim.isError) {
    const notFound = claim.error instanceof ProxyError && claim.error.status === 404;
    return <Alert severity="error">{notFound ? "ไม่พบเคลมนี้" : "โหลดข้อมูลเคลมไม่สำเร็จ"}</Alert>;
  }

  const c = claim.data;
  const canEdit = user?.role === "GarageStaff" && c.status === "Draft";

  return (
    <Stack spacing={3}>
      <Stack direction="row" spacing={2} sx={{ alignItems: "center" }}>
        <Typography variant="h4" component="h1" sx={{ flexGrow: 1 }}>
          {c.claimNumber}
        </Typography>
        <Chip label={claimStatusLabels[c.status]} color={c.status === "Draft" ? "default" : "primary"} />
        {canEdit && (
          <Button variant="contained" component={Link} href={`/claims/${c.id}/edit`}>
            แก้ไข
          </Button>
        )}
        {canEdit && (
          <Button variant="contained" color="success" disabled={submit.isPending} onClick={() => submit.mutate()}>
            ส่งเคลม
          </Button>
        )}
      </Stack>
      {submit.isError && <Alert severity="error">{describeStatusChangeError(submit.error)}</Alert>}

      <Card>
        <CardContent>
          <Typography variant="h6" gutterBottom>
            กรมธรรม์และลูกค้า
          </Typography>
          <Grid container spacing={2}>
            <Field label="เลขกรมธรรม์" value={c.policy.policyNumber} />
            <Field label="วงเงินคุ้มครอง (บาท)" value={c.policy.coverageLimit.toLocaleString("th-TH")} />
            <Field label="ชื่อลูกค้า" value={c.policy.customer.fullName} />
            <Field label="อีเมลลูกค้าในเคลม" value={c.customerEmail} />
          </Grid>
        </CardContent>
      </Card>

      <Card>
        <CardContent>
          <Typography variant="h6" gutterBottom>
            รถและความเสียหาย
          </Typography>
          <Grid container spacing={2}>
            <Field label="ทะเบียนรถ" value={c.vehiclePlate} />
            <Field label="ยี่ห้อ / รุ่น" value={[c.vehicleMake, c.vehicleModel].filter(Boolean).join(" ")} />
            <Grid size={12}>
              <Typography variant="caption" color="text.secondary">
                รายละเอียดความเสียหาย
              </Typography>
              <Typography sx={{ whiteSpace: "pre-wrap" }}>{c.damageDescription || "-"}</Typography>
            </Grid>
            <Field label="สร้างเมื่อ" value={formatDate(c.createdAt)} />
            <Field label="แก้ไขล่าสุด" value={formatDate(c.updatedAt)} />
          </Grid>
        </CardContent>
      </Card>

      <Card>
        <CardContent>
          <Typography variant="h6" gutterBottom>
            ประวัติเคลม
          </Typography>
          {history.isPending && <CircularProgress size={20} />}
          {history.isError && <Alert severity="error">โหลดประวัติเคลมไม่สำเร็จ</Alert>}
          {history.data?.length === 0 && <Typography color="text.secondary">ยังไม่มีการเปลี่ยนสถานะ</Typography>}
          <Stack spacing={1.5}>
            {history.data?.map((h, i) => (
              <div key={i}>
                <Typography>
                  {h.fromStatus ? `${claimStatusLabels[h.fromStatus]} → ` : ""}
                  {claimStatusLabels[h.toStatus]}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {h.actorName} · {formatDate(h.occurredAt)}
                </Typography>
                {h.reason && <Typography variant="body2">เหตุผล: {h.reason}</Typography>}
              </div>
            ))}
          </Stack>
        </CardContent>
      </Card>
    </Stack>
  );
}

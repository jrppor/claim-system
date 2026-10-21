"use client";

import Link from "next/link";
import Alert from "@mui/material/Alert";
import Card from "@mui/material/Card";
import CardActionArea from "@mui/material/CardActionArea";
import CardContent from "@mui/material/CardContent";
import CircularProgress from "@mui/material/CircularProgress";
import Grid from "@mui/material/Grid";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { useQuery } from "@tanstack/react-query";
import { claimStatusLabels, type ClaimStatus } from "@/lib/claims";
import { fetchDashboardSummary } from "@/lib/claimList";
import { roleLabels } from "@/lib/roles";
import { useSession } from "@/lib/useSession";

export default function DashboardPage() {
  const { data: user } = useSession();
  const summary = useQuery({ queryKey: ["dashboard-summary"], queryFn: fetchDashboardSummary });

  return (
    <Stack spacing={3}>
      <Typography variant="h4" component="h1">
        แดชบอร์ด
      </Typography>
      {user && (
        <Typography color="text.secondary">
          จำนวนเคลมตามสถานะสำหรับ {roleLabels[user.role]}
          {user.role === "Admin" && " (ดูได้อย่างเดียว)"}
        </Typography>
      )}
      {summary.isPending && <CircularProgress size={24} />}
      {summary.isError && <Alert severity="error">โหลดข้อมูลสรุปไม่สำเร็จ</Alert>}
      {summary.data && (
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, sm: 6, md: 4 }}>
            <Card variant="outlined">
              <CardActionArea component={Link} href="/claims">
                <CardContent>
                  <Typography color="text.secondary">เคลมทั้งหมด</Typography>
                  <Typography variant="h3">{summary.data.total}</Typography>
                </CardContent>
              </CardActionArea>
            </Card>
          </Grid>
          {(Object.keys(summary.data.countsByStatus) as ClaimStatus[]).map((status) => (
            <Grid key={status} size={{ xs: 12, sm: 6, md: 4 }}>
              <Card variant="outlined">
                <CardActionArea component={Link} href={`/claims?status=${status}`}>
                  <CardContent>
                    <Typography color="text.secondary">{claimStatusLabels[status]}</Typography>
                    <Typography variant="h3">{summary.data.countsByStatus[status] ?? 0}</Typography>
                  </CardContent>
                </CardActionArea>
              </Card>
            </Grid>
          ))}
        </Grid>
      )}
    </Stack>
  );
}

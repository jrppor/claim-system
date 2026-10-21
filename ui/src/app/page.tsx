"use client";

import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import CircularProgress from "@mui/material/CircularProgress";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { useQuery } from "@tanstack/react-query";
import { apiBaseUrl, apiFetch } from "@/lib/api";

export default function HomePage() {
  const health = useQuery({
    queryKey: ["health"],
    queryFn: () => apiFetch<unknown>("/health"),
  });

  return (
    <Stack spacing={3}>
      <Typography variant="h4" component="h1">
        ยินดีต้อนรับสู่ระบบเคลมประกันรถยนต์
      </Typography>
      <Card>
        <CardContent>
          <Stack spacing={2}>
            <Typography variant="h6">สถานะ API</Typography>
            <Typography variant="body2" color="text.secondary">
              ที่อยู่ API: {apiBaseUrl}
            </Typography>
            {health.isPending && <CircularProgress size={24} />}
            {health.isError && <Alert severity="error">เชื่อมต่อ API ไม่ได้</Alert>}
            {health.isSuccess && <Alert severity="success">API พร้อมใช้งาน</Alert>}
            <Button variant="outlined" onClick={() => health.refetch()} disabled={health.isFetching} sx={{ alignSelf: "flex-start" }}>
              ตรวจสอบอีกครั้ง
            </Button>
          </Stack>
        </CardContent>
      </Card>
    </Stack>
  );
}

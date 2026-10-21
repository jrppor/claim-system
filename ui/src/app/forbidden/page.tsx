import Alert from "@mui/material/Alert";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

// Served with status 403 by src/proxy.ts when a role opens a page it is not allowed to use.
export default function ForbiddenPage() {
  return (
    <Stack spacing={2}>
      <Typography variant="h4" component="h1">
        ไม่มีสิทธิ์เข้าถึง
      </Typography>
      <Alert severity="warning">บทบาทของคุณไม่ได้รับอนุญาตให้เปิดหน้านี้</Alert>
    </Stack>
  );
}

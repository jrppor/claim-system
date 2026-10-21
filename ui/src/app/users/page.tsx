"use client";

import Alert from "@mui/material/Alert";
import CircularProgress from "@mui/material/CircularProgress";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Typography from "@mui/material/Typography";
import { useQuery } from "@tanstack/react-query";
import { roleLabels, type Role } from "@/lib/roles";

interface UserRow {
  id: number;
  email: string;
  displayName: string;
  role: Role;
}

// Admin-only (src/lib/roles.ts routeRules). Minimal list; full user management is a later ticket.
export default function UsersPage() {
  const users = useQuery({
    queryKey: ["users"],
    queryFn: async () => {
      const response = await fetch("/api/proxy/users", { cache: "no-store" });
      if (!response.ok) throw new Error(String(response.status));
      return (await response.json()) as UserRow[];
    },
  });

  return (
    <Stack spacing={3}>
      <Typography variant="h4" component="h1">
        จัดการผู้ใช้
      </Typography>
      {users.isPending && <CircularProgress size={24} />}
      {users.isError && <Alert severity="error">โหลดรายชื่อผู้ใช้ไม่สำเร็จ</Alert>}
      {users.isSuccess && (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>ชื่อ</TableCell>
              <TableCell>อีเมล</TableCell>
              <TableCell>บทบาท</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {users.data.map((u) => (
              <TableRow key={u.id}>
                <TableCell>{u.displayName}</TableCell>
                <TableCell>{u.email}</TableCell>
                <TableCell>{roleLabels[u.role]}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </Stack>
  );
}

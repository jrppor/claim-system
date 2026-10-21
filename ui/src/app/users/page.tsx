"use client";

import { useState } from "react";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import Dialog from "@mui/material/Dialog";
import DialogActions from "@mui/material/DialogActions";
import DialogContent from "@mui/material/DialogContent";
import DialogTitle from "@mui/material/DialogTitle";
import MenuItem from "@mui/material/MenuItem";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { ROLES, roleLabels, type Role } from "@/lib/roles";
import { useSession } from "@/lib/useSession";

interface UserRow {
  id: number;
  email: string;
  displayName: string;
  role: Role;
  isActive: boolean;
}

interface ProblemBody {
  title?: string;
  errors?: Record<string, string[]>;
}

class RequestError extends Error {
  constructor(public readonly problem: ProblemBody | undefined) {
    super(problem?.title ?? "request failed");
  }
}

async function callApi(path: string, method: string, body?: unknown): Promise<void> {
  const response = await fetch(`/api/proxy/${path}`, {
    method,
    headers: body ? { "Content-Type": "application/json" } : undefined,
    body: body ? JSON.stringify(body) : undefined,
    cache: "no-store",
  });
  if (!response.ok) throw new RequestError(await response.json().catch(() => undefined));
}

/** First validation message the API returned (Thai), else a generic Thai fallback. */
function problemMessage(error: unknown): string {
  if (error instanceof RequestError) {
    const first = Object.values(error.problem?.errors ?? {})[0]?.[0];
    if (first) return first;
  }
  return "ดำเนินการไม่สำเร็จ กรุณาลองใหม่อีกครั้ง";
}

const MIN_PASSWORD = 8;
const passwordSchema = z.string().min(MIN_PASSWORD, `รหัสผ่านต้องมีอย่างน้อย ${MIN_PASSWORD} ตัวอักษร`);

const createSchema = z.object({
  email: z.email("รูปแบบอีเมลไม่ถูกต้อง"),
  displayName: z.string().trim().min(1, "กรุณากรอกชื่อ"),
  password: passwordSchema,
  role: z.enum(ROLES),
});
type CreateValues = z.infer<typeof createSchema>;

function CreateUserDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const queryClient = useQueryClient();
  const {
    register,
    control,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = useForm<CreateValues>({
    resolver: zodResolver(createSchema),
    defaultValues: { email: "", displayName: "", password: "", role: "GarageStaff" },
  });
  const [formError, setFormError] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: (values: CreateValues) => callApi("users", "POST", values),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["users"] });
      reset();
      setFormError(null);
      onClose();
    },
    onError: (error) => {
      const fieldErrors = error instanceof RequestError ? error.problem?.errors : undefined;
      let placed = false;
      for (const field of ["email", "displayName", "password", "role"] as const) {
        const message = fieldErrors?.[field]?.[0];
        if (message) {
          setError(field, { message });
          placed = true;
        }
      }
      setFormError(placed ? null : problemMessage(error));
    },
  });

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <form onSubmit={handleSubmit((values) => create.mutate(values))} noValidate>
        <DialogTitle>เพิ่มผู้ใช้</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            {formError && <Alert severity="error">{formError}</Alert>}
            <TextField label="อีเมล" {...register("email")} error={!!errors.email} helperText={errors.email?.message} />
            <TextField
              label="ชื่อที่แสดง"
              {...register("displayName")}
              error={!!errors.displayName}
              helperText={errors.displayName?.message}
            />
            <TextField
              label="รหัสผ่านเริ่มต้น"
              type="password"
              autoComplete="new-password"
              {...register("password")}
              error={!!errors.password}
              helperText={errors.password?.message}
            />
            <Controller
              name="role"
              control={control}
              render={({ field }) => (
                <TextField select label="บทบาท" {...field} error={!!errors.role} helperText={errors.role?.message}>
                  {ROLES.map((r) => (
                    <MenuItem key={r} value={r}>
                      {roleLabels[r]}
                    </MenuItem>
                  ))}
                </TextField>
              )}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose}>ยกเลิก</Button>
          <Button type="submit" variant="contained" disabled={create.isPending}>
            สร้างผู้ใช้
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}

const resetSchema = z.object({ password: passwordSchema });
type ResetValues = z.infer<typeof resetSchema>;

function ResetPasswordDialog({ user, onClose }: { user: UserRow | null; onClose: () => void }) {
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ResetValues>({ resolver: zodResolver(resetSchema), defaultValues: { password: "" } });
  const [formError, setFormError] = useState<string | null>(null);

  const close = () => {
    reset();
    setFormError(null);
    onClose();
  };

  const resetPassword = useMutation({
    mutationFn: (values: ResetValues) => callApi(`users/${user!.id}/reset-password`, "POST", values),
    onSuccess: close,
    onError: (error) => setFormError(problemMessage(error)),
  });

  return (
    <Dialog open={user !== null} onClose={close} fullWidth maxWidth="xs">
      <form onSubmit={handleSubmit((values) => resetPassword.mutate(values))} noValidate>
        <DialogTitle>รีเซ็ตรหัสผ่าน</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <Typography variant="body2">{user?.displayName} ({user?.email})</Typography>
            {formError && <Alert severity="error">{formError}</Alert>}
            <TextField
              label="รหัสผ่านใหม่"
              type="password"
              autoComplete="new-password"
              {...register("password")}
              error={!!errors.password}
              helperText={errors.password?.message}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={close}>ยกเลิก</Button>
          <Button type="submit" variant="contained" disabled={resetPassword.isPending}>
            บันทึกรหัสผ่านใหม่
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}

// Admin-only (src/lib/roles.ts routeRules); the API enforces the same.
export default function UsersPage() {
  const queryClient = useQueryClient();
  const session = useSession();
  const [creating, setCreating] = useState(false);
  const [resetting, setResetting] = useState<UserRow | null>(null);
  const [disabling, setDisabling] = useState<UserRow | null>(null);

  const users = useQuery({
    queryKey: ["users"],
    queryFn: async () => {
      const response = await fetch("/api/proxy/users", { cache: "no-store" });
      if (!response.ok) throw new Error(String(response.status));
      return (await response.json()) as UserRow[];
    },
  });

  const disable = useMutation({
    mutationFn: (user: UserRow) => callApi(`users/${user.id}/disable`, "POST"),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["users"] });
      setDisabling(null);
    },
  });

  const currentUserId = session.data?.id;

  return (
    <Stack spacing={3}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center" }}>
        <Typography variant="h4" component="h1">
          จัดการผู้ใช้
        </Typography>
        <Button variant="contained" onClick={() => setCreating(true)}>
          เพิ่มผู้ใช้
        </Button>
      </Stack>
      {users.isPending && <CircularProgress size={24} />}
      {users.isError && <Alert severity="error">โหลดรายชื่อผู้ใช้ไม่สำเร็จ</Alert>}
      {users.isSuccess && (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>ชื่อ</TableCell>
              <TableCell>อีเมล</TableCell>
              <TableCell>บทบาท</TableCell>
              <TableCell>สถานะ</TableCell>
              <TableCell align="right">จัดการ</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {users.data.map((u) => (
              <TableRow key={u.id}>
                <TableCell>{u.displayName}</TableCell>
                <TableCell>{u.email}</TableCell>
                <TableCell>{roleLabels[u.role]}</TableCell>
                <TableCell>
                  <Chip
                    size="small"
                    label={u.isActive ? "ใช้งานอยู่" : "ปิดใช้งาน"}
                    color={u.isActive ? "success" : "default"}
                  />
                </TableCell>
                <TableCell align="right">
                  <Button size="small" onClick={() => setResetting(u)}>
                    รีเซ็ตรหัสผ่าน
                  </Button>
                  {u.isActive && u.id !== currentUserId && (
                    <Button size="small" color="error" onClick={() => setDisabling(u)}>
                      ปิดใช้งาน
                    </Button>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <CreateUserDialog open={creating} onClose={() => setCreating(false)} />
      <ResetPasswordDialog user={resetting} onClose={() => setResetting(null)} />

      <Dialog open={disabling !== null} onClose={() => setDisabling(null)}>
        <DialogTitle>ปิดใช้งานผู้ใช้</DialogTitle>
        <DialogContent>
          <Typography>
            ปิดใช้งาน {disabling?.displayName} ({disabling?.email}) ผู้ใช้จะเข้าสู่ระบบไม่ได้ และเซสชันที่ถืออยู่จะถูกเพิกถอน
          </Typography>
          {disable.isError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              {problemMessage(disable.error)}
            </Alert>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDisabling(null)}>ยกเลิก</Button>
          <Button color="error" variant="contained" disabled={disable.isPending} onClick={() => disabling && disable.mutate(disabling)}>
            ยืนยันปิดใช้งาน
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
}

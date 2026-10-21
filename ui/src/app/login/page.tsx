"use client";

import { Suspense, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";

const schema = z.object({
  email: z.email("รูปแบบอีเมลไม่ถูกต้อง"),
  password: z.string().min(1, "กรุณากรอกรหัสผ่าน"),
});

type FormValues = z.infer<typeof schema>;

function LoginForm() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const searchParams = useSearchParams();
  const [error, setError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  async function onSubmit(values: FormValues) {
    setError(null);
    const response = await fetch("/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(values),
    }).catch(() => null);

    if (!response) return setError("เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่อีกครั้ง");
    if (response.status === 401) return setError("อีเมลหรือรหัสผ่านไม่ถูกต้อง");
    if (!response.ok) return setError("เข้าสู่ระบบไม่สำเร็จ กรุณาลองใหม่อีกครั้ง");

    await queryClient.invalidateQueries({ queryKey: ["session"] });
    // Only follow same-site relative targets.
    const next = searchParams.get("next");
    router.replace(next && next.startsWith("/") && !next.startsWith("//") ? next : "/");
    router.refresh();
  }

  return (
    <Card sx={{ maxWidth: 420, mx: "auto" }}>
      <CardContent>
        <Stack spacing={3} component="form" onSubmit={handleSubmit(onSubmit)} noValidate>
          <Typography variant="h5" component="h1">
            เข้าสู่ระบบ
          </Typography>
          {error && <Alert severity="error">{error}</Alert>}
          <TextField
            label="อีเมล"
            type="email"
            autoComplete="username"
            {...register("email")}
            error={!!errors.email}
            helperText={errors.email?.message}
          />
          <TextField
            label="รหัสผ่าน"
            type="password"
            autoComplete="current-password"
            {...register("password")}
            error={!!errors.password}
            helperText={errors.password?.message}
          />
          <Button type="submit" variant="contained" disabled={isSubmitting}>
            เข้าสู่ระบบ
          </Button>
        </Stack>
      </CardContent>
    </Card>
  );
}

export default function LoginPage() {
  return (
    <Suspense>
      <LoginForm />
    </Suspense>
  );
}

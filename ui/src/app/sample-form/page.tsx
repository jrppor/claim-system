"use client";

import { useState } from "react";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";

const schema = z.object({
  customerEmail: z.email("รูปแบบอีเมลไม่ถูกต้อง"),
  damageDescription: z.string().min(10, "กรุณาระบุรายละเอียดความเสียหายอย่างน้อย 10 ตัวอักษร"),
});

type FormValues = z.infer<typeof schema>;

export default function SampleFormPage() {
  const [submitted, setSubmitted] = useState<FormValues | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  return (
    <Stack spacing={3} component="form" onSubmit={handleSubmit(setSubmitted)} noValidate>
      <Typography variant="h4" component="h1">
        ฟอร์มตัวอย่าง
      </Typography>
      <TextField
        label="อีเมลลูกค้า"
        {...register("customerEmail")}
        error={!!errors.customerEmail}
        helperText={errors.customerEmail?.message}
      />
      <TextField
        label="รายละเอียดความเสียหาย"
        multiline
        minRows={3}
        {...register("damageDescription")}
        error={!!errors.damageDescription}
        helperText={errors.damageDescription?.message}
      />
      <Button type="submit" variant="contained" sx={{ alignSelf: "flex-start" }}>
        ตรวจสอบข้อมูล
      </Button>
      {submitted && <Alert severity="success">ข้อมูลถูกต้อง ({submitted.customerEmail})</Alert>}
    </Stack>
  );
}

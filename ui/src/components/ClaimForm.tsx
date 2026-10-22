"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import List from "@mui/material/List";
import ListItemButton from "@mui/material/ListItemButton";
import ListItemText from "@mui/material/ListItemText";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import {
  describeSaveError,
  ifMatch,
  proxyFetch,
  type ClaimDetail,
  type ClaimInput,
  type PolicyInfo,
} from "@/lib/claims";

const schema = z.object({
  vehiclePlate: z.string().max(20, "ทะเบียนยาวเกินไป"),
  vehicleMake: z.string().max(100, "ยี่ห้อยาวเกินไป"),
  vehicleModel: z.string().max(100, "รุ่นยาวเกินไป"),
  customerEmail: z.union([z.literal(""), z.email("รูปแบบอีเมลไม่ถูกต้อง")]),
  damageDescription: z.string().max(2000, "รายละเอียดต้องไม่เกิน 2000 ตัวอักษร"),
});

type FormValues = z.infer<typeof schema>;

function PolicySummary({ policy }: { policy: PolicyInfo }) {
  return (
    <Alert severity="info" icon={false}>
      <Typography variant="subtitle2">กรมธรรม์ {policy.policyNumber}</Typography>
      <Typography variant="body2">
        ลูกค้า: {policy.customer.fullName} · รถ: {policy.vehicleMake} {policy.vehicleModel} ทะเบียน {policy.plateNumber}
      </Typography>
      <Typography variant="body2">วงเงินคุ้มครอง: {policy.coverageLimit.toLocaleString("th-TH")} บาท</Typography>
    </Alert>
  );
}

function PolicyPicker({ onPick }: { onPick: (policy: PolicyInfo) => void }) {
  const [text, setText] = useState("");
  const [term, setTerm] = useState("");
  const results = useQuery({
    queryKey: ["policies", term],
    queryFn: () => proxyFetch<PolicyInfo[]>(`/policies?q=${encodeURIComponent(term)}`),
    enabled: term.length > 0,
  });

  return (
    <Stack spacing={2}>
      <Typography variant="h6">1. ค้นหากรมธรรม์</Typography>
      <Stack
        direction="row"
        spacing={2}
        component="form"
        onSubmit={(e) => {
          e.preventDefault();
          setTerm(text.trim());
        }}
      >
        <TextField
          label="เลขกรมธรรม์หรือทะเบียนรถ"
          value={text}
          onChange={(e) => setText(e.target.value)}
          fullWidth
          size="small"
        />
        <Button type="submit" variant="contained" disabled={!text.trim()}>
          ค้นหา
        </Button>
      </Stack>
      {results.isError && <Alert severity="error">ค้นหากรมธรรม์ไม่สำเร็จ</Alert>}
      {results.isSuccess && results.data.length === 0 && <Alert severity="warning">ไม่พบกรมธรรม์ที่ตรงกับคำค้น</Alert>}
      {results.isSuccess && results.data.length > 0 && (
        <List dense disablePadding>
          {results.data.map((p) => (
            <ListItemButton key={p.id} onClick={() => onPick(p)} divider>
              <ListItemText
                primary={`${p.policyNumber} · ${p.customer.fullName}`}
                secondary={`${p.vehicleMake} ${p.vehicleModel} ทะเบียน ${p.plateNumber}`}
              />
            </ListItemButton>
          ))}
        </List>
      )}
    </Stack>
  );
}

interface Props {
  /** When set, the form edits this Draft Claim; otherwise it creates one. */
  claim?: ClaimDetail;
}

export function ClaimForm({ claim }: Props) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [policy, setPolicy] = useState<PolicyInfo | null>(claim?.policy ?? null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      vehiclePlate: claim?.vehiclePlate ?? "",
      vehicleMake: claim?.vehicleMake ?? "",
      vehicleModel: claim?.vehicleModel ?? "",
      customerEmail: claim?.customerEmail ?? "",
      damageDescription: claim?.damageDescription ?? "",
    },
  });

  const save = useMutation({
    mutationFn: (values: FormValues) => {
      const body: ClaimInput = { policyId: policy!.id, ...values };
      return claim
        ? proxyFetch<ClaimDetail>(`/claims/${claim.id}`, { method: "PUT", body: JSON.stringify(body), ...ifMatch(claim) })
        : proxyFetch<ClaimDetail>("/claims", { method: "POST", body: JSON.stringify(body) });
    },
    onSuccess: (saved) => {
      queryClient.setQueryData(["claim", String(saved.id)], saved);
      router.push(`/claims/${saved.id}`);
    },
  });

  function pickPolicy(p: PolicyInfo) {
    setPolicy(p);
    reset({
      vehiclePlate: p.plateNumber,
      vehicleMake: p.vehicleMake,
      vehicleModel: p.vehicleModel,
      customerEmail: p.customer.email,
      damageDescription: "",
    });
  }

  if (!policy) return <PolicyPicker onPick={pickPolicy} />;

  return (
    <Stack spacing={3} component="form" onSubmit={handleSubmit((v) => save.mutate(v))} noValidate>
      <PolicySummary policy={policy} />
      {!claim && (
        <Button variant="text" onClick={() => setPolicy(null)} sx={{ alignSelf: "flex-start" }}>
          เปลี่ยนกรมธรรม์
        </Button>
      )}
      <Typography variant="h6">{claim ? "แก้ไขข้อมูล" : "2. กรอกข้อมูลเคลม"}</Typography>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
        <TextField label="ทะเบียนรถ" {...register("vehiclePlate")} error={!!errors.vehiclePlate} helperText={errors.vehiclePlate?.message} fullWidth />
        <TextField label="ยี่ห้อ" {...register("vehicleMake")} error={!!errors.vehicleMake} helperText={errors.vehicleMake?.message} fullWidth />
        <TextField label="รุ่น" {...register("vehicleModel")} error={!!errors.vehicleModel} helperText={errors.vehicleModel?.message} fullWidth />
      </Stack>
      <TextField
        label="อีเมลลูกค้า"
        {...register("customerEmail")}
        error={!!errors.customerEmail}
        helperText={errors.customerEmail?.message}
      />
      <TextField
        label="รายละเอียดความเสียหาย"
        multiline
        minRows={4}
        {...register("damageDescription")}
        error={!!errors.damageDescription}
        helperText={errors.damageDescription?.message}
      />
      {save.isError && <Alert severity="error">{describeSaveError(save.error)}</Alert>}
      <Stack direction="row" spacing={2}>
        <Button type="submit" variant="contained" disabled={save.isPending}>
          {claim ? "บันทึกการแก้ไข" : "บันทึกแบบร่าง"}
        </Button>
        <Button onClick={() => router.back()} disabled={save.isPending}>
          ยกเลิก
        </Button>
      </Stack>
    </Stack>
  );
}

"use client";

import { useState } from "react";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import IconButton from "@mui/material/IconButton";
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
import { useFieldArray, useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import {
  describeEstimateError,
  estimateAcceptingStatuses,
  estimateLineKindLabels,
  ifMatch,
  proxyFetch,
  type ClaimDetail,
  type EstimateLineKind,
  type RepairEstimate,
} from "@/lib/claims";

const money = (n: number) => n.toLocaleString("th-TH", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const dateFormat = new Intl.DateTimeFormat("th-TH", { dateStyle: "medium", timeStyle: "short" });
const formatDate = (iso: string) => dateFormat.format(new Date(iso.endsWith("Z") ? iso : `${iso}Z`));

const positive = (s: string) => s.trim() !== "" && Number.isFinite(Number(s)) && Number(s) > 0;
const nonNegative = (s: string) => s.trim() !== "" && Number.isFinite(Number(s)) && Number(s) >= 0;

const schema = z.object({
  lines: z
    .array(
      z.object({
        kind: z.enum(["Part", "Labour"]),
        description: z.string().trim().min(1, "กรุณาระบุรายการ").max(200, "ยาวเกินไป"),
        quantity: z.string().refine(positive, "ต้องมากกว่า 0"),
        unitPrice: z.string().refine(nonNegative, "ต้องไม่ติดลบ"),
      }),
    )
    .min(1, "ต้องมีอย่างน้อย 1 รายการ"),
});

type FormValues = z.infer<typeof schema>;

const emptyLine = { kind: "Part" as EstimateLineKind, description: "", quantity: "1", unitPrice: "" };

function RevisionCard({ estimate }: { estimate: RepairEstimate }) {
  return (
    <Card variant="outlined">
      <CardContent>
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 1 }}>
          <Typography variant="subtitle1" sx={{ flexGrow: 1 }}>
            ฉบับที่ {estimate.revision} · {formatDate(estimate.submittedAt)}
          </Typography>
          {estimate.isLatest && <Chip color="primary" size="small" label="ฉบับล่าสุด (ที่อยู่ระหว่างพิจารณา)" />}
        </Stack>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>ประเภท</TableCell>
              <TableCell>รายการ</TableCell>
              <TableCell align="right">จำนวน</TableCell>
              <TableCell align="right">ราคาต่อหน่วย</TableCell>
              <TableCell align="right">รวม</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {estimate.lines.map((l, i) => (
              <TableRow key={i}>
                <TableCell>{estimateLineKindLabels[l.kind]}</TableCell>
                <TableCell>{l.description}</TableCell>
                <TableCell align="right">{l.quantity.toLocaleString("th-TH")}</TableCell>
                <TableCell align="right">{money(l.unitPrice)}</TableCell>
                <TableCell align="right">{money(l.lineTotal)}</TableCell>
              </TableRow>
            ))}
            <TableRow>
              <TableCell colSpan={4} align="right">
                <strong>ยอดประเมิน (Estimated Amount)</strong>
              </TableCell>
              <TableCell align="right">
                <strong>{money(estimate.estimatedAmount)}</strong>
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>
        {estimate.exceedsCoverageLimit && (
          <Alert severity="warning" sx={{ mt: 1 }}>
            ยอดประเมินเกินวงเงินคุ้มครอง ({money(estimate.coverageLimit)} บาท) ส่วนที่เกินเป็นความรับผิดชอบของลูกค้า
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}

function EstimateForm({ claim, onSubmitted }: { claim: ClaimDetail; onSubmitted: (e: RepairEstimate) => void }) {
  const form = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { lines: [{ ...emptyLine }] } });
  const { fields, append, remove } = useFieldArray({ control: form.control, name: "lines" });
  const watched = useWatch({ control: form.control, name: "lines" });

  // Preview only: the API recomputes the Estimated Amount and its answer is the one shown after submit.
  const preview = (watched ?? []).reduce((sum, l) => {
    const q = Number(l.quantity);
    const p = Number(l.unitPrice);
    return Number.isFinite(q) && Number.isFinite(p) ? sum + q * p : sum;
  }, 0);
  const overLimit = preview > claim.policy.coverageLimit;

  const submit = useMutation({
    mutationFn: (values: FormValues) =>
      proxyFetch<RepairEstimate>(`/claims/${claim.id}/estimates`, {
        method: "POST",
        ...ifMatch(claim),
        body: JSON.stringify({
          lines: values.lines.map((l) => ({
            kind: l.kind,
            description: l.description.trim(),
            quantity: Number(l.quantity),
            unitPrice: Number(l.unitPrice),
          })),
        }),
      }),
    onSuccess: (estimate) => {
      onSubmitted(estimate);
      form.reset({ lines: [{ ...emptyLine }] });
    },
  });

  return (
    <form onSubmit={form.handleSubmit((v) => submit.mutate(v))} noValidate>
      <Stack spacing={2}>
        <Typography variant="subtitle1">
          {claim.status === "ChangesRequested" ? "ส่งใบประเมินฉบับแก้ไข" : "ส่งใบประเมินราคาซ่อม"}
        </Typography>
        {fields.map((field, i) => (
          <Stack key={field.id} direction={{ xs: "column", md: "row" }} spacing={1} sx={{ alignItems: "flex-start" }}>
            <TextField select size="small" label="ประเภท" defaultValue={field.kind} sx={{ minWidth: 120 }} {...form.register(`lines.${i}.kind`)}>
              <MenuItem value="Part">{estimateLineKindLabels.Part}</MenuItem>
              <MenuItem value="Labour">{estimateLineKindLabels.Labour}</MenuItem>
            </TextField>
            <TextField
              size="small"
              label="รายการ"
              sx={{ flexGrow: 1 }}
              error={!!form.formState.errors.lines?.[i]?.description}
              helperText={form.formState.errors.lines?.[i]?.description?.message}
              {...form.register(`lines.${i}.description`)}
            />
            <TextField
              size="small"
              label="จำนวน"
              sx={{ width: 110 }}
              slotProps={{ htmlInput: { inputMode: "decimal" } }}
              error={!!form.formState.errors.lines?.[i]?.quantity}
              helperText={form.formState.errors.lines?.[i]?.quantity?.message}
              {...form.register(`lines.${i}.quantity`)}
            />
            <TextField
              size="small"
              label="ราคาต่อหน่วย (บาท)"
              sx={{ width: 170 }}
              slotProps={{ htmlInput: { inputMode: "decimal" } }}
              error={!!form.formState.errors.lines?.[i]?.unitPrice}
              helperText={form.formState.errors.lines?.[i]?.unitPrice?.message}
              {...form.register(`lines.${i}.unitPrice`)}
            />
            <IconButton aria-label="ลบรายการ" disabled={fields.length === 1} onClick={() => remove(i)}>
              ✕
            </IconButton>
          </Stack>
        ))}
        {form.formState.errors.lines?.message && <Alert severity="error">{form.formState.errors.lines.message}</Alert>}
        <Button variant="outlined" onClick={() => append({ ...emptyLine })} sx={{ alignSelf: "flex-start" }}>
          เพิ่มรายการ
        </Button>
        <Typography>ยอดประเมินโดยประมาณ: {money(preview)} บาท (ระบบจะคำนวณยอดจริงอีกครั้งเมื่อส่ง)</Typography>
        {overLimit && (
          <Alert severity="warning">
            ยอดประเมินเกินวงเงินคุ้มครอง {money(claim.policy.coverageLimit)} บาท ยังส่งได้ แต่ส่วนที่เกินเป็นความรับผิดชอบของลูกค้า
          </Alert>
        )}
        {submit.isError && <Alert severity="error">{describeEstimateError(submit.error)}</Alert>}
        <Button type="submit" variant="contained" disabled={submit.isPending} sx={{ alignSelf: "flex-start" }}>
          ส่งใบประเมิน
        </Button>
      </Stack>
    </form>
  );
}

/** Repair Estimate revisions of a Claim (latest marked) and, for Garage Staff when the status allows, the form to add a revision. */
export function RepairEstimateSection({ claim, role }: { claim: ClaimDetail; role: string | undefined }) {
  const queryClient = useQueryClient();
  const [notice, setNotice] = useState<RepairEstimate | null>(null);
  const estimates = useQuery({
    queryKey: ["claim", String(claim.id), "estimates"],
    queryFn: () => proxyFetch<RepairEstimate[]>(`/claims/${claim.id}/estimates`),
    enabled: claim.status !== "Draft" || role !== "InsuranceAdjuster",
  });
  const canSubmit = role === "GarageStaff" && estimateAcceptingStatuses.includes(claim.status);

  const refresh = (estimate: RepairEstimate) => {
    setNotice(estimate);
    void queryClient.invalidateQueries({ queryKey: ["claim", String(claim.id)] });
  };

  return (
    <Card>
      <CardContent>
        <Typography variant="h6" gutterBottom>
          ใบประเมินราคาซ่อม
        </Typography>
        {estimates.isPending && <CircularProgress size={20} />}
        {estimates.isError && <Alert severity="error">โหลดใบประเมินไม่สำเร็จ</Alert>}
        {estimates.data?.length === 0 && <Typography color="text.secondary">ยังไม่มีใบประเมิน</Typography>}
        <Stack spacing={2}>
          {notice && (
            <Alert severity={notice.exceedsCoverageLimit ? "warning" : "success"} onClose={() => setNotice(null)}>
              ส่งใบประเมินฉบับที่ {notice.revision} แล้ว ยอดประเมิน {money(notice.estimatedAmount)} บาท
              {notice.exceedsCoverageLimit && ` ซึ่งเกินวงเงินคุ้มครอง ${money(notice.coverageLimit)} บาท`}
            </Alert>
          )}
          {estimates.data
            ?.slice()
            .reverse()
            .map((e) => <RevisionCard key={e.id} estimate={e} />)}
          {canSubmit && <EstimateForm claim={claim} onSubmitted={refresh} />}
        </Stack>
      </CardContent>
    </Card>
  );
}

"use client";

import { useState } from "react";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Dialog from "@mui/material/Dialog";
import DialogActions from "@mui/material/DialogActions";
import DialogContent from "@mui/material/DialogContent";
import DialogTitle from "@mui/material/DialogTitle";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { availableRepairActions, describeRepairError, proxyFetch, type ClaimDetail, type Invoice } from "@/lib/claims";

const money = (n: number) => n.toLocaleString("th-TH", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** Garage Staff: start repair, mark repaired, submit the Invoice. Insurance Adjuster: confirm payment. Only the actions role and status allow. */
export function RepairInvoiceActions({ claim, role }: { claim: ClaimDetail; role: string | undefined }) {
  const actions = availableRepairActions(role, claim.status);
  const queryClient = useQueryClient();
  const [open, setOpen] = useState<"invoice" | "pay" | null>(null);
  const [amount, setAmount] = useState("");
  const [text, setText] = useState("");

  const key = ["claim", String(claim.id)];
  const reset = () => {
    setOpen(null);
    setAmount("");
    setText("");
  };

  const action = useMutation({
    mutationFn: async (request: { path: string; body?: unknown }) => {
      const result = await proxyFetch<ClaimDetail | Invoice>(`/claims/${claim.id}/${request.path}`, {
        method: "POST",
        body: request.body === undefined ? undefined : JSON.stringify(request.body),
      });
      // Submitting the Invoice answers with the Invoice; reload the Claim to get its new status.
      return request.path === "invoice" ? await proxyFetch<ClaimDetail>(`/claims/${claim.id}`) : (result as ClaimDetail);
    },
    onSuccess: (updated) => {
      queryClient.setQueryData(key, updated);
      void queryClient.invalidateQueries({ queryKey: key });
      reset();
    },
  });

  if (actions.length === 0) return null;

  const amountNumber = Number(amount);
  const amountValid =
    amount.trim() !== "" && Number.isFinite(amountNumber) && amountNumber > 0 && Math.round(amountNumber * 100) / 100 === amountNumber;

  const confirm = () => {
    if (open === "invoice") action.mutate({ path: "invoice", body: { actualAmount: amountNumber, note: text.trim() || undefined } });
    else if (open === "pay") action.mutate({ path: "pay", body: { reason: text.trim() || undefined } });
  };

  return (
    <>
      {actions.includes("startRepair") && (
        <Button variant="contained" disabled={action.isPending} onClick={() => action.mutate({ path: "start-repair" })}>
          เริ่มซ่อม
        </Button>
      )}
      {actions.includes("markRepaired") && (
        <Button variant="contained" disabled={action.isPending} onClick={() => action.mutate({ path: "mark-repaired" })}>
          ซ่อมเสร็จ
        </Button>
      )}
      {actions.includes("submitInvoice") && (
        <Button variant="contained" color="success" onClick={() => setOpen("invoice")}>
          ส่งใบเรียกเก็บ (Invoice)
        </Button>
      )}
      {actions.includes("confirmPayment") && (
        <Button variant="contained" color="success" onClick={() => setOpen("pay")}>
          ยืนยันการจ่าย
        </Button>
      )}
      {!open && action.isError && <Alert severity="error">{describeRepairError(action.error)}</Alert>}

      <Dialog
        open={open !== null}
        onClose={() => {
          reset();
          action.reset();
        }}
        fullWidth
        maxWidth="sm"
      >
        <DialogTitle>{open === "invoice" ? "ส่งใบเรียกเก็บ (Invoice)" : "ยืนยันการจ่าย"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            {open === "invoice" && (
              <>
                {claim.approvedAmount !== null && (
                  <Typography>ยอดอนุมัติ (Approved Amount): {money(claim.approvedAmount)} บาท</Typography>
                )}
                <TextField
                  label="ค่าซ่อมจริง (บาท)"
                  value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                  slotProps={{ htmlInput: { inputMode: "decimal" } }}
                  error={amount.trim() !== "" && !amountValid}
                  helperText={amount.trim() !== "" && !amountValid ? "ต้องมากกว่า 0 และมีทศนิยมไม่เกิน 2 ตำแหน่ง" : undefined}
                  autoFocus
                />
              </>
            )}
            <TextField
              label={open === "invoice" ? "หมายเหตุ (ไม่บังคับ)" : "หมายเหตุการจ่าย (ไม่บังคับ)"}
              value={text}
              onChange={(e) => setText(e.target.value)}
              multiline
              minRows={2}
              slotProps={{ htmlInput: { maxLength: 2000 } }}
            />
            {action.isError && <Alert severity="error">{describeRepairError(action.error)}</Alert>}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button
            onClick={() => {
              reset();
              action.reset();
            }}
          >
            ยกเลิก
          </Button>
          <Button variant="contained" disabled={(open === "invoice" && !amountValid) || action.isPending} onClick={confirm}>
            ยืนยัน
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}

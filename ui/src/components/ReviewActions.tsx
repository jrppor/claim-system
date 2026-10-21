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
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  availableReviewActions,
  describeReviewError,
  proxyFetch,
  type ClaimDetail,
  type RepairEstimate,
  type ReviewAction,
} from "@/lib/claims";

const money = (n: number) => n.toLocaleString("th-TH", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const dialogTitles: Record<Exclude<ReviewAction, "pickup">, string> = {
  approve: "อนุมัติเคลม",
  reject: "ปฏิเสธเคลม",
  requestChanges: "ขอให้แก้ไขใบประเมิน",
};

/** Buttons for the Insurance Adjuster's decisions. Rendered only for the actions the role and the Claim's status allow. */
export function ReviewActions({ claim, role }: { claim: ClaimDetail; role: string | undefined }) {
  const actions = availableReviewActions(role, claim.status);
  const queryClient = useQueryClient();
  const [open, setOpen] = useState<Exclude<ReviewAction, "pickup"> | null>(null);
  const [amount, setAmount] = useState("");
  const [text, setText] = useState("");

  // Same query as the estimate section, so the approval can name the latest revision it was reviewed against.
  const estimates = useQuery({
    queryKey: ["claim", String(claim.id), "estimates"],
    queryFn: () => proxyFetch<RepairEstimate[]>(`/claims/${claim.id}/estimates`),
    enabled: actions.includes("approve"),
  });
  const latest = estimates.data?.find((e) => e.isLatest);

  const close = () => {
    setOpen(null);
    setAmount("");
    setText("");
    action.reset();
  };

  const action = useMutation({
    mutationFn: (request: { path: string; body?: unknown }) =>
      proxyFetch<ClaimDetail>(`/claims/${claim.id}/${request.path}`, {
        method: "POST",
        body: request.body === undefined ? undefined : JSON.stringify(request.body),
      }),
    onSuccess: (updated) => {
      queryClient.setQueryData(["claim", String(claim.id)], updated);
      void queryClient.invalidateQueries({ queryKey: ["claim", String(claim.id)] });
      setOpen(null);
      setAmount("");
      setText("");
    },
  });

  if (actions.length === 0) return null;

  const amountNumber = Number(amount);
  const amountValid = amount.trim() !== "" && Number.isFinite(amountNumber) && amountNumber > 0 && amountNumber <= claim.policy.coverageLimit;
  const textValid = text.trim() !== "";
  const canConfirm = open === "approve" ? amountValid && textValid && !!latest : textValid;

  const confirm = () => {
    if (open === "approve")
      action.mutate({ path: "approve", body: { approvedAmount: amountNumber, reason: text.trim(), revision: latest?.revision } });
    else if (open === "reject") action.mutate({ path: "reject", body: { reason: text.trim() } });
    else if (open === "requestChanges") action.mutate({ path: "request-changes", body: { comments: text.trim() } });
  };

  return (
    <>
      {actions.includes("pickup") && (
        <Button variant="contained" disabled={action.isPending} onClick={() => action.mutate({ path: "review" })}>
          รับเรื่องพิจารณา
        </Button>
      )}
      {actions.includes("approve") && (
        <Button variant="contained" color="success" onClick={() => setOpen("approve")}>
          อนุมัติ
        </Button>
      )}
      {actions.includes("requestChanges") && (
        <Button variant="outlined" color="warning" onClick={() => setOpen("requestChanges")}>
          ขอให้แก้ไข
        </Button>
      )}
      {actions.includes("reject") && (
        <Button variant="outlined" color="error" onClick={() => setOpen("reject")}>
          ปฏิเสธ
        </Button>
      )}
      {!open && action.isError && <Alert severity="error">{describeReviewError(action.error)}</Alert>}

      <Dialog open={open !== null} onClose={close} fullWidth maxWidth="sm">
        <DialogTitle>{open ? dialogTitles[open] : ""}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            {open === "approve" && (
              <>
                {latest ? (
                  <Typography>
                    ใบประเมินฉบับล่าสุด (ฉบับที่ {latest.revision}) ยอดประเมิน {money(latest.estimatedAmount)} บาท · วงเงินคุ้มครอง{" "}
                    {money(claim.policy.coverageLimit)} บาท
                  </Typography>
                ) : (
                  <Alert severity="warning">เคลมนี้ยังไม่มีใบประเมินราคาซ่อม จึงอนุมัติไม่ได้</Alert>
                )}
                <TextField
                  label="ยอดอนุมัติ (บาท)"
                  value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                  slotProps={{ htmlInput: { inputMode: "decimal" } }}
                  error={amount.trim() !== "" && !amountValid}
                  helperText={
                    amount.trim() !== "" && !amountValid
                      ? `ต้องมากกว่า 0 และไม่เกินวงเงินคุ้มครอง ${money(claim.policy.coverageLimit)} บาท`
                      : "ส่วนต่างจากยอดประเมินเป็นความรับผิดชอบของลูกค้า"
                  }
                  autoFocus
                />
              </>
            )}
            <TextField
              label={open === "requestChanges" ? "ความเห็นถึงอู่" : "เหตุผล"}
              value={text}
              onChange={(e) => setText(e.target.value)}
              multiline
              minRows={3}
              slotProps={{ htmlInput: { maxLength: 2000 } }}
              autoFocus={open !== "approve"}
            />
            {action.isError && <Alert severity="error">{describeReviewError(action.error)}</Alert>}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={close}>ยกเลิก</Button>
          <Button variant="contained" disabled={!canConfirm || action.isPending} onClick={confirm}>
            ยืนยัน
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}

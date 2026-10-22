"use client";

import { useState } from "react";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Dialog from "@mui/material/Dialog";
import DialogActions from "@mui/material/DialogActions";
import DialogContent from "@mui/material/DialogContent";
import DialogTitle from "@mui/material/DialogTitle";
import TextField from "@mui/material/TextField";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ifMatch, isVersionConflict, ProxyError, proxyFetch, versionConflictMessage, type ClaimDetail, type ClaimStatus } from "@/lib/claims";

/** Statuses from which Garage Staff may cancel (mirrors ClaimLifecycle: anything before Approved). */
const cancellableStatuses: ClaimStatus[] = ["Draft", "Submitted", "UnderReview", "ChangesRequested"];

export const canCancelClaim = (role: string | undefined, status: ClaimStatus) =>
  role === "GarageStaff" && cancellableStatuses.includes(status);

function describeCancelError(error: unknown): string {
  if (isVersionConflict(error)) return versionConflictMessage;
  if (error instanceof ProxyError) {
    if (error.status === 400) return "กรุณากรอกเหตุผลในการยกเลิก";
    if (error.status === 409) return "ยกเลิกไม่ได้ เคลมนี้อนุมัติแล้วหรือสถานะเปลี่ยนไปแล้ว กรุณารีเฟรชหน้า";
    if (error.status === 403) return "คุณไม่มีสิทธิ์ทำรายการนี้";
    if (error.status === 404) return "ไม่พบเคลมนี้";
  }
  return "ทำรายการไม่สำเร็จ กรุณาลองใหม่อีกครั้ง";
}

/** Cancel button and reason dialog; renders nothing unless the role and the Claim's status allow cancelling. */
export function CancelClaimAction({ claim, role }: { claim: ClaimDetail; role: string | undefined }) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");

  const cancel = useMutation({
    mutationFn: () =>
      proxyFetch<ClaimDetail>(`/claims/${claim.id}/cancel`, { method: "POST", body: JSON.stringify({ reason: reason.trim() }), ...ifMatch(claim) }),
    onSuccess: (updated) => {
      queryClient.setQueryData(["claim", String(claim.id)], updated);
      void queryClient.invalidateQueries({ queryKey: ["claim", String(claim.id)] });
      setOpen(false);
      setReason("");
    },
  });

  if (!canCancelClaim(role, claim.status)) return null;

  const close = () => {
    setOpen(false);
    setReason("");
    cancel.reset();
  };

  return (
    <>
      <Button variant="outlined" color="error" onClick={() => setOpen(true)}>
        ยกเลิกเคลม
      </Button>
      <Dialog open={open} onClose={close} fullWidth maxWidth="sm">
        <DialogTitle>ยกเลิกเคลม {claim.claimNumber}</DialogTitle>
        <DialogContent>
          <TextField
            label="เหตุผลในการยกเลิก"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            multiline
            minRows={3}
            fullWidth
            autoFocus
            sx={{ mt: 1 }}
            slotProps={{ htmlInput: { maxLength: 2000 } }}
          />
          {cancel.isError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              {describeCancelError(cancel.error)}
            </Alert>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={close}>ไม่ยกเลิก</Button>
          <Button variant="contained" color="error" disabled={reason.trim() === "" || cancel.isPending} onClick={() => cancel.mutate()}>
            ยืนยันการยกเลิก
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}

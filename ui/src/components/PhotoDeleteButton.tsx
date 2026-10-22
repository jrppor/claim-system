"use client";

import { useState } from "react";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Dialog from "@mui/material/Dialog";
import DialogActions from "@mui/material/DialogActions";
import DialogContent from "@mui/material/DialogContent";
import DialogContentText from "@mui/material/DialogContentText";
import DialogTitle from "@mui/material/DialogTitle";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  canChangePhotos,
  describePhotoDeleteError,
  ifMatch,
  proxyFetch,
  type ClaimDetail,
  type ClaimPhoto,
} from "@/lib/claims";

/** Delete button with a Thai confirm dialog. Renders nothing unless the role and the Claim's status allow deleting photos. */
export function PhotoDeleteButton({ claim, photo, role }: { claim: ClaimDetail; photo: ClaimPhoto; role: string | undefined }) {
  const queryClient = useQueryClient();
  const [confirming, setConfirming] = useState(false);

  const remove = useMutation({
    mutationFn: () => proxyFetch<{ version: string }>(`/claims/${claim.id}/photos/${photo.id}`, { method: "DELETE", ...ifMatch(claim) }),
    onSuccess: ({ version }) => {
      setConfirming(false);
      // The delete gives the Claim a new version; keep the cached Claim current so the next change does not look stale.
      queryClient.setQueryData<ClaimDetail>(["claim", String(claim.id)], (old) => (old ? { ...old, version } : old));
      void queryClient.invalidateQueries({ queryKey: ["claim", String(claim.id), "photos"] });
      void queryClient.invalidateQueries({ queryKey: ["claim", String(claim.id), "history"] });
    },
    onError: () => void queryClient.invalidateQueries({ queryKey: ["claim", String(claim.id)] }),
  });

  if (!canChangePhotos(role, claim.status)) return null;

  return (
    <>
      <Button size="small" color="error" onClick={() => { remove.reset(); setConfirming(true); }}>
        ลบ
      </Button>
      <Dialog open={confirming} onClose={() => !remove.isPending && setConfirming(false)}>
        <DialogTitle>ลบรูปนี้?</DialogTitle>
        <DialogContent>
          <DialogContentText>
            รูป “{photo.originalFileName}” จะถูกลบออกจากเคลมนี้อย่างถาวร และบันทึกลงประวัติเคลม
          </DialogContentText>
          {remove.isError && <Alert severity="error" sx={{ mt: 2 }}>{describePhotoDeleteError(remove.error)}</Alert>}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirming(false)} disabled={remove.isPending}>ยกเลิก</Button>
          <Button color="error" variant="contained" onClick={() => remove.mutate()} disabled={remove.isPending}>
            {remove.isPending ? "กำลังลบ..." : "ลบรูป"}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}

"use client";

import { useState } from "react";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardActionArea from "@mui/material/CardActionArea";
import CardContent from "@mui/material/CardContent";
import CircularProgress from "@mui/material/CircularProgress";
import Dialog from "@mui/material/Dialog";
import DialogContent from "@mui/material/DialogContent";
import DialogActions from "@mui/material/DialogActions";
import DialogTitle from "@mui/material/DialogTitle";
import TextField from "@mui/material/TextField";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  canChangePhotos,
  describePhotoUploadError,
  maxPhotoCaptionLength,
  maxPhotosPerClaim,
  ifMatch,
  proxyBlob,
  proxyFetch,
  proxyUpload,
  type ClaimDetail,
  type ClaimPhoto,
} from "@/lib/claims";
import { PhotoDeleteButton } from "@/components/PhotoDeleteButton";

/** One photo's bytes, fetched through the authenticated proxy and shown from an object URL. */
function PhotoImage({ claimId, photo, height }: { claimId: number; photo: ClaimPhoto; height: number | string }) {
  const file = useQuery({
    queryKey: ["claim", String(claimId), "photo", photo.id, "file"],
    queryFn: async () => URL.createObjectURL(await proxyBlob(`/claims/${claimId}/photos/${photo.id}/file`)),
    staleTime: Infinity,
    gcTime: Infinity,
  });
  if (file.isPending) return <CircularProgress size={20} />;
  if (file.isError) return <Typography color="error">โหลดรูปไม่สำเร็จ</Typography>;
  return (
    <Box
      component="img"
      src={file.data}
      alt={photo.caption ?? photo.originalFileName}
      sx={{ display: "block", width: "100%", height, objectFit: height === "auto" ? "contain" : "cover" }}
    />
  );
}

/** Damage photos of a Claim: gallery, click for the full photo, and (Garage Staff, before approval) upload. */
export function ClaimPhotoSection({ claim, role }: { claim: ClaimDetail; role: string | undefined }) {
  const queryClient = useQueryClient();
  const [viewing, setViewing] = useState<ClaimPhoto | null>(null);
  /** Files chosen but not sent yet, each with the caption typed for it. */
  const [pending, setPending] = useState<{ file: File; caption: string }[]>([]);
  const [chooseError, setChooseError] = useState<string | null>(null);
  const photos = useQuery({
    queryKey: ["claim", String(claim.id), "photos"],
    queryFn: () => proxyFetch<ClaimPhoto[]>(`/claims/${claim.id}/photos`),
  });

  const upload = useMutation({
    mutationFn: (chosen: { file: File; caption: string }[]) => {
      const form = new FormData();
      // "file" and "caption" repeat in the same order; the API pairs them by position.
      for (const { file, caption } of chosen) {
        form.append("file", file);
        form.append("caption", caption);
      }
      return proxyUpload<ClaimPhoto | ClaimPhoto[]>(`/claims/${claim.id}/photos`, form, ifMatch(claim));
    },
    onSuccess: (created) => {
      setPending([]);
      const photo = Array.isArray(created) ? created[0] : created;
      // The upload moves the Claim to a new version; keep the cached Claim current so the next change does not look stale.
      queryClient.setQueryData<ClaimDetail>(["claim", String(claim.id)], (old) => (old && photo?.version ? { ...old, version: photo.version } : old));
      void queryClient.invalidateQueries({ queryKey: ["claim", String(claim.id), "photos"] });
      void queryClient.invalidateQueries({ queryKey: ["claim", String(claim.id), "history"] });
    },
    onError: () => void queryClient.invalidateQueries({ queryKey: ["claim", String(claim.id)] }),
  });

  const canUpload = canChangePhotos(role, claim.status);
  const list = photos.data ?? [];
  const room = maxPhotosPerClaim - list.length;

  const choose = (files: File[]) => {
    if (files.length === 0) return;
    if (files.length > room) {
      setChooseError(
        room <= 0
          ? `เคลมนี้มีรูปครบ ${maxPhotosPerClaim} รูปแล้ว แนบเพิ่มไม่ได้`
          : `เลือกได้อีกไม่เกิน ${room} รูป (เคลมหนึ่งมีได้ ${maxPhotosPerClaim} รูป) แต่เลือกมา ${files.length} รูป`,
      );
      return;
    }
    setChooseError(null);
    upload.reset();
    setPending(files.map((file) => ({ file, caption: "" })));
  };
  if (!canUpload && !photos.isPending && !photos.isError && list.length === 0) return null;

  return (
    <Card>
      <CardContent>
        <Stack direction="row" spacing={2} sx={{ alignItems: "center", mb: 1 }}>
          <Typography variant="h6" sx={{ flexGrow: 1 }}>
            รูปความเสียหาย
          </Typography>
          {canUpload && (
            <Button component="label" variant="contained" disabled={upload.isPending}>
              แนบรูป
              <input
                hidden
                multiple
                type="file"
                accept="image/jpeg,image/png,image/webp"
                onChange={(e) => {
                  const files = Array.from(e.target.files ?? []);
                  e.target.value = "";
                  choose(files);
                }}
              />
            </Button>
          )}
        </Stack>
        {chooseError && <Alert severity="warning" sx={{ mb: 1 }}>{chooseError}</Alert>}
        {upload.isError && pending.length === 0 && <Alert severity="error" sx={{ mb: 1 }}>{describePhotoUploadError(upload.error)}</Alert>}
        {photos.isPending && <CircularProgress size={20} />}
        {photos.isError && <Alert severity="error">โหลดรูปความเสียหายไม่สำเร็จ</Alert>}
        {photos.data && list.length === 0 && <Typography color="text.secondary">ยังไม่มีรูปความเสียหาย</Typography>}
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(160px, 1fr))", gap: 2 }}>
          {list.map((p) => (
            <Card key={p.id} variant="outlined">
              <CardActionArea onClick={() => setViewing(p)} aria-label={`ดูรูป ${p.originalFileName}`}>
                <PhotoImage claimId={claim.id} photo={p} height={120} />
              </CardActionArea>
              <Typography variant="caption" color="text.secondary" sx={{ display: "block", px: 1, py: 0.5, overflowWrap: "anywhere" }}>
                {p.caption ?? p.originalFileName}
              </Typography>
              <PhotoDeleteButton claim={claim} photo={p} role={role} />
            </Card>
          ))}
        </Box>
      </CardContent>
      <Dialog open={pending.length > 0} onClose={() => !upload.isPending && setPending([])} maxWidth="sm" fullWidth>
        <DialogTitle>แนบรูป {pending.length} ไฟล์</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            {upload.isError && <Alert severity="error">{describePhotoUploadError(upload.error)}</Alert>}
            {pending.map((item, index) => (
              <TextField
                key={`${item.file.name}-${index}`}
                label={`คำอธิบาย: ${item.file.name} (ไม่บังคับ)`}
                value={item.caption}
                disabled={upload.isPending}
                slotProps={{ htmlInput: { maxLength: maxPhotoCaptionLength } }}
                onChange={(e) => setPending((all) => all.map((x, i) => (i === index ? { ...x, caption: e.target.value } : x)))}
                size="small"
                fullWidth
              />
            ))}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPending([])} disabled={upload.isPending}>
            ยกเลิก
          </Button>
          <Button variant="contained" onClick={() => upload.mutate(pending)} disabled={upload.isPending}>
            {upload.isPending ? "กำลังอัปโหลด..." : "อัปโหลด"}
          </Button>
        </DialogActions>
      </Dialog>
      <Dialog open={viewing !== null} onClose={() => setViewing(null)} maxWidth="lg" fullWidth>
        <DialogTitle>{viewing?.originalFileName}</DialogTitle>
        <DialogContent>
          {viewing && <PhotoImage claimId={claim.id} photo={viewing} height="auto" />}
          {viewing?.caption && <Typography sx={{ mt: 1 }}>{viewing.caption}</Typography>}
        </DialogContent>
      </Dialog>
    </Card>
  );
}

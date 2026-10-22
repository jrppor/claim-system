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
import DialogTitle from "@mui/material/DialogTitle";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  canChangePhotos,
  describePhotoUploadError,
  ifMatch,
  proxyBlob,
  proxyFetch,
  proxyUpload,
  type ClaimDetail,
  type ClaimPhoto,
} from "@/lib/claims";

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
  const photos = useQuery({
    queryKey: ["claim", String(claim.id), "photos"],
    queryFn: () => proxyFetch<ClaimPhoto[]>(`/claims/${claim.id}/photos`),
  });

  const upload = useMutation({
    mutationFn: (file: File) => {
      const form = new FormData();
      form.append("file", file);
      return proxyUpload<ClaimPhoto>(`/claims/${claim.id}/photos`, form, ifMatch(claim));
    },
    onSuccess: (photo) => {
      // The upload moves the Claim to a new version; keep the cached Claim current so the next change does not look stale.
      queryClient.setQueryData<ClaimDetail>(["claim", String(claim.id)], (old) => (old && photo.version ? { ...old, version: photo.version } : old));
      void queryClient.invalidateQueries({ queryKey: ["claim", String(claim.id), "photos"] });
      void queryClient.invalidateQueries({ queryKey: ["claim", String(claim.id), "history"] });
    },
    onError: () => void queryClient.invalidateQueries({ queryKey: ["claim", String(claim.id)] }),
  });

  const canUpload = canChangePhotos(role, claim.status);
  const list = photos.data ?? [];
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
              {upload.isPending ? "กำลังอัปโหลด..." : "แนบรูป"}
              <input
                hidden
                type="file"
                accept="image/jpeg,image/png,image/webp"
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  e.target.value = "";
                  if (file) upload.mutate(file);
                }}
              />
            </Button>
          )}
        </Stack>
        {upload.isError && <Alert severity="error" sx={{ mb: 1 }}>{describePhotoUploadError(upload.error)}</Alert>}
        {photos.isPending && <CircularProgress size={20} />}
        {photos.isError && <Alert severity="error">โหลดรูปความเสียหายไม่สำเร็จ</Alert>}
        {photos.data && list.length === 0 && <Typography color="text.secondary">ยังไม่มีรูปความเสียหาย</Typography>}
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(160px, 1fr))", gap: 2 }}>
          {list.map((p) => (
            <Card key={p.id} variant="outlined">
              <CardActionArea onClick={() => setViewing(p)} aria-label={`ดูรูป ${p.originalFileName}`}>
                <PhotoImage claimId={claim.id} photo={p} height={120} />
              </CardActionArea>
              <Typography variant="caption" color="text.secondary" noWrap sx={{ display: "block", px: 1, py: 0.5 }}>
                {p.originalFileName}
              </Typography>
            </Card>
          ))}
        </Box>
      </CardContent>
      <Dialog open={viewing !== null} onClose={() => setViewing(null)} maxWidth="lg" fullWidth>
        <DialogTitle>{viewing?.originalFileName}</DialogTitle>
        <DialogContent>{viewing && <PhotoImage claimId={claim.id} photo={viewing} height="auto" />}</DialogContent>
      </Dialog>
    </Card>
  );
}

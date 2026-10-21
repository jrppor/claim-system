"use client";

import { useParams } from "next/navigation";
import Alert from "@mui/material/Alert";
import CircularProgress from "@mui/material/CircularProgress";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { useQuery } from "@tanstack/react-query";
import { ClaimForm } from "@/components/ClaimForm";
import { proxyFetch, type ClaimDetail } from "@/lib/claims";
import { useSession } from "@/lib/useSession";

export default function EditClaimPage() {
  const { id } = useParams<{ id: string }>();
  const { data: user } = useSession();
  const claim = useQuery({
    queryKey: ["claim", id],
    queryFn: () => proxyFetch<ClaimDetail>(`/claims/${id}`),
  });

  return (
    <Stack spacing={3}>
      <Typography variant="h4" component="h1">
        แก้ไขเคลม {claim.data?.claimNumber}
      </Typography>
      {claim.isPending && <CircularProgress size={24} />}
      {claim.isError && <Alert severity="error">ไม่พบเคลมนี้</Alert>}
      {claim.isSuccess && user && user.role !== "GarageStaff" && (
        <Alert severity="warning">เฉพาะพนักงานอู่เท่านั้นที่แก้ไขเคลมได้</Alert>
      )}
      {claim.isSuccess && user?.role === "GarageStaff" && claim.data.status !== "Draft" && (
        <Alert severity="warning">แก้ไขได้เฉพาะเคลมที่อยู่ในสถานะแบบร่างเท่านั้น</Alert>
      )}
      {claim.isSuccess && user?.role === "GarageStaff" && claim.data.status === "Draft" && (
        <ClaimForm claim={claim.data} />
      )}
    </Stack>
  );
}

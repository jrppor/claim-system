"use client";

import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { ClaimForm } from "@/components/ClaimForm";

// Garage Staff only (src/lib/roles.ts routeRules).
export default function NewClaimPage() {
  return (
    <Stack spacing={3}>
      <Typography variant="h4" component="h1">
        สร้างเคลมใหม่
      </Typography>
      <ClaimForm />
    </Stack>
  );
}

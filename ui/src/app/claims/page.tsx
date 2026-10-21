"use client";

import { Suspense, useEffect, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import MenuItem from "@mui/material/MenuItem";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableContainer from "@mui/material/TableContainer";
import TableHead from "@mui/material/TableHead";
import TablePagination from "@mui/material/TablePagination";
import TableRow from "@mui/material/TableRow";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { claimStatusLabels, type ClaimStatus } from "@/lib/claims";
import { fetchClaimList, formatDateTime } from "@/lib/claimList";
import { useSession } from "@/lib/useSession";

const allStatuses = Object.keys(claimStatusLabels) as ClaimStatus[];

export default function ClaimListPage() {
  return (
    <Suspense fallback={<CircularProgress size={24} />}>
      <ClaimList />
    </Suspense>
  );
}

function ClaimList() {
  const { data: user } = useSession();
  const initialStatus = useSearchParams().get("status");
  const [searchText, setSearchText] = useState("");
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState<ClaimStatus | "">(
    allStatuses.includes(initialStatus as ClaimStatus) ? (initialStatus as ClaimStatus) : "",
  );
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(20);

  // Debounce typing so each keystroke does not hit the API.
  useEffect(() => {
    const t = setTimeout(() => {
      setSearch(searchText);
      setPage(0);
    }, 300);
    return () => clearTimeout(t);
  }, [searchText]);

  const list = useQuery({
    queryKey: ["claims", search, status, page, pageSize],
    queryFn: () => fetchClaimList({ search, status, page: page + 1, pageSize }),
    placeholderData: keepPreviousData,
  });

  // The Adjuster never sees a Draft, so the filter does not offer it.
  const statuses = user?.role === "InsuranceAdjuster" ? allStatuses.filter((s) => s !== "Draft") : allStatuses;

  return (
    <Stack spacing={3}>
      <Stack direction="row" sx={{ alignItems: "center" }}>
        <Typography variant="h4" component="h1" sx={{ flexGrow: 1 }}>
          รายการเคลม
        </Typography>
        {user?.role === "GarageStaff" && (
          <Button variant="contained" component={Link} href="/claims/new">
            สร้างเคลม
          </Button>
        )}
      </Stack>

      <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
        <TextField
          label="ค้นหา (เลขเคลม ทะเบียน หรือชื่อลูกค้า)"
          value={searchText}
          onChange={(e) => setSearchText(e.target.value)}
          fullWidth
        />
        <TextField
          select
          label="สถานะ"
          value={status}
          onChange={(e) => {
            setStatus(e.target.value as ClaimStatus | "");
            setPage(0);
          }}
          sx={{ minWidth: 220 }}
        >
          <MenuItem value="">ทุกสถานะ</MenuItem>
          {statuses.map((s) => (
            <MenuItem key={s} value={s}>
              {claimStatusLabels[s]}
            </MenuItem>
          ))}
        </TextField>
      </Stack>

      {list.isPending && <CircularProgress size={24} />}
      {list.isError && <Alert severity="error">โหลดรายการเคลมไม่สำเร็จ</Alert>}
      {list.data && (
        <Paper>
          <TableContainer>
            <Table>
              <TableHead>
                <TableRow>
                  <TableCell>เลขเคลม</TableCell>
                  <TableCell>ทะเบียน</TableCell>
                  <TableCell>ลูกค้า</TableCell>
                  <TableCell>สถานะ</TableCell>
                  <TableCell>อัปเดตล่าสุด</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {list.data.items.map((c) => (
                  <TableRow key={c.id} hover>
                    <TableCell>
                      <Link href={`/claims/${c.id}`}>{c.claimNumber}</Link>
                    </TableCell>
                    <TableCell>{c.vehiclePlate ?? "-"}</TableCell>
                    <TableCell>{c.customerName}</TableCell>
                    <TableCell>
                      <Chip size="small" label={claimStatusLabels[c.status]} color={c.status === "Draft" ? "default" : "primary"} />
                    </TableCell>
                    <TableCell>{formatDateTime(c.updatedAt)}</TableCell>
                  </TableRow>
                ))}
                {list.data.items.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={5} align="center">
                      ไม่พบเคลมที่ตรงกับเงื่อนไข
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </TableContainer>
          <TablePagination
            component="div"
            count={list.data.total}
            page={page}
            rowsPerPage={pageSize}
            rowsPerPageOptions={[10, 20, 50]}
            labelRowsPerPage="แถวต่อหน้า"
            onPageChange={(_, p) => setPage(p)}
            onRowsPerPageChange={(e) => {
              setPageSize(Number(e.target.value));
              setPage(0);
            }}
          />
        </Paper>
      )}
    </Stack>
  );
}

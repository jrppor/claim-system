import { proxyFetch, type ClaimStatus } from "@/lib/claims";

export interface ClaimListItem {
  id: number;
  claimNumber: string;
  status: ClaimStatus;
  policyNumber: string;
  vehiclePlate: string | null;
  customerName: string;
  customerEmail: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface ClaimPage {
  items: ClaimListItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface DashboardSummary {
  countsByStatus: Partial<Record<ClaimStatus, number>>;
  total: number;
}

export interface ClaimListParams {
  search: string;
  status: ClaimStatus | "";
  page: number;
  pageSize: number;
}

export function fetchClaimList({ search, status, page, pageSize }: ClaimListParams) {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (search.trim()) query.set("search", search.trim());
  if (status) query.set("status", status);
  return proxyFetch<ClaimPage>(`/claims?${query}`);
}

export const fetchDashboardSummary = () => proxyFetch<DashboardSummary>("/dashboard/summary");

// The API sends UTC without a zone suffix.
const dateFormat = new Intl.DateTimeFormat("th-TH", { dateStyle: "medium", timeStyle: "short" });
export const formatDateTime = (iso: string) => dateFormat.format(new Date(iso.endsWith("Z") ? iso : `${iso}Z`));

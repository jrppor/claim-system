import type { ProblemDetails } from "@/lib/api";

export interface CustomerInfo {
  id: number;
  fullName: string;
  email: string;
}

export interface PolicyInfo {
  id: number;
  policyNumber: string;
  customer: CustomerInfo;
  plateNumber: string;
  vehicleMake: string;
  vehicleModel: string;
  coverageLimit: number;
}

export type ClaimStatus =
  | "Draft"
  | "Submitted"
  | "UnderReview"
  | "ChangesRequested"
  | "Approved"
  | "Rejected"
  | "InRepair"
  | "Repaired"
  | "InvoiceSubmitted"
  | "Paid"
  | "Cancelled";

export const claimStatusLabels: Record<ClaimStatus, string> = {
  Draft: "แบบร่าง",
  Submitted: "ส่งแล้ว",
  UnderReview: "กำลังพิจารณา",
  ChangesRequested: "ขอให้แก้ไข",
  Approved: "อนุมัติ",
  Rejected: "ปฏิเสธ",
  InRepair: "กำลังซ่อม",
  Repaired: "ซ่อมเสร็จ",
  InvoiceSubmitted: "ส่งใบเรียกเก็บแล้ว",
  Paid: "จ่ายแล้ว",
  Cancelled: "ยกเลิก",
};

export interface ClaimDetail {
  id: number;
  claimNumber: string;
  status: ClaimStatus;
  policy: PolicyInfo;
  vehiclePlate: string | null;
  vehicleMake: string | null;
  vehicleModel: string | null;
  customerEmail: string | null;
  damageDescription: string | null;
  createdAt: string;
  updatedAt: string;
}

/** One line of Claim History: who moved the Claim between which statuses, when and why. */
export interface ClaimHistoryEntry {
  fromStatus: ClaimStatus | null;
  toStatus: ClaimStatus;
  actorUserId: number;
  actorName: string;
  occurredAt: string;
  reason: string | null;
}

const fieldLabels: Record<string, string> = {
  vehiclePlate: "ทะเบียนรถ",
  vehicleMake: "ยี่ห้อรถ",
  vehicleModel: "รุ่นรถ",
  customerEmail: "อีเมลลูกค้า",
  damageDescription: "รายละเอียดความเสียหาย",
  reason: "เหตุผล",
};

/** Thai message for a failed status change, naming the missing details when the API lists them. */
export function describeStatusChangeError(error: unknown): string {
  if (error instanceof ProxyError) {
    if (error.status === 400 && error.problem?.errors) {
      const missing = Object.keys(error.problem.errors).map((f) => fieldLabels[f] ?? f);
      return `ส่งไม่ได้ ข้อมูลยังไม่ครบ: ${missing.join(", ")}`;
    }
    if (error.status === 409) return "เปลี่ยนสถานะไม่ได้ เพราะสถานะของเคลมนี้ไม่ได้อนุญาตให้ทำขั้นตอนนี้";
    if (error.status === 403) return "คุณไม่มีสิทธิ์ทำรายการนี้";
    if (error.status === 404) return "ไม่พบเคลมนี้";
  }
  return "ทำรายการไม่สำเร็จ กรุณาลองใหม่อีกครั้ง";
}

export interface ClaimInput {
  policyId: number;
  vehiclePlate: string;
  vehicleMake: string;
  vehicleModel: string;
  customerEmail: string;
  damageDescription: string;
}

export class ProxyError extends Error {
  constructor(
    public readonly status: number,
    public readonly problem?: ProblemDetails,
  ) {
    super(problem?.detail ?? problem?.title ?? `API error ${status}`);
  }
}

/** Calls the Claim API through the authenticated Next proxy (the browser never holds the token). */
export async function proxyFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api/proxy${path}`, {
    cache: "no-store",
    ...init,
    headers: { "Content-Type": "application/json", ...init?.headers },
  });
  if (!response.ok) {
    const problem = (await response.json().catch(() => undefined)) as ProblemDetails | undefined;
    throw new ProxyError(response.status, problem);
  }
  return (await response.json()) as T;
}

/** Thai message for a failed save (the API's own messages are English). */
export function describeSaveError(error: unknown): string {
  if (error instanceof ProxyError) {
    if (error.status === 409) return "แก้ไขไม่ได้ เพราะ Claim นี้ไม่ได้อยู่ในสถานะแบบร่างแล้ว";
    if (error.status === 400) return "ข้อมูลไม่ถูกต้อง กรุณาตรวจสอบอีเมลและรายละเอียดอีกครั้ง";
    if (error.status === 403) return "คุณไม่มีสิทธิ์ทำรายการนี้";
    if (error.status === 404) return "ไม่พบ Claim นี้";
  }
  return "บันทึกไม่สำเร็จ กรุณาลองใหม่อีกครั้ง";
}

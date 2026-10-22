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
  /** What the Insurance Adjuster approved; null until approved. Separate from any Estimated Amount. */
  approvedAmount: number | null;
  approvedEstimateId: number | null;
  /** Optimistic-concurrency token: send it back as `If-Match` (see ifMatch) on every change to this Claim. */
  version: string;
}

/** Request options carrying the version of the Claim the user is looking at, so a change made on stale data is refused with 409. */
export const ifMatch = (claim: { version: string }): Pick<RequestInit, "headers"> => ({ headers: { "If-Match": claim.version } });

/** Problem `type` the API puts on a 409 caused by someone else changing the Claim first (as opposed to a forbidden status move). */
export const versionConflictType = "urn:claim:version-conflict";

export const isVersionConflict = (error: unknown) => error instanceof ProxyError && error.status === 409 && error.problem?.type === versionConflictType;

/** Thai message for a concurrent-change conflict; the page reloads the Claim so the user can retry on fresh data. */
export const versionConflictMessage = "เคลมนี้ถูกแก้ไขโดยผู้อื่นไปก่อนหน้า ระบบโหลดข้อมูลล่าสุดให้แล้ว กรุณาตรวจสอบแล้วทำรายการอีกครั้ง";

export type ReviewAction = "pickup" | "approve" | "reject" | "requestChanges";

/**
 * Review actions an Insurance Adjuster may take, from role and status (mirrors ClaimLifecycle):
 * Submitted can be picked up; UnderReview can be approved, rejected or sent back for changes. Nothing else.
 */
export function availableReviewActions(role: string | undefined, status: ClaimStatus): ReviewAction[] {
  if (role !== "InsuranceAdjuster") return [];
  if (status === "Submitted") return ["pickup"];
  if (status === "UnderReview") return ["approve", "reject", "requestChanges"];
  return [];
}

/** Thai message for a failed review action. */
export function describeReviewError(error: unknown): string {
  if (isVersionConflict(error)) return versionConflictMessage;
  if (error instanceof ProxyError) {
    if (error.status === 400) {
      const fields = Object.keys(error.problem?.errors ?? {});
      if (fields.includes("approvedAmount")) return "ยอดอนุมัติต้องมากกว่า 0 และไม่เกินวงเงินคุ้มครอง";
      return "กรุณากรอกเหตุผลหรือความเห็นให้ครบ";
    }
    if (error.status === 409) return "ทำรายการไม่ได้ สถานะหรือใบประเมินฉบับล่าสุดของเคลมนี้เปลี่ยนไปแล้ว กรุณารีเฟรชหน้า";
    if (error.status === 403) return "คุณไม่มีสิทธิ์ทำรายการนี้";
    if (error.status === 404) return "ไม่พบเคลมนี้";
  }
  return "ทำรายการไม่สำเร็จ กรุณาลองใหม่อีกครั้ง";
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

export type EstimateLineKind = "Part" | "Labour";

export const estimateLineKindLabels: Record<EstimateLineKind, string> = {
  Part: "อะไหล่",
  Labour: "ค่าแรง",
};

export interface EstimateLine {
  kind: EstimateLineKind;
  description: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

/** One revision of a Repair Estimate. The Estimated Amount is computed by the API, never sent by the client. */
export interface RepairEstimate {
  id: number;
  claimId: number;
  revision: number;
  isLatest: boolean;
  lines: EstimateLine[];
  estimatedAmount: number;
  coverageLimit: number;
  exceedsCoverageLimit: boolean;
  warnings: string[];
  submittedByUserId: number;
  submittedAt: string;
}

export interface EstimateInput {
  lines: { kind: EstimateLineKind; description: string; quantity: number; unitPrice: number }[];
}

/** Statuses in which Garage Staff may submit a Repair Estimate revision (mirrors ClaimLifecycle.AcceptsEstimate). */
export const estimateAcceptingStatuses: readonly ClaimStatus[] = ["Submitted", "ChangesRequested"];

/** Thai message for a failed Repair Estimate submission. */
export function describeEstimateError(error: unknown): string {
  if (isVersionConflict(error)) return versionConflictMessage;
  if (error instanceof ProxyError) {
    if (error.status === 400) return "ข้อมูลรายการไม่ถูกต้อง กรุณาตรวจสอบรายละเอียด จำนวน และราคาต่อหน่วย";
    if (error.status === 409) return "ส่งใบประเมินไม่ได้ เพราะสถานะของเคลมนี้ไม่รับใบประเมิน";
    if (error.status === 403) return "คุณไม่มีสิทธิ์ทำรายการนี้";
    if (error.status === 404) return "ไม่พบเคลมนี้";
  }
  return "ส่งใบประเมินไม่สำเร็จ กรุณาลองใหม่อีกครั้ง";
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
  if (isVersionConflict(error)) return versionConflictMessage;
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
  if (isVersionConflict(error)) return versionConflictMessage;
  if (error instanceof ProxyError) {
    if (error.status === 409) return "แก้ไขไม่ได้ เพราะ Claim นี้ไม่ได้อยู่ในสถานะแบบร่างแล้ว";
    if (error.status === 400) return "ข้อมูลไม่ถูกต้อง กรุณาตรวจสอบอีเมลและรายละเอียดอีกครั้ง";
    if (error.status === 403) return "คุณไม่มีสิทธิ์ทำรายการนี้";
    if (error.status === 404) return "ไม่พบ Claim นี้";
  }
  return "บันทึกไม่สำเร็จ กรุณาลองใหม่อีกครั้ง";
}

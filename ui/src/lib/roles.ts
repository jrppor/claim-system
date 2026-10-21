// Role names match the API (Claim.Domain.Users.Role). Pure module: safe to import from proxy, server and client.

export const ROLES = ["GarageStaff", "InsuranceAdjuster", "Admin"] as const;
export type Role = (typeof ROLES)[number];

export const roleLabels: Record<Role, string> = {
  GarageStaff: "พนักงานอู่",
  InsuranceAdjuster: "เจ้าหน้าที่ประกัน",
  Admin: "ผู้ดูแลระบบ",
};

export function isRole(value: unknown): value is Role {
  return typeof value === "string" && (ROLES as readonly string[]).includes(value);
}

export interface SessionUser {
  id: number;
  email: string;
  displayName: string;
  role: Role;
}

export interface RouteRule {
  /** Path prefix; matches the path itself and anything beneath it. */
  prefix: string;
  roles: readonly Role[];
}

/**
 * Role-restricted pages. Any signed-in user may open a path that matches no rule.
 * Later tickets add their pages here (the API remains the real enforcement point).
 */
export const routeRules: readonly RouteRule[] = [
  { prefix: "/users", roles: ["Admin"] },
  { prefix: "/claims/new", roles: ["GarageStaff"] },
];

export function pathMatches(pathname: string, prefix: string): boolean {
  return pathname === prefix || pathname.startsWith(prefix + "/");
}

export function canAccess(role: Role, pathname: string): boolean {
  const rule = routeRules.find((r) => pathMatches(pathname, r.prefix));
  return !rule || rule.roles.includes(role);
}

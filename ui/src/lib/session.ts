import type { NextResponse } from "next/server";
import { isRole, type SessionUser } from "@/lib/roles";

// Server-side only (route handlers). Tokens never reach browser JavaScript: all cookies are httpOnly.

export const ACCESS_COOKIE = "claim_access";
export const REFRESH_COOKIE = "claim_refresh";
/** Non-secret copy of the user's identity so the proxy/UI can route by role without decoding tokens. */
export const USER_COOKIE = "claim_user";

const REFRESH_MAX_AGE = 7 * 24 * 60 * 60;

export const apiBase = (process.env.API_BASE_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000").replace(/\/$/, "");

export interface AuthResult {
  accessToken: string;
  expiresIn: number;
  refreshToken: string;
  user: SessionUser;
}

const cookieBase = {
  httpOnly: true,
  sameSite: "lax" as const,
  secure: process.env.NODE_ENV === "production",
  path: "/",
};

export function setSessionCookies(res: NextResponse, auth: AuthResult) {
  res.cookies.set(ACCESS_COOKIE, auth.accessToken, { ...cookieBase, maxAge: auth.expiresIn });
  res.cookies.set(REFRESH_COOKIE, auth.refreshToken, { ...cookieBase, maxAge: REFRESH_MAX_AGE });
  res.cookies.set(USER_COOKIE, encodeURIComponent(JSON.stringify(auth.user)), { ...cookieBase, maxAge: REFRESH_MAX_AGE });
}

export function clearSessionCookies(res: NextResponse) {
  for (const name of [ACCESS_COOKIE, REFRESH_COOKIE, USER_COOKIE]) res.cookies.delete(name);
}

export function parseUserCookie(value: string | undefined): SessionUser | null {
  if (!value) return null;
  try {
    const user = JSON.parse(decodeURIComponent(value)) as SessionUser;
    return isRole(user.role) ? user : null;
  } catch {
    return null;
  }
}

/** Exchanges a refresh token (rotating it) for a new token pair, or null if the API rejects it. */
export async function refreshWithApi(refreshToken: string): Promise<AuthResult | null> {
  const response = await fetch(`${apiBase}/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
    cache: "no-store",
  });
  return response.ok ? ((await response.json()) as AuthResult) : null;
}

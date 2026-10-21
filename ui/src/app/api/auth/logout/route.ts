import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { apiBase, clearSessionCookies, REFRESH_COOKIE } from "@/lib/session";

export async function POST() {
  const refreshToken = (await cookies()).get(REFRESH_COOKIE)?.value;
  if (refreshToken) {
    // Revoke server-side first; clear the browser cookies even if the API is unreachable.
    await fetch(`${apiBase}/auth/logout`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken }),
      cache: "no-store",
    }).catch(() => undefined);
  }
  const res = new NextResponse(null, { status: 204 });
  clearSessionCookies(res);
  return res;
}

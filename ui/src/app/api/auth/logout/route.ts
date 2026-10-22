import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { apiBase, clearSessionCookies, REFRESH_COOKIE } from "@/lib/session";

export async function POST() {
  const refreshToken = (await cookies()).get(REFRESH_COOKIE)?.value;
  if (refreshToken) {
    // Revoke server-side first; clear the browser cookies even if the API is unreachable.
    try {
      await fetch(`${apiBase}/auth/logout`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken }),
        cache: "no-store",
      });
    } catch (error) {
      // The user is still signed out of this browser; the refresh token simply stays valid on the server until it expires.
      console.error("Logout: could not revoke the refresh token on the Claim API", error);
    }
  }
  const res = new NextResponse(null, { status: 204 });
  clearSessionCookies(res);
  return res;
}

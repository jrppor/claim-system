import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import {
  ACCESS_COOKIE,
  clearSessionCookies,
  parseUserCookie,
  REFRESH_COOKIE,
  refreshWithApi,
  setSessionCookies,
  USER_COOKIE,
} from "@/lib/session";

/** Returns the signed-in user, silently refreshing the access token if it has expired. */
export async function GET() {
  const jar = await cookies();
  const refreshToken = jar.get(REFRESH_COOKIE)?.value;
  const user = parseUserCookie(jar.get(USER_COOKIE)?.value);
  if (!refreshToken || !user) return unauthenticated();

  if (jar.get(ACCESS_COOKIE)?.value) return NextResponse.json({ user });

  const auth = await refreshWithApi(refreshToken);
  if (!auth) return unauthenticated();
  const res = NextResponse.json({ user: auth.user });
  setSessionCookies(res, auth);
  return res;
}

function unauthenticated() {
  const res = NextResponse.json({ title: "Unauthorized" }, { status: 401 });
  clearSessionCookies(res);
  return res;
}

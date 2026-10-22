import { NextResponse } from "next/server";
import { apiBase, setSessionCookies, type AuthResult } from "@/lib/session";

export async function POST(request: Request) {
  const body = await request.text();
  let response: Response;
  try {
    response = await fetch(`${apiBase}/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body,
      cache: "no-store",
    });
  } catch (error) {
    console.error("Login: the Claim API is unreachable", error);
    return NextResponse.json({ title: "API unavailable" }, { status: 502 });
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    return NextResponse.json(problem, { status: response.status });
  }

  const auth = (await response.json()) as AuthResult;
  const res = NextResponse.json({ user: auth.user });
  setSessionCookies(res, auth);
  return res;
}

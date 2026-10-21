import { NextResponse, type NextRequest } from "next/server";
import { canAccess } from "@/lib/roles";
import { parseUserCookie, REFRESH_COOKIE, USER_COOKIE } from "@/lib/session";

// Page guard: sign-in required everywhere except /login, and role-restricted pages (see routeRules)
// are replaced with a 403 page. The API still enforces roles; this only keeps users out of pages
// they cannot use.
export function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const user = parseUserCookie(request.cookies.get(USER_COOKIE)?.value);
  const signedIn = !!user && !!request.cookies.get(REFRESH_COOKIE)?.value;

  if (pathname === "/login") {
    return signedIn ? NextResponse.redirect(new URL("/", request.url)) : NextResponse.next();
  }

  if (!signedIn) {
    const login = new URL("/login", request.url);
    if (pathname !== "/") login.searchParams.set("next", pathname + request.nextUrl.search);
    return NextResponse.redirect(login);
  }

  if (!canAccess(user.role, pathname)) {
    return NextResponse.rewrite(new URL("/forbidden", request.url), { status: 403 });
  }
  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!api|_next/static|_next/image|favicon.ico).*)"],
};

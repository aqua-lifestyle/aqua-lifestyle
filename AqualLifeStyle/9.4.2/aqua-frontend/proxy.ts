import { NextRequest, NextResponse } from "next/server";

import { decryptSession, projectSession, readSessionCookie } from "@/src/shared/auth/session";
import { getRoleHome } from "@/src/shared/auth/roles";

const protectedPrefixes = [
  "/admin", "/area-leader", "/customers", "/dashboard", "/enquiries",
  "/facilitator", "/member", "/memberships", "/order-intents", "/products",
  "/profile", "/settings",
];
const guestOnlyRoutes = new Set(["/login", "/signup"]);

export async function proxy(request: NextRequest) {
  const path = request.nextUrl.pathname;
  const session = await decryptSession(readSessionCookie(request.cookies));
  const user = session ? projectSession(session).user : null;
  const isProtected = protectedPrefixes.some(
    (prefix) => path === prefix || path.startsWith(`${prefix}/`),
  );

  if (isProtected && !user) {
    const loginUrl = new URL("/login", request.url);
    loginUrl.searchParams.set("redirect", `${path}${request.nextUrl.search}`);
    return NextResponse.redirect(loginUrl);
  }
  if ((path === "/" || guestOnlyRoutes.has(path)) && user) {
    return NextResponse.redirect(new URL(getRoleHome(user.role).href, request.url));
  }
  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!api|_next/static|_next/image|favicon.ico|.*\\.(?:png|jpg|jpeg|gif|svg|webp)$).*)"],
};

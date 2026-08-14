import { NextResponse } from "next/server";

import { deleteSessionCookies, projectSession, readSession } from "@/src/shared/auth/session";

export async function GET() {
  const session = await readSession();
  const projected = session ? projectSession(session) : null;
  const response = NextResponse.json(projected?.user ? projected : null);
  response.headers.set("Cache-Control", "no-store");
  if (!session || !projected?.user) deleteSessionCookies(response.cookies);
  return response;
}

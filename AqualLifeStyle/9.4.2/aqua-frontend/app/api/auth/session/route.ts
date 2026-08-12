import { NextResponse } from "next/server";

import { deleteSessionCookies, projectSession, readSession } from "@/src/shared/auth/session";

export async function GET() {
  const session = await readSession();
  const response = NextResponse.json(
    session ? projectSession(session) : null,
  );
  response.headers.set("Cache-Control", "no-store");
  if (!session) deleteSessionCookies(response.cookies);
  return response;
}

import { NextResponse } from "next/server";

import { deleteSessionCookies } from "@/src/shared/auth/session";

export async function POST() {
  const response = new NextResponse(null, { status: 204 });
  deleteSessionCookies(response.cookies);
  response.headers.set("Cache-Control", "no-store");
  return response;
}

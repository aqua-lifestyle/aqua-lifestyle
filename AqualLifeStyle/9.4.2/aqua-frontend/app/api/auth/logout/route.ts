import { NextResponse } from "next/server";

import { isSameOrigin } from "@/src/shared/auth/origin";
import { deleteSessionCookies } from "@/src/shared/auth/session";

export async function POST(request: Request) {
  if (!isSameOrigin(request)) {
    return NextResponse.json(
      { message: "Invalid request origin." },
      { status: 403, headers: { "Cache-Control": "no-store" } },
    );
  }

  const response = new NextResponse(null, { status: 204 });
  deleteSessionCookies(response.cookies);
  response.headers.set("Cache-Control", "no-store");
  return response;
}

import { NextRequest, NextResponse } from "next/server";

import { publicEnv } from "@/src/shared/config";
import { deleteSessionCookies, readSession } from "@/src/shared/auth/session";

const forward = async (
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> },
) => {
  const session = await readSession();
  if (!session) {
    const response = NextResponse.json({ message: "Authentication required." }, { status: 401 });
    deleteSessionCookies(response.cookies);
    return response;
  }

  if (!["GET", "HEAD", "OPTIONS"].includes(request.method)) {
    const origin = request.headers.get("origin");
    if (origin && origin !== request.nextUrl.origin) {
      return NextResponse.json({ message: "Invalid request origin." }, { status: 403 });
    }
  }

  const { path } = await params;
  const backendUrl = new URL(path.join("/"), `${publicEnv.NEXT_PUBLIC_ABP_API_URL}/`);
  backendUrl.search = request.nextUrl.search;
  const headers = new Headers();
  headers.set("Accept", request.headers.get("accept") ?? "application/json");
  headers.set("Authorization", `Bearer ${session.accessToken}`);
  const contentType = request.headers.get("content-type");
  if (contentType) headers.set("Content-Type", contentType);
  if (session.tenant) headers.set("__tenant", session.tenant);

  const backendResponse = await fetch(backendUrl, {
    body: ["GET", "HEAD"].includes(request.method) ? undefined : await request.arrayBuffer(),
    cache: "no-store",
    headers,
    method: request.method,
    redirect: "manual",
  });
  const response = new NextResponse(backendResponse.body, {
    headers: { "Content-Type": backendResponse.headers.get("content-type") ?? "application/json" },
    status: backendResponse.status,
  });
  response.headers.set("Cache-Control", "no-store");
  if (backendResponse.status === 401) deleteSessionCookies(response.cookies);
  return response;
};

export const GET = forward;
export const POST = forward;
export const PUT = forward;
export const PATCH = forward;
export const DELETE = forward;

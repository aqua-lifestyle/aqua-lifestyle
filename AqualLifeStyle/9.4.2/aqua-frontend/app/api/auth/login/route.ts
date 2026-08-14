import { NextResponse } from "next/server";

import { publicEnv } from "@/src/shared/config";
import { isSameOrigin } from "@/src/shared/auth/origin";
import {
  encryptSession,
  projectSession,
  setSessionCookies,
  type ServerSession,
} from "@/src/shared/auth/session";

type LoginBody = {
  email: string;
  password: string;
  rememberMe?: boolean;
  tenant?: string | null;
};

export async function POST(request: Request) {
  if (!isSameOrigin(request)) {
    return NextResponse.json(
      { message: "Invalid request origin." },
      { status: 403, headers: { "Cache-Control": "no-store" } },
    );
  }

  const body = (await request.json()) as LoginBody;
  const backendResponse = await fetch(
    `${publicEnv.NEXT_PUBLIC_ABP_API_URL}/api/TokenAuth/Authenticate`,
    {
      body: JSON.stringify({
        password: body.password,
        rememberClient: body.rememberMe ?? false,
        userNameOrEmailAddress: body.email,
      }),
      cache: "no-store",
      headers: { "Content-Type": "application/json", __tenant: body.tenant ?? "" },
      method: "POST",
    },
  );

  const payload = await backendResponse.json();
  if (!backendResponse.ok) {
    return NextResponse.json(payload, {
      status: backendResponse.status,
      headers: { "Cache-Control": "no-store" },
    });
  }

  const result = payload.result as { accessToken: string; expireInSeconds: number };
  const expiresAt = new Date(Date.now() + result.expireInSeconds * 1000).toISOString();
  const session: ServerSession = {
    accessToken: result.accessToken,
    expiresAt,
    tenant: body.tenant ?? null,
  };
  const projected = projectSession(session);
  if (!projected.user) {
    return NextResponse.json(
      {
        error: {
          message:
            "This account cannot be used to sign in to the member portal. Ask a platform administrator for help.",
        },
      },
      { status: 401, headers: { "Cache-Control": "no-store" } },
    );
  }
  const response = NextResponse.json(projected);
  setSessionCookies(response.cookies, await encryptSession(session), expiresAt);
  response.headers.set("Cache-Control", "no-store");
  return response;
}

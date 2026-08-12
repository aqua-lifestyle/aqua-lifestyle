import "server-only";

import { cookies } from "next/headers";

import { serverEnv } from "@/src/shared/config/server-env";
import { claimsToUser, decodeJwtPayload } from "@/src/shared/api/auth-service";
import {
  readSessionCookie,
  setSessionCookies as setCookieChunks,
} from "./session-cookie";

export { deleteSessionCookies, readSessionCookie, SESSION_COOKIE } from "./session-cookie";

export type ServerSession = {
  accessToken: string;
  expiresAt: string;
  tenant: string | null;
};

type CookieWriter = {
  delete: (name: string) => void;
  set: (name: string, value: string, options: ReturnType<typeof sessionCookieOptions>) => void;
};

const encoder = new TextEncoder();
const decoder = new TextDecoder();

const toBase64Url = (value: Uint8Array) =>
  Buffer.from(value).toString("base64url");

const fromBase64Url = (value: string) => new Uint8Array(Buffer.from(value, "base64url"));

const getEncryptionKey = async () => {
  const digest = await crypto.subtle.digest(
    "SHA-256",
    encoder.encode(serverEnv.NEXTAUTH_SECRET),
  );
  return crypto.subtle.importKey("raw", digest, "AES-GCM", false, ["encrypt", "decrypt"]);
};

export const encryptSession = async (session: ServerSession) => {
  const iv = crypto.getRandomValues(new Uint8Array(12));
  const encrypted = await crypto.subtle.encrypt(
    { iv, name: "AES-GCM" },
    await getEncryptionKey(),
    encoder.encode(JSON.stringify(session)),
  );
  return `${toBase64Url(iv)}.${toBase64Url(new Uint8Array(encrypted))}`;
};

export const decryptSession = async (value?: string | null): Promise<ServerSession | null> => {
  if (!value) return null;

  try {
    const [encodedIv, encodedPayload] = value.split(".");
    if (!encodedIv || !encodedPayload) return null;
    const decrypted = await crypto.subtle.decrypt(
      { iv: fromBase64Url(encodedIv), name: "AES-GCM" },
      await getEncryptionKey(),
      fromBase64Url(encodedPayload),
    );
    const session = JSON.parse(decoder.decode(decrypted)) as ServerSession;
    if (!session.accessToken || !session.expiresAt || Date.parse(session.expiresAt) <= Date.now()) {
      return null;
    }
    return session;
  } catch {
    return null;
  }
};

export const projectSession = (session: ServerSession) => {
  const claims = decodeJwtPayload(session.accessToken);
  return {
    expiresAt: session.expiresAt,
    tenant: session.tenant,
    user: claims ? claimsToUser(claims) : null,
  };
};

export const readSession = async () => {
  const cookieStore = await cookies();
  return decryptSession(readSessionCookie(cookieStore));
};

export const sessionCookieOptions = (expiresAt: string) => ({
  expires: new Date(expiresAt),
  httpOnly: true,
  path: "/",
  sameSite: "lax" as const,
  secure: process.env.NODE_ENV === "production",
});

export const setSessionCookies = (
  cookieStore: CookieWriter,
  encryptedSession: string,
  expiresAt: string,
) => setCookieChunks(cookieStore, encryptedSession, sessionCookieOptions(expiresAt));

/**
 * Whether the request Origin is the application's own origin.
 *
 * The primary comparison is strict same-origin against the request URL. The
 * fallback matches host and port against the Host header and is deliberately
 * scheme-insensitive, because the Host header cannot express a scheme; it
 * tolerates Next.js normalising request.url differently from the browser's
 * Host (for example localhost vs 127.0.0.1 in the standalone server). It
 * cannot be reached by a cross-site browser request, because the browser
 * pins Host to the destination host. Forwarded host headers are never
 * trusted. A missing Origin is accepted; modern browsers always send Origin
 * on POST, and the routes using this helper have no legitimate non-browser
 * callers.
 */
export const isSameOrigin = (request: Request): boolean => {
  const origin = request.headers.get("origin");
  if (!origin) return true;
  if (origin === new URL(request.url).origin) return true;
  const host = request.headers.get("host");
  if (!host) return false;
  try {
    return new URL(origin).host === host;
  } catch {
    return false;
  }
};
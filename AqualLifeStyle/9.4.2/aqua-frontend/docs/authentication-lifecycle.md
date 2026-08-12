# Frontend authentication lifecycle

- The authoritative browser session is an AES-GCM encrypted value split across bounded `aqua.session.*` cookies so real permission-bearing JWTs fit within per-cookie browser limits. Every chunk is HttpOnly, host scoped, `SameSite=Lax`, `Path=/`, and `Secure` in production. Their absolute expiry matches the backend access-token expiry.
- `proxy.ts` performs optimistic cookie checks before rendering `/`, guest-only routes, or protected routes. ASP.NET authorization remains authoritative for all data and actions.
- The client auth provider starts in `bootstrapping` and resolves `/api/auth/session`; it never reads the bearer credential. Authenticated API calls use `/api/backend`, which adds the bearer credential server-side and disables response caching.
- Login creates the cookie server-side and uses replace navigation to the validated destination. A valid session visiting `/` or `/login` is redirected to `/dashboard` before anonymous content renders.
- Logout deletes every cookie chunk, clears client user state, and replaces navigation with `/`. User-scoped providers unmount with the authenticated route.
- The backend issues no refresh token. On expiry or a backend `401`, the cookie is removed and the user is sent to login with the protected return destination.

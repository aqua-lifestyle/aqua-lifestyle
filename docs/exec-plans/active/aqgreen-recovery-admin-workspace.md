# AQGreen recovery administrator workspace

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

Goal: reach a read-only administrator landing page after login and run bounded B6
discovery without opening business capabilities or transferring session credentials.

Authority: the owner's 14 October 2026 instruction authorizes a frontend-only
production release and discovery for Tenant 1, original Johannesburg Area. Original
system-introduction identity is defined by `20260811150251_SeparateAreaFromTenantBoundary`;
actual identity and assignments must be verified through the application before reads.

Implementation uses the existing encrypted browser session, authentication,
assigned-Area endpoint and legacy discovery endpoint. A GET-only frontend relay
checks recovery headers, live session identity and the exclusive Area envelope,
then aggregates at most 100 records in pages of 25. No source record is returned to
the browser. Scope changes, pagination inconsistencies, unknown contracts or denied
permissions reject the run. Discovery is not frozen evidence or review acceptance.

Branch: `fix/aqgreen-recovery-admin-login`, parent release `3d03bb7`.
Render remains on `d7947fe` in recovery; automatic backend deployment is held off.
Vercel Git remains disconnected. No backend application, database or provider change.

Current state: IMPLEMENTED and TESTED. All 566 frontend unit/component cases pass,
including 26 server relay cases; all seven focused desktop browser journeys pass
with synthetic authenticated sessions and backend fixtures. Production build,
TypeScript and lint pass (one existing warning); dependency audit is clear.
Production operator Tenant/Area and discovery counts are UNKNOWN until
the authenticated browser uses the released workspace. Never substitute synthetic
workflow results for those production observations.

Next action: publish one controlled frontend release,
then verify the owner's session-bound scope and aggregate outcome in the workspace.

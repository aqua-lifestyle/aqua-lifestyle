# Administrator access security

This document is the operational and technical reference for administrator access, account registration, password changes, and initial administrator credentials.

## Security decisions

- The sign-in page remains public. Authentication endpoints must be reachable by legitimate users; hiding the page is not an access control.
- Customer self-registration is enabled by default for active Areas. An authorised administrator can disable it for an Area that requires managed registration.
- Public registration creates only a Club Member customer account with Guest access. Staff and administrator accounts are created through authorised administrator workflows.
- Browser input never selects an administrator role. Role assignment is performed only through authorised administrator services.
- The platform administrator role is not a default role and must never be assigned automatically.
- Administrator services enforce server-side ABP permissions. Frontend route guards improve navigation but are not the security boundary.
- Passwords, reset tokens, password hashes, and deployment secrets must never be written to logs.

## Registration boundary

`Abp.Account.IsSelfRegistrationEnabled` is defined at application and Area scope with a default value of `true`. An explicit Area value can disable customer signup without changing other Areas.

The boundary is enforced in several places:

1. The Next.js application asks the backend for the selected Area's current registration availability.
2. The sign-in page shows **Sign up** only when that Area currently permits self-registration.
3. A direct visit to `/signup` checks the same runtime availability and fails closed if it is disabled or cannot be confirmed.
4. The ABP account application service checks the target Area setting again before registration.
5. The registration domain manager repeats the Area-specific check as defence in depth.
6. Both GET and POST requests to the legacy MVC registration action redirect to sign-in when registration is disabled.

There is no frontend registration feature flag. `Abp.Account.IsSelfRegistrationEnabled` is the single authority and can vary by Area at runtime. Frontend availability checks improve the experience, while the backend registration checks remain the security boundary.

## Administrator account creation

Administrators create users through the protected administrator workspace. The server checks the caller's granular user-management permission before accepting a role assignment. A caller cannot become an administrator by changing a browser request or registration payload.

Relevant automated coverage verifies that:

- a non-administrator cannot create a System Administrator;
- an authorised administrator can create one;
- the business role and persisted Identity role agree; and
- cross-Area account creation is rejected for an Area administrator.

## Changing a password

Every authenticated user can change their own password from **Settings → Account security**.

The workflow is:

1. Validate the current password, new password, and confirmation in the browser.
2. Repeat new-password validation on the server.
3. Check whether the account is locked.
4. Verify the current password.
5. Record an incorrect attempt using ASP.NET Identity lockout counters.
6. Return a typed business result so the counter commits instead of being rolled back with an expected exception.
7. On success, change the password, rotate the security stamp, clear the failed-attempt count, and save.
8. Clear the browser session and require sign-in with the new password.

The new password requires at least eight characters with uppercase, lowercase, number, and one of `!@#$%^&*()`. The current and new passwords must differ.

JWT authentication validates the persisted security stamp. Rotating it invalidates existing tokens on their next authenticated request, so changing a password signs the account out on every device. If refresh tokens are added later, revoke them explicitly as part of the same operation.

Audit records and structured logs contain the Area ID, user ID, outcome, and lockout state. Password fields are marked `DisableAuditing`, and no password value is logged.

## Restored Club Member accounts

Customer restoration is a separate workflow from administrator bootstrap:

1. Restore the existing Customer and Identity IDs.
2. Enable the Identity record.
3. Require password setup.
4. Rotate the security stamp so old sessions stop working.
5. Generate a one-time ASP.NET Identity reset token.
6. Block normal authentication until password setup completes.
7. Clear the reset requirement after the Club Member chooses a password.

Restoration reconnects the original account to its history. It does not recreate or rewrite historical records, and an administrator does not choose the Club Member's final password.

## Initial production administrators

Fresh production databases require `AQUA_INITIAL_ADMIN_PASSWORD`. The Render Blueprint declares it with `sync: false`, so the value must be entered in Render and is never committed to Git.

Every environment except the explicitly named `Development` environment requires at least 16 characters containing uppercase, lowercase, number, and special characters. Production, staging, QA, unset, and unrecognised environment names fail closed when the secret is missing or weak. Local development continues to use the conventional local-only password unless an override is supplied.

The shared secret bootstraps the host administrator and is the fallback for the initial administrator in each Area. A specific Area can override it with:

```text
AQUA_INITIAL_TENANT_<AREA_ID>_ADMIN_PASSWORD
```

For example, Area ID `1` uses `AQUA_INITIAL_TENANT_1_ADMIN_PASSWORD` when present.

Important behavior:

- Bootstrap secrets apply only when the corresponding administrator does not exist.
- Changing the Render value does not rotate an existing database password.
- The application never prints or returns the bootstrap password.
- Seeded administrators can sign in with the bootstrap password and must immediately replace it through **Account security**.
- Do not mark a seeded administrator as requiring an undelivered reset token. That would block the only administrator without giving them a usable recovery path.

## Existing production deployment

Use this sequence for the current Render and Vercel deployment:

1. Merge and deploy the reviewed branch.
2. Confirm `Abp.Account.IsSelfRegistrationEnabled` is `true` for Areas that accept customer signup and explicitly `false` only for managed-registration Areas.
3. Confirm the Render API is healthy at `https://aqualifestyle-api.onrender.com/api/health`.
4. Sign in to the Default Area as its administrator.
5. Open **Settings → Account security** and replace `123qwe` with a unique password stored in a password manager.
6. Confirm the browser returns to sign-in and the old password no longer works.
7. Sign in to **Platform administration** with the host administrator and rotate that password separately if the account is in use.
8. Confirm a previously issued token receives an unauthorised response after its security stamp changes.
9. Confirm `/signup` creates only a customer with Guest access in an enabled Area, while disabled Areas show the managed-registration message and reject direct registration requests.
10. Review Render logs for successful startup and the expected password-change audit event without credential values.

An existing database does not need the bootstrap secret to redeploy because both administrator records already exist. Set the secret before a fresh database or disaster-recovery bootstrap.

## Fresh production deployment

Before the first migration against an empty production database:

1. Generate a unique password of at least 16 characters. Prefer a password-manager generator with substantially more entropy.
2. In Render, open the API service or Blueprint environment settings.
3. Set `AQUA_INITIAL_ADMIN_PASSWORD` as a secret value.
4. Deploy and allow the pre-deploy migrator to create the initial administrators.
5. Sign in separately to the Default Area and Platform administration.
6. Change both passwords immediately. Do not reuse the shared bootstrap value as either final password.
7. Remove or rotate the bootstrap secret after successful recovery testing. A future empty-database bootstrap must deliberately supply a new value.

## Verification commands

From `AqualLifeStyle/9.4.2/aspnet-core`:

```bash
dotnet restore AqualLifeStyle.sln
dotnet build AqualLifeStyle.sln --no-restore
dotnet test AqualLifeStyle.sln --no-build --no-restore -m:1
```

From `AqualLifeStyle/9.4.2/aqua-frontend` using the repository-supported Node version:

```bash
npm ci
npm run lint
npm run type-check
npm test -- --run
npm run build
```

Manual checks:

- Enabled Areas advertise customer sign-up and create only Guest access.
- Sign-up is not advertised when an Area has disabled registration.
- Direct registration is blocked by both frontend and backend paths.
- Club Member registration cannot submit a role.
- Non-administrators receive an authorisation failure from administrator APIs.
- Wrong current-password attempts increase the Identity failure counter.
- Successful password changes invalidate existing JWTs and reset the failure counter.
- Host administrator is not a default role.
- Fresh production bootstrap fails when its secret is missing or weak.
- No logs contain bootstrap passwords, user passwords, hashes, or reset tokens.

## Rollback and recovery

Application rollback does not restore a previous password or security stamp. If a deployment must be rolled back:

1. Roll back the application image or Git revision.
2. Keep the rotated administrator credentials; do not restore `123qwe`.
3. Verify sign-in and permission checks on the rolled-back version.
4. If the administrator cannot sign in, use a reviewed out-of-band database recovery procedure or a future email-based password-reset workflow. Never place a password or reset token in logs.
5. Rotate the JWT signing key if token material may have been exposed. This invalidates every outstanding JWT.

## Remaining recommended controls

These controls are not completed by the current change and should be planned explicitly:

- administrator MFA, preferably phishing-resistant WebAuthn/passkeys;
- email delivery for one-time invitations and password recovery;
- explicit refresh-token storage and revocation if refresh tokens are introduced;
- rate limiting at Render/reverse-proxy and application levels;
- verified email addresses instead of automatic confirmation for public registration;
- CAPTCHA or bot protection for public registration;
- security alerts for administrator creation, role changes, password changes, and repeated lockouts;
- periodic access reviews and removal of unused administrator accounts; and
- recovery codes and a documented two-person administrator recovery process.

Do not extend public customer registration to staff or administrator roles. Administrator onboarding should become invitation-based, permission-gated, time-limited, and fully audited.

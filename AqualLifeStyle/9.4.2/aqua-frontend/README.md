# Aqua Frontend

Next.js App Router frontend for the AquaLifeStyle ABP backend.

## Current Demo Scope

- Products: read live product data from ABP.
- Customers: register, view, and update customer records.
- Memberships: view membership tiers and assign a membership during customer registration.
- Enquiries: create enquiries, view enquiry records, and manage response/close/reopen workflow actions.

The root route is a demo hub that guides the current end-to-end validation path.

## Architecture Baseline

- TypeScript strict mode is required.
- Tailwind CSS is the primary styling solution.
- Runtime environment variables are validated with Zod in `src/shared/config`.
- Backend access goes through the shared Axios boundary in `src/shared/api`.
- ABP response envelopes and error envelopes are normalized centrally.
- Feature state uses the agreed four-file provider structure:
  - `actions.tsx`
  - `context.tsx`
  - `index.tsx`
  - `reducer.tsx`
- Feature code should not import raw Axios or read `process.env` directly.
- We work with a lean startup mindset: build the smallest useful slice, validate it against the running app, then commit the learning before expanding scope.

## Environment

Create a local environment file:

```bash
cp .env.example .env.local
```

Default local backend settings:

```env
NEXT_PUBLIC_ABP_API_URL=https://localhost:44311
NEXTAUTH_SECRET=replace_with_a_32_character_minimum_secret
```

`NEXT_PUBLIC_ABP_API_URL` is intentionally public because browser code needs the backend base URL. `NEXTAUTH_SECRET` is server-only and must not be exposed to client modules.

## Development

Start the backend first, then run:

```bash
npm run dev
```

Open:

- Demo hub: [http://localhost:3000](http://localhost:3000)
- Products: [http://localhost:3000/products](http://localhost:3000/products)
- Customers: [http://localhost:3000/customers](http://localhost:3000/customers)
- Register customer: [http://localhost:3000/customers/register](http://localhost:3000/customers/register)
- Enquiries: [http://localhost:3000/enquiries](http://localhost:3000/enquiries)
- Create enquiry: [http://localhost:3000/enquiries/create](http://localhost:3000/enquiries/create)
- Memberships: [http://localhost:3000/memberships](http://localhost:3000/memberships)

Use a normal browser such as Chrome or Edge for local HTTPS backend testing. VS Code's embedded preview can report generic network errors with local development certificates.

## Validation

Before committing frontend changes, run:

```bash
npm run lint
npm run build
```

## Next Steps

- Add authentication with OIDC Authorization Code Flow + PKCE.
- Add tenant resolution and `__tenant` header selection UI.
- Generate type-safe API clients from OpenAPI once the backend Swagger JSON endpoint is available.
- Add focused tests for API client behavior, provider reducers, and registration validation.

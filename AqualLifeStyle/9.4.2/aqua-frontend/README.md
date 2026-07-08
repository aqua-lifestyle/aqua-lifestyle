# Aqua Frontend

Next.js App Router frontend for the AquaLifeStyle ABP backend.

## Current Demo Scope

- Products: read live product data from ABP, open product details, and validate membership-based customer eligibility.
- Customers: register, view, update customer records, and inspect eligible products.
- Memberships: view membership tiers, open membership details with tier benefits, and assign a membership during customer registration.
- Enquiries: create enquiries, view enquiry records, record follow-ups, review sales-ready enquiries, mark conversions, and manage response/close/reopen workflow actions.

The root route is a live demo dashboard that guides the current end-to-end validation path and summarizes backend data.

## Demo Walkthrough

Use this path for the current end-to-end demo:

1. Open the demo hub at `/` and confirm the readiness checklist is green where backend data exists.
2. Open `/memberships`, filter by tier/status, then choose a membership and register a customer with that tier preselected.
3. Open `/products`, filter by membership access, then start an enquiry from a product or from a customer eligible-product card.
4. Open `/enquiries`, use the pipeline filter, respond to the enquiry, record follow-ups, and review sales-ready movement.
5. Mark the enquiry converted and return to the dashboard to confirm the conversion handoff is reflected in live metrics.

## Known Backend Gaps For The Next Demo Level

- Authentication and tenant switching now have frontend readiness boundaries, but real login, token refresh, and tenant selection UI are not wired yet.
- Email/SMS delivery for enquiry notifications is intentionally paused.
- Orders, payments, subscriptions, and fulfillment are not exposed as frontend workflows yet.
- Area Space, Area Leader, events, training, and therapy modules need backend/API confirmation before UI work.
- OpenAPI-generated clients should replace hand-written DTOs once the Swagger contract is stable for frontend generation.

## API Contract

- Backend Swagger UI is enabled by the ASP.NET Core host and `/` redirects to `/swagger`.
- The verified local Swagger JSON endpoint is `https://localhost:44311/swagger/v1/swagger.json`.
- Type generation should wait until authentication, tenant behavior, and the demo DTO contracts stabilize.
- The current handwritten DTOs remain intentionally small and live behind provider/shared API boundaries.

## Architecture Baseline

- TypeScript strict mode is required.
- Tailwind CSS is the primary styling solution.
- Runtime environment variables are validated with Zod in `src/shared/config`.
- Backend access goes through the shared Axios boundary in `src/shared/api`.
- ABP response envelopes and error envelopes are normalized centrally.
- Auth and tenant readiness providers register access-token and `__tenant` resolvers with the shared Axios boundary.
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
- Sales-ready enquiries: [http://localhost:3000/enquiries/sales-ready](http://localhost:3000/enquiries/sales-ready)
- Create enquiry: [http://localhost:3000/enquiries/create](http://localhost:3000/enquiries/create)
- Memberships: [http://localhost:3000/memberships](http://localhost:3000/memberships)

Use a normal browser such as Chrome or Edge for local HTTPS backend testing. VS Code's embedded preview can report generic network errors with local development certificates.

## Validation

Before committing frontend changes, run:

```bash
npm run test
npm run lint
npm run build
```

## Next Steps

- Wire authentication with OIDC Authorization Code Flow + PKCE.
- Add tenant selection UI that feeds the existing tenant provider.
- Generate type-safe API clients from the verified Swagger JSON endpoint once auth/tenant contracts are stable.
- Expand focused tests for API client behavior, provider reducers, and registration validation.

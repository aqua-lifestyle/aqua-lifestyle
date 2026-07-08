# Aqua Frontend

Next.js App Router frontend for the AquaLifeStyle ABP backend.

## Current Demo Scope

- Products: read live product data from ABP, open product details, and validate membership-based customer eligibility.
- Customers: register, view, update customer records, and inspect eligible products.
- Memberships: view membership tiers, open membership details with tier benefits, and assign a membership during customer registration.
- Enquiries: create enquiries, view enquiry records, record follow-ups, review sales-ready enquiries, mark conversions, and manage response/close/reopen workflow actions.
- Order intents: create a lightweight reservation from a converted enquiry, then cancel or complete it without introducing payments prematurely.
- Tenant context: switch between host mode and a named tenant so subsequent API requests carry ABP's `__tenant` header.

The root route is a live demo dashboard that guides the current end-to-end validation path and summarizes backend data.

## Demo Walkthrough

Use this path for the current end-to-end demo:

1. Open the demo hub at `/` and confirm the readiness checklist is green where backend data exists.
2. Open `/memberships`, filter by tier/status, then choose a membership and register a customer with that tier preselected.
3. Open `/products`, filter by membership access, then start an enquiry from a product or from a customer eligible-product card.
4. Open `/enquiries`, use the pipeline filter, respond to the enquiry, record follow-ups, and review sales-ready movement.
5. Mark the enquiry converted, create an order intent from the enquiry detail page, then open `/order-intents` to review the reservation handoff.
6. Return to the dashboard to confirm conversion and order-intent metrics are reflected in the live readiness view.

## Known Backend Gaps For The Next Demo Level

- Authentication has a frontend readiness boundary, but real login and token refresh are not wired yet.
- Tenant switching is available as a demo control that feeds the shared API client; tenant discovery and tenant-specific login are not wired yet.
- Email/SMS delivery for enquiry notifications is intentionally paused.
- Payments, subscriptions, fulfillment, and real order settlement are not exposed as frontend workflows yet.
- Order intents are available as the intentionally small pre-payment commerce handoff.
- Area Space, Area Leader, events, training, and therapy modules need backend/API confirmation before UI work.
- OpenAPI-generated clients should replace hand-written DTOs once the Swagger contract is stable for frontend generation.

## API Contract

- Backend Swagger UI is enabled by the ASP.NET Core host and `/` redirects to `/swagger`.
- The verified local Swagger JSON endpoint is `https://localhost:44311/swagger/v1/swagger.json`.
- Type generation should wait until authentication, tenant behavior, and the demo DTO contracts stabilize.
- The current handwritten DTOs remain intentionally small and live behind provider/shared API boundaries.

## Commerce Next Step Analysis

Backend scan summary:

- Products, memberships, customers, and enquiries have application services and are already used by the demo.
- Savings exists in the domain model with tests, but there is no savings application service exposed to the frontend yet.
- Order intent application services are now exposed and used by the demo.
- Payment, fulfillment, Area Space, Area Leader, event, training, and therapy workflows do not currently have confirmed application services for frontend integration.

Completed commerce slice:

1. Added an order-intent reservation backend workflow after enquiry conversion, before payment.
2. Connected it to customer, product, membership eligibility, tier discounts, tier max-open-order rules, and order-window rules.
3. Added a frontend `/order-intents` demo page and converted-enquiry handoff action.

Recommended next commerce slice:

1. Decide whether the next learning target is payment intent/proof-of-payment or fulfillment workflow.
2. Keep payment/proof-of-payment separate from order-intent creation so the reservation lifecycle remains reviewable.
3. Surface savings-window status as supporting membership context, but do not build full savings account management until an application service exists.

Why this order:

- It extends the current proven journey naturally: membership -> product -> customer -> enquiry -> conversion -> order intent.
- It validates buyer/operator behavior before committing to payment, fulfillment, or savings-led complexity.
- It avoids creating frontend-only workflows for backend modules that are not yet exposed.

## Architecture Baseline

- TypeScript strict mode is required.
- Tailwind CSS is the primary styling solution.
- Runtime environment variables are validated with Zod in `src/shared/config`.
- Backend access goes through the shared Axios boundary in `src/shared/api`.
- ABP response envelopes and error envelopes are normalized centrally.
- Auth and tenant readiness providers register access-token and `__tenant` resolvers with the shared Axios boundary.
- The tenant switcher persists the local tenant selection and keeps host mode as the safe default.
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
- Order intents: [http://localhost:3000/order-intents](http://localhost:3000/order-intents)
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
- Add the next post-reservation commerce slice: payment intent/proof-of-payment or fulfillment, based on demo feedback.
- Replace manual tenant entry with tenant discovery once the backend contract is confirmed.
- Generate type-safe API clients from the verified Swagger JSON endpoint once auth/tenant contracts are stable.
- Expand focused tests for API client behavior, provider reducers, and registration validation.

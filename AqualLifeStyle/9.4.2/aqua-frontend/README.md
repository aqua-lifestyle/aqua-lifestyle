# Aqua Frontend

Next.js App Router frontend for the AquaLifeStyle ABP backend.

## Current Demo Scope

- Products: read live product data from ABP, open product details, and validate membership-based customer eligibility.
- Customers: register, view, update customer records, and inspect eligible products.
- Memberships: view membership tiers, open membership details with tier benefits, and assign a membership during customer registration.
- Savings readiness: view read-only tier savings-window status as supporting membership context.
- Enquiries: create enquiries, view enquiry records, record follow-ups, review sales-ready enquiries, mark conversions, and manage response/close/reopen workflow actions.
- Order intents: create a lightweight reservation from a converted enquiry, review reserved/completed value, show member savings from tier discounts, then cancel or complete it without introducing payments prematurely.
- Authentication context: show whether API calls are anonymous or include an access token from the auth boundary.
- Tenant context: switch between host mode and a named tenant so subsequent API requests carry ABP's `__tenant` header.
- Backend readiness: show whether the browser can reach ABP's `/api/health` endpoint, including refreshable version/environment and database reachability context.

The root route is a live demo dashboard that guides the current end-to-end validation path, summarizes backend data, and includes backend/database readiness in the demo checklist.

## Demo Walkthrough

Use this path for the current end-to-end demo:

1. Open the demo hub at `/` and confirm the readiness checklist is green where backend data exists.
2. Open `/memberships`, filter by tier/status, then choose a membership and register a customer with that tier preselected.
3. Open `/products`, filter by membership access, then start an enquiry from a product or from a customer eligible-product card.
4. Open `/enquiries`, use the pipeline filter, respond to the enquiry, record follow-ups, and review sales-ready movement.
5. Mark the enquiry converted, create an order intent from the enquiry detail page, then open `/order-intents` to review the reservation handoff, reserved value, completed value, and member savings.
6. Return to the dashboard to confirm conversion and order-intent metrics are reflected in the live readiness view.

## Known Backend Gaps For The Next Demo Level

- Authentication has a visible frontend readiness boundary, but real login and token refresh are not wired yet.
- Tenant switching is available as a demo control that feeds the shared API client; tenant discovery and tenant-specific login are not wired yet.
- Email/SMS delivery for enquiry notifications is intentionally paused.
- Payments, subscriptions, fulfillment, and real order settlement are not exposed as frontend workflows yet.
- Order intents are available as the intentionally small pre-payment commerce handoff with read-only value metrics, not payment settlement.
- Area Space, Area Leader, Facilitator, and Referral modules now have frontend UI flows backed by existing ABP app services.
- Events, training, and therapy modules need backend/API confirmation before UI work.
- OpenAPI-generated clients should replace hand-written DTOs once the Swagger contract is stable for frontend generation.

## API Contract

- Backend Swagger UI is enabled by the ASP.NET Core host and `/` redirects to `/swagger`.
- The verified local Swagger JSON endpoint is `https://localhost:44311/swagger/v1/swagger.json`.
- Type generation should wait until authentication, tenant behavior, and the demo DTO contracts stabilize.
- The current handwritten DTOs remain intentionally small and live behind provider/shared API boundaries.

## Commerce Next Step Analysis

Backend scan summary:

- Products, memberships, customers, and enquiries have application services and are already used by the demo.
- Savings-window readiness is exposed as a read-only membership endpoint and surfaced on the memberships page; full savings account/deposit workflows are not exposed yet.
- Order intent application services are now exposed and used by the demo.
- Payment, fulfillment, Area Space, Area Leader, event, training, and therapy workflows do not currently have confirmed application services for frontend integration.

Completed commerce slice:

1. Added an order-intent reservation backend workflow after enquiry conversion, before payment.
2. Connected it to customer, product, membership eligibility, tier discounts, tier max-open-order rules, and order-window rules.
3. Added a frontend `/order-intents` demo page, converted-enquiry handoff action, and value metrics for reserved demand plus member savings.

Recommended next commerce slice:

1. Decide whether the next learning target is payment intent/proof-of-payment or fulfillment workflow.
2. Keep payment/proof-of-payment separate from order-intent creation so the reservation lifecycle remains reviewable.
3. Keep savings-window status as supporting membership context, but do not build full savings account management until account/deposit application services exist.

Why this order:

- It extends the current proven journey naturally: membership -> product -> customer -> enquiry -> conversion -> order intent.
- It validates buyer/operator behavior before committing to payment, fulfillment, or savings-led complexity.
- It avoids creating frontend-only workflows for backend modules that are not yet exposed.

## Role-Based UI Flows

The frontend now includes role-specific pages for AreaLeader, Facilitator, Member, and Guest flows. Navigation links are permission-gated using the existing `hasPermission()` pattern.

### AreaLeader

Routes:
- `/area-leader` — list all area leaders
- `/area-leader/[areaLeaderId]` — area leader details with rank promotion
- `/area-leader/area-spaces` — list all area spaces
- `/area-leader/area-spaces/[areaSpaceId]` — area space details with review/approval actions

Provider: `src/providers/AreaLeaders/` and `src/providers/AreaSpaces/`

Permissions: `Pages.AreaLeaders`, `Pages.AreaSpaces`, `Aqua.AreaLeaders.Manage`, `Aqua.AreaSpaces.Manage`

### Facilitator

Routes:
- `/facilitator` — list all facilitators
- `/facilitator/[facilitatorId]` — facilitator details
- `/facilitator/referrals` — list all referrals
- `/facilitator/referrals/[referralId]` — referral details with award confirmation

Provider: `src/providers/Facilitators/` and `src/providers/Referrals/`

Permissions: `Pages.Facilitators`, `Pages.Referrals`, `Aqua.Referrals.Confirm`

### Member

Routes:
- `/member` — member dashboard with orders, membership, and savings overview
- `/member/orders` — member's order history
- `/member/savings` — member's savings window status

Provider: reuses existing `OrderIntents` and `Memberships` providers

Permissions: `Pages.Orders`, `Aqua.Orders.ViewSelf`, `Pages.Memberships`

### Guest

Routes:
- `/catalog` — public product catalog
- `/contact` — public contact page
- `/signup` — registration flow (existing)

Permissions: none required for public pages

## Architecture Baseline

- TypeScript strict mode is required.
- Tailwind CSS is the primary styling solution.
- Runtime environment variables are validated with Zod in `src/shared/config`.
- Backend access goes through the shared Axios boundary in `src/shared/api`.
- ABP response envelopes and error envelopes are normalized centrally.
- Demo-critical API responses, starting with backend health, are validated at runtime before entering provider state.
- Auth and tenant readiness providers register access-token and `__tenant` resolvers with the shared Axios boundary.
- The app context bar keeps backend reachability, auth mode, and tenant mode explicit while host mode remains the safe default.
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
- Area Leaders: [http://localhost:3000/area-leader](http://localhost:3000/area-leader)
- Area Spaces: [http://localhost:3000/area-leader/area-spaces](http://localhost:3000/area-leader/area-spaces)
- Facilitators: [http://localhost:3000/facilitator](http://localhost:3000/facilitator)
- Referrals: [http://localhost:3000/facilitator/referrals](http://localhost:3000/facilitator/referrals)
- Member dashboard: [http://localhost:3000/member](http://localhost:3000/member)
- Member orders: [http://localhost:3000/member/orders](http://localhost:3000/member/orders)
- Member savings: [http://localhost:3000/member/savings](http://localhost:3000/member/savings)
- Public catalog: [http://localhost:3000/catalog](http://localhost:3000/catalog)
- Contact: [http://localhost:3000/contact](http://localhost:3000/contact)

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
- Add E2E tests for role-specific flows once Cypress or Playwright is configured.

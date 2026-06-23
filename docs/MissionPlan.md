# Mission Plan: Area Network, Referrals, Admin & the Enquiry Relationship

Branch: `feature/business-docs-gap-analysis`
Scope (confirmed): **Docs + Tests + Implementation**, extending existing docs.
Priority (confirmed): **Area Leaders, Facilitators, Referrals + their complete flow and relation to Enquiries + Customer, and a real Admin.** Savings is **deferred**.

---

## 0. Stack & findings (verified in code)

- **Stack:** ABP (ASP.NET Boilerplate) .NET 8 backend + Next.js frontend. Tests: xUnit/Shouldly/NSubstitute/EF-InMemory (backend), vitest (frontend).
- **Enquiry→Customer conversion is fake:** `Enquiry.ConvertToCustomer()` only flips `IsConverted`; `EnquiryAppService.ConvertToCustomerAsync` does **not** create a `Customer` (no `ICustomerRepository` injected). `Enquiry` already requires a `CustomerId`. This contradiction is fixed in Phase 4.
- **Zero Area Leader / Area Space / Facilitator / Referral code** — greenfield.
- **No real admin:** `TokenAuthController` exists (`/api/TokenAuth/Authenticate`), but **no business app service is authorized**, permissions cover only `Pages.Users/Roles/Tenants`, and the frontend has **no login page** (Auth provider is just a token holder).
- **Entity baseline:** existing aggregates use plain `Entity<int>` (not audited, not tenant-scoped). New aggregates will use the ABP standard below; existing ones are not retrofitted (KISS/YAGNI) — captured as an ADR.

---

## 1. Architecture principles applied (governing rules → concrete decisions)

**Clean Architecture / Layering / Hexagonal (Dependency Rule inward):**
`Core` (domain) ← `Application` (use cases) ← `EntityFrameworkCore` (adapters) ← `Web.Host`. Domain has **no** EF/framework refs. Repositories are **ports** in `Core`, EF implementations are **adapters** in `EntityFrameworkCore`.

**DDD building blocks (new Area-Network bounded context):**
- **Aggregate roots:** `AreaLeader`, `AreaSpace`, `Facilitator`, `Referral` as `FullAuditedAggregateRoot<int>, IMustHaveTenant` (audit trail via `IHasCreationTime`/`IHasModificationTime`, soft delete via `ISoftDelete`, tenant isolation via `IMustHaveTenant`, optimistic concurrency via `ConcurrencyStamp`).
- **Value Objects (immutable):** `Money(amount, ZAR)`, `Address`, `AreaLeaderRank`, `FacilitatorRank`, `LicenseType`.
- **Domain Services (stateless):** `ReferralAttributionService`, `CommissionCalculator`, `RankProgressionPolicy` (Open/Closed: add ranks without touching callers).
- **Domain Events (Event-Driven):** `EnquiryConvertedEvent`, `ReferralConfirmedEvent`, `AreaSpaceApprovedEvent`, `FacilitatorRankAchievedEvent`. Handlers do attribution/award side-effects — keeps aggregates decoupled (Law of Demeter, SRP).
- **Repositories (Core ports) + Specification pattern** for reusable queries (e.g. `SalesReadyEnquirySpec`, `FacilitatorsByRankSpec`).

**SOLID / Code quality:** one aggregate per file, guard-clause **Fail Fast** ctors/factories, constants for all money/threshold magic numbers (seeded, not hard-coded), functions < 30 lines, early returns, Composition over Inheritance (policies injected), Convention over Configuration (ABP dynamic API).

**CQRS-lite:** app services separate command methods (`Create/Approve/Convert/RecordReferral`) from query methods/DTOs (`GetNetworkOverview`). **DTOs never expose entities**; mapping via **AutoMapper `IObjectMapper`** (introduce profiles; existing manual mapping left as-is per KISS, noted in ADR).

**ABP-specific:** `PermissionManager`/`[AbpAuthorize]` RBAC, `IMultiTenant` isolation + `__tenant` header, `AbpException` hierarchy (reuse existing centralized exceptions), `ABP Migrator` for schema, seed data for roles/permissions and rank/commission config, Unit of Work (implicit in app services).

**Frontend (FSD + Next.js):** keep the existing pragmatic FSD layout (`src/shared` → `src/providers` → `src/components` → `app`), **Public API via `index.ts`**, unidirectional data flow (context+reducer), Server Components by default with Client isolation for interactive forms, `next/middleware` for 401/403 handling, Zod validation at boundaries, secrets via env only.

**Testing:** **TDD** (red-green-refactor) per aggregate/service, **Test Pyramid** (unit ≫ integration ≫ e2e), FIRST, mock external deps, edge cases + **property-based** for `CommissionCalculator`/rank math, target **85%+** on new business code.

**API/Security/Errors:** RESTful ABP endpoints documented in OpenAPI; proper status codes incl. ABP `401/403/404/449/410`; consistent `AbpError`/`ErrorResponse`; RBAC + least privilege + audit logging on all financial/approval actions.

---

## 2. Demo-critical flow (the money path)

```
Admin logs in (real JWT auth)
 → Admin registers Area Leader + approves Area Space
     (guards: 20+ interested, 4 presentations, 42h review, 20 startup orders)
   → Area Leader has Facilitators (under upline)
     → Facilitator generates a lead  ==> Enquiry (ReferredByFacilitatorId set)
        → follow-up → CONVERT
             ==> creates/links Customer + assigns membership tier   (fixes broken conversion)
             ==> raises EnquiryConvertedEvent
                 ==> ReferralAttributionService: Referral(Direct→Facilitator) + Referral(Indirect→AreaLeader)
                     ==> counts update → RankProgressionPolicy → rank up → CommissionCalculator award
Admin console shows the whole network: Area Leaders → Facilitators → referrals → commissions + enquiry pipeline
```

**Relationship model (resolves Member/Customer ambiguity):** a **Customer** is the person. A **Facilitator** and an **Area Leader** each reference a `CustomerId`. An **Enquiry** is a lead about a `Product` for a prospect `Customer`, optionally carrying `ReferredByFacilitatorId`. **Referral** links `ReferrerFacilitatorId → ReferredCustomerId` with a `SourceEnquiryId`. `Enquiry.AssignedToMemberId` is documented as the staff/owner handling the lead (renamed intent, not schema churn).

---

## 3. Phased implementation (TDD, atomic conventional commits)

Order is chosen so each phase compiles/tests green and builds toward the demo. Every commit: `dotnet build` + `dotnet test` green, one logical change, conventional message.

### Phase 1 — Documentation & ADRs (no code)
- `docs/technical-requirements.md` — ERD (real + new tables), per-table constraints, business rules cross-ref, NFRs.
- `docs/BusinessDocs/demo-analysis.md` — the analysis + demo script in §2.
- `docs/lean-startup.md` — product experiments (facilitator recruitment velocity, referral→conversion rate, area-leader growth) + Build-Measure-Learn.
- `docs/adr/` — ADR-001 new aggregates use `FullAuditedAggregateRoot`+`IMustHaveTenant` (existing entities not retrofitted); ADR-002 referral attribution via domain events; ADR-003 AutoMapper for new DTOs only.
- `docs/api/openapi.yaml` — curated spec incl. new Area-Network + TokenAuth endpoints.
- Refresh `docs/GapAnalysis.md`.
- Commits: `docs: ...` (one per artifact).

### Phase 2 — Facilitator + Referral bounded context
- Domain: `Facilitator`, `Referral` aggregates; `FacilitatorRank` VO; `RankProgressionPolicy`; `Money` VO; domain events; repository ports + specs. **Tests first.**
- Persistence: EF configs, `DbSet`s, repositories (adapters), migration `Add_AreaNetwork_Facilitators`.
- Application: `IFacilitatorAppService` (register, record referral, get) + DTOs + AutoMapper profile; authorized with new permissions (defined in Phase 5, referenced now).
- Commits: `test(facilitator): ...` → `feat(facilitator): add facilitator and referral aggregates` → `feat(facilitator): add ef config, repositories and migration` → `feat(facilitator): add application service and dtos`.

### Phase 3 — Area Leader + Area Space bounded context
- Domain: `AreaLeader`, `AreaSpace` aggregates; `AreaLeaderRank`/`LicenseType`/`Address` VOs; approval workflow with **Fail-Fast** guards (20+ interested, 4 presentations, 42h window, 20 startup orders); `AreaSpaceApprovedEvent`; 300-leader cap policy. **Tests first.**
- Persistence: EF configs, repositories, migration `Add_AreaNetwork_AreaLeaders`.
- Application: `IAreaLeaderApplicationAppService` (apply, review, record presentation, approve, promote) + DTOs + profile.
- Commits: `test(area): ...` → `feat(area): add area leader aggregate ...` → `feat(area): add area space application workflow` → `feat(area): add ef config, repositories and migration` → `feat(area): add application service and dtos`.

### Phase 4 — Enquiry ↔ Customer ↔ Referral wiring (core relationship)
- Domain: add `ReferredByFacilitatorId?` to `Enquiry`; raise `EnquiryConvertedEvent` on conversion; `CommissionCalculator` domain service. **Tests first.**
- Application: fix `ConvertToCustomerAsync` to create/link `Customer` + assign tier (inject `ICustomerRepository`/`IMembershipRepository`); `EnquiryConvertedEventHandler` → `ReferralAttributionService` (direct + indirect referral, count updates, rank eval, award). Migration `Add_Enquiry_ReferredBy`.
- Integration test: full path enquiry→convert→customer+referral→rank+commission (EF InMemory).
- Commits: `test(enquiry): cover conversion creates customer and referral` → `feat(enquiry): create customer on conversion` → `feat(area): attribute referrals on enquiry conversion` → `feat(area): add commission and award calculation`.

### Phase 5 — Real Admin (RBAC + auth + console)
- Backend: add business permissions (`Pages.Customers`, `Pages.Enquiries`, `Pages.Memberships`, `Pages.Products`, `Pages.AreaLeaders`(+`.Approve`), `Pages.Facilitators`, `Pages.Referrals`(+`.Confirm`), `Pages.Network.Dashboard`) in `AuthorizationProvider`; decorate app services with `[AbpAuthorize(...)]`; grant to seeded **Admin** role. **Least privilege + audit logging** on approvals/awards.
- Frontend: `/login` page → `/api/TokenAuth/Authenticate`, store token via existing `AuthProvider`; `middleware.ts` for 401→login / 403→forbidden; tenant `__tenant` header injection; authenticated admin shell + nav. New feature slices (Public API via `index.ts`): `area-leaders`, `area-spaces`, `facilitators`, `referrals` (list + review/approve/confirm actions).
- Commits: `feat(auth): add business permissions and authorize services` → `feat(auth): seed admin role grants` → `feat(frontend): add login and auth middleware` → `feat(frontend): add area network admin pages` (split per slice) → `test(...)` alongside.

### Phase 6 — Admin network dashboard
- Backend query service `GetNetworkOverviewAsync` (Area Leaders → Facilitators → referrals/commissions) — CQRS read model, Specification-backed.
- Frontend: network overview widget on home; wire metrics.
- Commits: `feat(area): add network overview query` → `feat(frontend): add network dashboard`.

### Deferred (explicitly out of demo scope)
Savings persistence/interest, product combos + dual pricing, order-cycle enforcement. Revisit after demo.

---

## 4. Testing strategy (per phase, 85%+ new code)
- **Unit:** aggregate invariants, VO immutability, approval guards, rank progression (property-based), commission math, event raising.
- **Integration:** app-service flows on EF InMemory, esp. the Phase 4 end-to-end path; authorization tests (permitted vs forbidden).
- **Frontend:** vitest for new reducers + critical components; login flow.
- **E2E:** noted as future (no Playwright configured) — not blocking the demo.

## 5. Pre-commit checklist (every commit)
Backend (from `AqualLifeStyle/9.4.2/aspnet-core`): `dotnet build` ✔ · `dotnet test` ✔.
Frontend (from `aqua-frontend`, if touched): `pnpm lint` ✔ · `pnpm test` ✔ · `pnpm build` ✔.
Only related files staged · conventional commit · one logical change.

## 6. Risks / assumptions
- Rank thresholds, commission %, and award amounts have conflicting figures across PDFs → sourced from seed config and flagged against `docs/ValidationPlan.md` (V-03), not hard-coded silently.
- Migrations are generated via `dotnet ef`; applying them is a deploy step (design-time factory present).
- New aggregates adopt the full ABP audit/tenant standard; existing entities are intentionally not retrofitted this pass (ADR-001).
- "Admin" for the demo = authenticated Admin-role user managing the network; multi-role RBAC beyond Admin is future.

## Execution order & gating
Core, end-to-end: **Phase 1 → 2 → 3 → 4 → 5 → 6.** I will pause for your approval before Phase 5 (auth/RBAC changes touch security surface) and confirm the demo script after Phase 4.

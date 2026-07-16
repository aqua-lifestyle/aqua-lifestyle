# Admin Module Audit

## Scope

This audit covers the merged ABP Framework backend and Next.js 16 frontend before the admin-management implementation. Business behavior was checked against `docs/BusinessDocs/domain-model.md`, `requirements.md`, and `workflows.md`, plus the existing RBAC audit.

## Existing Conventions

| Concern | Existing convention |
| --- | --- |
| Application services | Feature services use `XxxAppService`; focused services inherit `AqualLifeStyleAppServiceBase`, while ABP identity/tenant CRUD uses `AsyncCrudAppService`. |
| DTOs | `XxxDto`, `CreateXxxDto`, and focused command DTOs under a feature `Dto` namespace. Data annotations provide transport validation; domain methods enforce invariants. |
| Permissions | New business permissions are constants under nested groups in `AquaPermissions`; `AqualLifeStyleAuthorizationProvider` registers the hierarchy; `AquaRolePermissions` is the intended role mapping source. Legacy `PermissionNames.Pages_*` remains in older services. |
| Repositories | Domain interfaces extend `IRepository<TEntity, TKey>` and add only business-specific queries. EF implementations live in `AqualLifeStyle.EntityFrameworkCore`. |
| Authorization | Existing services mix class-level legacy permissions and method-level `AquaPermissions`. New admin endpoints need an explicit granular permission on every public method. |
| Errors | IDs and inputs use `AqualLifeStyleValidator`; missing entities use `AqualLifeStyleNotFoundException`; safe business failures use `UserFriendlyException`. Frontend errors are normalized through `getRequestErrorMessage`. |
| Mapping | ABP `IObjectMapper` and feature AutoMapper profiles map entities to DTOs. |
| Frontend API | `httpClient` wraps the authenticated Axios client. The interceptor adds the bearer token and current tenant header. Endpoints are centralized in `src/shared/api/endpoints.ts`. |
| Frontend forms | The project does not use `react-hook-form`. Existing forms use native `FormData`, Zod `safeParse`, shared fields, and toast/status feedback. Admin forms should follow that installed pattern. |
| Frontend tables/dialogs | Generic `DataTable`, native `<dialog>` through the shared `Dialog`, and shared `Button`, `Card`, `SelectField`, and `TextField` components. |
| Route protection | `app/admin/layout.tsx` wraps routes in `AdminGuard`; role normalization is centralized in `src/shared/auth/roles.ts`. Backend permissions remain the security boundary. |

## Existing Admin Capability

- A protected `/admin/dashboard` exists and composes live entity providers.
- The dashboard still falls back to demo metrics when required calls fail, which can misrepresent production state.
- Tenant `SystemAdmin` and built-in `Admin` role names normalize to the frontend admin role.
- Existing customer, user, tenant, area-leader, and facilitator services provide portions of the requested behavior, but use broad or mixed permissions.
- ABP users, roles, tenants, audit logs, and full auditing for area leaders/facilitators already exist.
- `Customer` is currently a plain `Entity<int>` with tenant ownership; it does not expose creation/modification audit fields.

## Missing Capability

- No backend `Admin` application module or granular admin CRUD permissions.
- No admin navigation shell or management pages beyond the dashboard.
- No customer import parser, preview contract, validation report, or transactional import endpoint.
- No tenant-aware admin customer creation contract. Existing customer creation assumes the current tenant and provisions placeholder identity behavior.
- No consolidated admin user workflow for granular role assignment, activation, deletion, or password reset.
- No admin member lifecycle workflow for suspension/reactivation and tier changes.
- No explicit admin operations for area-leader/facilitator approval or removal.
- No tenant leader association exists in the current tenant domain model.
- No admin-specific authorization, validation, audit-log, or import tests.

## Security Findings and Design Decisions

1. `Aqua.Admin` is currently registered for the host side only, but the frontend dashboard is also used by tenant `SystemAdmin`. Granular entity-management permissions will support the tenant side; tenant-management permissions remain host-only.
2. A tenant `SystemAdmin` may administer only its own tenant. Only a host admin may enumerate or mutate all tenants. Cross-tenant filters will never be disabled for a tenant session.
3. Every public admin service method will carry its own `[AbpAuthorize]` attribute. UI role checks are navigation convenience, not authorization.
4. Mutations will use ABP auditing plus structured application logs containing actor, tenant, operation, target, and before/after summaries. Passwords and uploaded file contents must never be logged.
5. Bulk import will use a two-step preview/import contract. The server is authoritative for parsing and validation, enforces a bounded file/row size, rejects duplicate emails, and revalidates on import.
6. Import will create both the ABP user and linked customer in one transaction. Row-level validation errors are returned without exposing internal exceptions.
7. Existing rank progression is business-rule driven. Admin promotion will evaluate the current progression policy; demotion/removal requires an explicit reason and will use domain methods rather than direct property writes.
8. Tenant leader assignment requires a persisted, tenant-owned relationship and cannot be represented safely as a frontend-only selection.
9. Customer auditing requires a schema migration if the entity is upgraded to `FullAuditedAggregateRoot<int>`; that migration must be reviewed and tested before deployment.

## Planned Implementation

- Extend `AquaPermissions.Admin` with granular dashboard, user, tenant, customer, area-leader, facilitator, and member operations.
- Add focused admin application services and DTOs while reusing existing managers, repositories, validation, and mapping.
- Add domain lifecycle methods and the minimum schema required for audited customers and tenant leader assignment.
- Replace dashboard fallback data with explicit unavailable states.
- Add a protected admin sidebar and focused management screens using the existing shared UI and Zod form patterns.
- Add negative authorization, tenant-boundary, validation, audit, import, and frontend interaction tests.

## Baseline Risks

- The repository retains mixed legacy/new permission systems; new code will use only granular `AquaPermissions.Admin.*` permissions.
- Whole-frontend coverage is below 80%, so new admin functionality needs direct tests rather than relying on the global percentage.
- The backend build has a pre-existing warning backlog; new code must not introduce additional actionable warnings.

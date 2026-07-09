# ADR-001: New aggregates use FullAuditedAggregateRoot + IMustHaveTenant

## Status

Accepted

## Context

The Area-Network bounded context (Facilitator, Referral, AreaLeader, AreaSpace) is greenfield.
The existing core entities (`Customer`, `Enquiry`, `OrderIntent`, `Membership`, `Product`) are plain
`Entity<int>` with `protected` constructors, private setters, manual `Create`/`CreateDraft` factories,
and explicit EF mapping. They are **not** tenant-scoped or audited.

ABP's `AbpZeroDbContext` already applies the `IMustHaveTenant` query filter to any entity implementing
`IMustHaveTenant`, and `FullAuditedAggregateRoot<T>` provides `CreationTime`, `LastModificationTime`,
`IsDeleted`, soft-delete, and `ConcurrencyStamp`.

## Decision

New Area-Network aggregates will derive from `FullAuditedAggregateRoot<int>` and implement
`IMustHaveTenant`. Existing entities are intentionally **not** retrofitted (KISS/YAGNI) — they keep
their current shape. Mixing tenant-scoped and non-tenant tables is supported by ABP; the only
requirement is that `TenantId` is populated on new entities.

`TenantId` is set automatically by ABP in application services via `AbpSession` (the repository base
calls `AbpSession.TenantId` on insert). In tests and seed code we must therefore run under a tenant
context (or set `TenantId` explicitly) so the query filter does not hide rows.

## Consequences

- New tables get audit columns and a soft-delete filter for free.
- Tenant isolation is enforced at the query level for new aggregates, matching multi-tenant intent.
- Domain layer stays free of EF/ABP framework types except these ABP base classes, which are
  acceptable infrastructure contracts (repositories remain ports in `Core`, EF impls in `EntityFrameworkCore`).
- Seed and integration tests must account for the tenant filter.

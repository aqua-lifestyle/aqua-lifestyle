# ADR-003: AutoMapper for new DTOs only

## Status

Accepted

## Context

The codebase currently uses manual `MapToDto` mapping inside each application service
(e.g. `EnquiryAppService.MapToDto`). AutoMapper is not yet used anywhere. The mission plan specifies
that **new** DTOs use AutoMapper's `IObjectMapper`.

## Decision

New Area-Network application services map entity → DTO via AutoMapper `Profile` classes registered in
`AqualLifeStyleApplicationModule` (which already scans the assembly for `Profile` subclasses). Existing
manual mappers are left as-is (KISS) and are not migrated. Application services resolve
`IObjectMapper` (injected by ABP) rather than `Mapper.Map`.

## Consequences

- New code follows the plan's CQRS-lite / DTO mapping convention.
- Existing services are untouched, reducing regression risk.
- Test module already disables the static mapper (`UseStaticMapper = false`), so profiles are resolved
  from the container — consistent with how the app runs.

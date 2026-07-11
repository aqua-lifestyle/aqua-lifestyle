# ADR-002: Referral attribution via domain events

## Status

Accepted

## Context

When an `Enquiry` is converted, the system must:

1. Create/activate the membership assignment on the referenced `Customer`.
2. Attribute a **direct** referral to the `Facilitator` that sourced the lead (`Enquiry.ReferredByFacilitatorId`).
3. Attribute an **indirect** referral to that Facilitator's `AreaLeader`.
4. Update referral counts, re-evaluate rank via `RankProgressionPolicy`, and award commission via `CommissionCalculator`.

If the aggregate raised all of this inline, `Enquiry` would need to know about `Facilitator`,
`AreaLeader`, `Referral`, ranks, and commissions — violating the Law of Demeter and SRP, and coupling
the enquiry bounded context to the network bounded context.

## Decision

`Enquiry.ConvertToCustomer()` raises a `EnquiryConvertedEvent` (carrying enquiry id, customer id,
product id, facilitator id, and conversion time). A `EnquiryConvertedEventHandler` (application layer)
resolves the facilitator/area-leader and delegates the side-effects to the stateless domain service
`ReferralAttributionService`, which produces `Referral` entities, updates counts, evaluates rank, and
raises `FacilitatorRankAchievedEvent` / `ReferralConfirmedEvent` for downstream award side-effects.

This keeps aggregates decoupled: `Enquiry` emits an intent; network aggregates react.

## Consequences

- Aggregates stay small and single-purpose (SRP, Demeter).
- Adding a new consequence of conversion is an Open/Closed change (new handler), not a change to `Enquiry`.
- Attribution logic is unit-testable in isolation via `ReferralAttributionService`.
- Eventual consistency is acceptable for referral counts/commissions in the demo (single UoW).

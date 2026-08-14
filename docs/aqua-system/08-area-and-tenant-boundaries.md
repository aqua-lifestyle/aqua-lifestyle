# Tenant and Area boundaries

This document defines the implemented separation between ABP Tenants and Aqua business Areas. It must be read with the payment/approval rules in [document 04](04-payments-approval-and-yoco.md).

## Authoritative model

```text
Tenant
= hard security and data-isolation boundary

Area
= business and administrative subdivision inside a Tenant

Programme network
= TenantId + Programme

Area administration
= TenantId + active AreaAdminAssignment
```

An Area is not an ABP Tenant and is not an `AreaSpace`. Adding Pretoria or Cape Town to Aqua does not require another Tenant. Same-Tenant, same-programme recruitment remains valid across Areas; cross-Tenant placement and qualification remain forbidden.

## Current production mapping

The technical Tenant remains `Default`. Its owner-authorised current business Area is `Johannesburg`, with stable code `JHB`.

The migration creates Johannesburg only for the existing non-deleted `Default` Tenant. It maps all of that Tenant's current customers to Johannesburg, records a system-introduction assignment baseline, and assigns active tenant users holding the `Admin` or `SystemAdmin` role to administer Johannesburg. It does not rename or merge a Tenant, invent earlier Area movements, or alter payment, participation, recruitment, approval, or commission facts.

The baseline timestamp identifies when reliable Area tracking entered this system. It is not Johannesburg's creation date, a member's historic movement date, or evidence about earlier commission periods.

An empty database receives no Area from the migration. Normal seeding provisions Johannesburg only for Aqua's `Default` Tenant. Any other Tenant must deliberately provision its own Areas.

## Customer ownership and movement

`Customer.AreaId` is the authoritative current Area. AQGreen and Onyx participation Area context is derived through the Customer, avoiding a second mutable Area value that could diverge.

Area is customer-wide rather than programme-specific. An authoritative Area move through either programme therefore changes the operational Area used for all of that Customer's participation administration, while each programme's placement and qualification graph remains independent and Tenant-scoped.

Every assignment and move is tenant-checked. A move closes the current effective-dated `CustomerAreaAssignment` and starts another; it does not erase the prior relationship. The production backfill is explicitly marked as a migration baseline because no authoritative earlier movement history exists.

An invited customer inherits the inviting Club Member's current active business Area. The backend derives that Area from the invitation's source participation and recruiter Customer; URL values, browser state, and client-supplied Area identifiers are not authoritative. Registration may use the invitation to create the customer's initial Area assignment, but it does not create programme placement. AQGreen records placement and re-resolves the current recruiter Area at `StartEntry`; direct Onyx does so only after verified payment creates the participation. Missing, inactive, or cross-Tenant Area evidence fails closed.

Current commission ledgers do not require a business-Area snapshot, so this change does not redesign them. If reporting later requires Area-at-cutoff, it must resolve the effective assignment or store an immutable snapshot deliberately; current Area must not be projected backward.

## Area administration

An administrator may hold active assignments for multiple Areas in the same Tenant. Approval discovery and decisions are scoped from persisted assignments and the participant Customer's Area. A client-supplied `AreaId` can only narrow an already-authorised scope; it cannot grant access.

Inactive Areas and revoked assignments provide no approval access. Host-wide access remains a separate, explicit permission and does not turn a host user into an Area Administrator.

The durable portal queue remains authoritative. Notification recipients are active users who both have an active assignment to the Customer's active Area and hold the approval permission.

## API and interface boundary

Member/customer projections expose `AreaId` and `AreaName`. Administration exposes assigned Areas and Area-scoped participation filtering. Customer creation and registration accept an optional Area identifier; omission is accepted only when the Tenant has exactly one active Area.

Some authentication contracts still use a legacy `area` URL/query label for the technical Tenant workspace (`Default`). Programme invitation preview exposes this workspace separately from the recruiter's business Area so authentication routing cannot be mistaken for Area assignment or authorization. Invitation registration derives its authoritative Tenant from the persisted invitation and rejects a conflicting ambient Tenant; the browser query does not select the security boundary.

## Operational checks

Before applying the migration:

1. confirm the intended Tenant still has technical name `Default`;
2. confirm the authorised administrator role assignments are the expected tenant-scoped `Admin`/`SystemAdmin` population;
3. back up the database and record the application/migration version;
4. verify all AQGreen and Onyx participations resolve to a same-Tenant Customer.

After applying it, verify one active `JHB` Area for `Default`, every current Default Customer has that Area and one current baseline assignment, and every authorised tenant administrator has one current Johannesburg assignment. Cross-Tenant relationships are blocked by domain validation and composite Area foreign keys.

Rollback deletes the new Area and assignment records and removes `Customer.AreaId`. It cannot preserve Area movements recorded after deployment. Prefer reviewed forward remediation or controlled restoration after production use.

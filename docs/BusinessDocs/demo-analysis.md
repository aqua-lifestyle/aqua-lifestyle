# Demo Analysis & Script

The "money path" the demo must show end-to-end.

## 1. Narrative

An authenticated **Admin** builds out the sales network and watches commissions accrue as leads convert.

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

## 2. Relationship model (resolves Member/Customer ambiguity)

- A **Customer** is the person (prospect or member). `Enquiry` already requires a `CustomerId` at creation.
- A **Facilitator** and an **Area Leader** each reference a `CustomerId` (the person filling that role).
- An **Enquiry** is a lead about a `Product` for a prospect `Customer`, optionally carrying `ReferredByFacilitatorId`.
- **Referral** links `ReferrerFacilitatorId → ReferredCustomerId` (direct) and `ReferrerAreaLeaderId → ReferredCustomerId` (indirect), with a `SourceEnquiryId`.

### Conversion semantics (decision)

`Enquiry.CustomerId` is already required, so conversion does **not** create a new person from nothing.
`ConvertToCustomerAsync` assigns/activates the membership **tier on the already-referenced Customer**
(injecting `ICustomerRepository`/`IMembershipRepository`). A new `Customer` is only created when a
conversion is explicitly linked to a still-nonexistent prospect — out of scope for the demo, where the
referenced Customer pre-exists. The event then drives referral attribution.

## 3. Demo script (step-by-step)

1. `POST /api/TokenAuth/Authenticate` with admin credentials → JWT.
2. `POST /api/services/app/AreaLeader/Apply` → register an area leader (customer + license).
3. `POST /api/services/app/AreaLeader/RecordPresentation` ×4, `RecordStartupOrder` ×20, `StartReview`, wait 42h-equivalent, `Approve` → `AreaSpaceApprovedEvent`.
4. `POST /api/services/app/Facilitator/Register` (under the area leader).
5. `POST /api/services/app/Enquiry/Create` with `ReferredByFacilitatorId` set.
6. `POST /api/services/app/Enquiry/ConvertToCustomer` → Customer membership assigned; `EnquiryConvertedEvent` fires.
7. `ReferralAttributionService` creates direct + indirect referrals, updates counts, evaluates rank, awards commission.
8. `GET /api/services/app/Network/GetOverview` → full network view with referral/commission metrics.

## 4. Acceptance

- Every step returns the correct status; unauthorized calls return 401/403 once auth is on (Phase 5).
- After step 6–7, the facilitator's `DirectReferrals` increases by 1 and an indirect referral exists for the area leader.
- Commission awarded equals the seeded rank award for the facilitator's resulting rank (flagged V-03).

# Business Workflows: aQua Lifestyle Club Platform

Workflows marked **(existing)** are supported by the current codebase; those marked **(target)** come from the business documents and are not yet implemented.

## 1. Enquiry-to-Customer Conversion (existing)

```mermaid
sequenceDiagram
    participant P as Prospect
    participant UI as Frontend
    participant API as EnquiryAppService
    participant DB as Database

    P->>UI: Submit enquiry
    UI->>API: Create enquiry
    API->>DB: Save (status: Pending)
    API-->>UI: Created

    Note over API: Admin responds / assigns to member
    API->>DB: Respond / AssignToMember

    loop Follow-ups
        API->>DB: RecordFollowUp(outcome)
        Note over API: Conversion probability updated
    end

    API->>DB: ConvertToCustomer
    DB-->>API: Customer created, enquiry marked converted
```

## 2. Member Registration (target — online via ALC admin)

```mermaid
sequenceDiagram
    participant P as Prospect
    participant WA as WhatsApp/Admin
    participant ALC as ALC Administration
    participant SMS as SMS Service

    P->>WA: Submit full name, ID number, cell number, email
    ALC->>P: Provide bank account details + payment deadline
    ALC->>P: Redirect to online membership presentation
    P->>WA: Submit proof of payment
    ALC->>ALC: Verify payment & data
    ALC->>SMS: Send welcome note
    ALC->>SMS: Confirm next payment date + combo collection
```

Registration requirements: ID number and copy, contact info + WhatsApp, bank confirmation letter (PDF), admin SMS acceptance. Registration payment must complete within 14 business days.

## 3. Monthly Savings Cycle (target — AQGreen)

```mermaid
sequenceDiagram
    participant M as Member
    participant API as Savings Service
    participant Admin as AQG Admin

    Note over M,API: 1st–15th: deposit window open
    M->>API: Deposit (min per tier: R100–R1500)
    API->>API: Validate window & minimum
    M->>Admin: Submit proof of payment

    Note over Admin: 17th–24th: locked — admin compiles & verifies proofs
    Admin->>API: Verify deposits, link references

    Note over API: Monthly close
    API->>API: Accrue 20% share pool (17% Business Premier)
    API->>API: Flag accounts below refund threshold (3-month rule)
```

Refund rule: savings below R1,500 (Standard) / R2,500 (Club Millionaire) / R4,500 (Business Premier) within 3 months → refund minus admin and branding costs. First-year payments locked 12 months; withdrawals in the first 6 months forfeit the 20% interest.

## 4. Monthly Order & Collection Cycle (target)

```mermaid
sequenceDiagram
    participant M as Member (Jasper/Onyx/AQGreen)
    participant AL as Area Leader
    participant Admin as aQua Admin
    participant AS as Area Space/Outlet

    M->>AL: Choose monthly combo & place order
    M->>AL: Complete payment (direct to company account) + proof
    AL->>Admin: Submit paid orders
    Admin->>Admin: Confirm payments
    Admin->>M: Notify order release date
    M->>AS: Collect order (Area verified, receipt confirmed)
```

Order calendar:

| Opening | Cut-off | Delivery |
|---------|---------|----------|
| 1st | 5th | 10th |
| 6th | 10th | 15th |
| 11th | 16th | 25th |

Onyx level order sets: Level 0 → Combo 4 (pay 25th, collect 5th); Levels 1–3 → Combo 4 ×4 (pay 30th, collect 10th); Levels 4–5 → Combo 4 ×8 (pay 1st, collect 16th). Area changes require 42 hours before order release.

## 5. Order Intent Lifecycle (existing)

```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> Reserved
    Reserved --> Completed
    Draft --> Cancelled
    Reserved --> Cancelled
    Completed --> [*]
    Cancelled --> [*]
```

## 6. Area Leader License Application (target)

```mermaid
sequenceDiagram
    participant O as Onyx Member
    participant Admin as aQua Head Admin

    Note over O: Precondition: 20+ interested people in the area
    O->>Admin: Report of all area members (direct & indirect)
    O->>Admin: Submit profile photo, ID, address, social media, signed agreement
    O->>Admin: Purchase startup stock (20 full combos) + branding suit
    Admin->>Admin: Verify commitment, membership & referrals
    Note over Admin: 42 hours to review area; 4 consecutive presentations required
    Admin->>O: Approve license (Entre R750 / Area Independent Leader R2500)
    Note over O: ~30 days for profile to complete → Level 1 Area Leader
    Admin->>Admin: Host introduction event for approved Area Leader
```

Rank progression (order targets): Ruby 20 → Emerald 60 → Premier 100 → Dimond 200 → VIP 400 → Presidential 1200 → Chairman's circle 3600 → Ambassador 18,000. Cap: 300 Area Leaders.

## 7. Facilitator Referral & Ranking (target)

```mermaid
flowchart LR
    A[Facilitator registers under Area Leader] --> B[Recruits members anywhere]
    B --> C[Redirects members to nearest Area Space]
    C --> D{Direct referrals per stage}
    D -->|10| Bronze[Bronze - R50]
    Bronze -->|10 more| Gold[Gold - R250]
    Gold -->|5| Pearl[Pearl - R1,250]
    Pearl -->|5| Sapphire[Sapphire - R2,500]
    Sapphire -->|20| Ruby[Ruby - R11,250]
    Ruby -->|10| Platinum[Platinum - R41,250]
    Platinum -->|Total 60| Premier[Premier T/60 - R68,750]
```

Awards are issued on completed direct referrals; incentives on completed indirect referrals. Facilitators must attend training workshops and may rate/report Area Leaders.

## 8. Business Premier Clubbing & Borrowing (target)

```mermaid
sequenceDiagram
    participant BP as Business Premier Member
    participant Club as Clubbing Pool
    participant aQua as aQua Admin

    BP->>Club: Join plan A–D (R6k / R12k / R20k / R50k)
    Note over Club: 6-month waiting, 3-month circle, lock by 6 companies
    Club->>aQua: Pooled allocation (e.g. R20k x 10 = R200k)
    aQua->>aQua: Purchase designated equipment directly
    aQua->>BP: Bi-annual progress reports with evidence

    Note over BP: Borrowing: 6 months saving without skipping
    BP->>aQua: Borrow request
    aQua->>BP: Loan (30% total charge, repay in 6–8 months)
```

## 9. Profit Share Distribution (target)

Company retains 60% of project profits; 40% goes to the liquidity share pool, paid bi-quarterly and shared equally among participants. Example: R600,000 profit → R240,000 pool distributed equally.

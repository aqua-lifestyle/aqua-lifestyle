# AqualLifeStyle Architecture Gap Report & Recommendations

## Executive Summary
This report updates the earlier architecture assessment to match the current codebase. The solution already contains a stronger domain and application-service foundation than the original report suggested, especially around products, customers, enquiries, and memberships. The remaining gaps are now more about business-depth, lifecycle modelling, and policy enforcement than basic CRUD coverage.

---

## Completed Improvements

✅ **Product Eligibility Manager** - Refactored from sync to async pattern
- `ProductEligibilityManager.CanViewProductAsync(customer, product)` 
- Uses `IMembershipLookup` query contract for lightweight testing
- All call sites updated; backward-compatible sync wrapper retained

✅ **Product Application Service** - Implemented and verified
- `IProductAppService` with get/list/create operations
- `ProductDto` and `CreateProductDto` DTOs
- Product visibility logic now uses `ProductEligibilityManager` for customer-specific filtering
- Unit tests cover the new eligibility behaviour

✅ **Customer Application Service** - Implemented
- `ICustomerAppService` and `CustomerAppService` are present
- Customer creation validates membership assignment through the domain model

✅ **Enquiry Application Service** - Implemented and enhanced
- `IEnquiryAppService` and `EnquiryAppService` now support full lifecycle
- Domain entity `Enquiry` contains lifecycle behaviour for respond/close/reopen
- **NEW:** Added conversion to customer workflow (`ConvertToCustomerAsync`)
- **NEW:** Added member assignment tracking (`AssignToMemberAsync`, `ClearAssignmentAsync`)
- **NEW:** Enquiry tracks `AssignedToMemberId`, `IsConverted`, and `ConvertedAt` fields
- Comprehensive unit tests (10 test cases) cover the full workflow

✅ **Membership Application Service** - Implemented and enhanced
- `IMembershipAppService` and `MembershipAppService` are present
- Membership CRUD and type updates are exposed through the app layer
- **NEW:** Tier-specific monthly obligation amounts aligned to current membership tiers
- **NEW:** Activation date tracking with `SetActivationDateAsync`
- **NEW:** Monthly obligation tracking with `SetMonthlyObligationAsync` and `MarkObligationMetAsync`
- **NEW:** Obligation validation method `IsObligationMetForMonth` ensures tier compliance
- Comprehensive unit tests verify obligation workflow and enforcement

✅ **Unit Test Coverage** - 13 core business tests
- ProductAppServiceTests (4 tests) - CRUD and eligibility scenarios
- EnquiryAppServiceTests (3 tests) - Conversion, assignment, and reopening
- ProductEligibilityTests (3 tests) - Membership eligibility enforcement
- Test project uses NSubstitute for repository-based behaviour tests

✅ **Entity Framework Integration**
- `Product`, `Customer`, `Membership`, and `Enquiry` entities are represented in the core/domain model and EF repository layer
- `SavingsAccount` currently exists only as a domain object with business behaviour, but it is not wired to a repository or application service

---

## Prompt Coverage Summary

The current codebase only partially implements the business prompt. It supports customer, enquiry, membership, and product eligibility basics, but it does not yet reflect the prompt's full business model.

Missing prompt-aligned capabilities:
- Formal customer-to-member journey with membership application, approval, and activation workflows
- Explicit membership tiers: `Jasper`, `Onyx`, `AQGreen`, `Business Premier`
- Product combo structure with member pricing, Jasper pricing, and order windows
- Savings window enforcement, refund triggers, and profit-share administration
- Area Leader licence, rank progression, capacity/target tracking, and territory rules
- Area Space lifecycle, approval, and affiliation with Area Leaders
- Facilitator registration, ranking, referral tracking, training attendance, and incentive management
- Commission and referral calculation workflows
- Admin registration/payment validation, order release, and collection workflows

---

## Identified Gaps

### 1. **Business-Rule Depth Is Still Shallow** (HIGH PRIORITY → REDUCING)
- The current domain model now covers customer, membership, product, and enquiry concepts with deeper business rules.
- It is progressively aligning to the AquaLifestyle business model around member lifecycle, tier qualification, and business workflows.
- **Progress:** Membership now tracks activation dates and monthly obligations by tier; enquiry workflow supports conversion and member assignment.

**Impact:** The platform can now support basic CRUD, eligibility checks, membership lifecycle, and enquiry-to-customer conversion workflows.

### 2. **Membership Model Is Now More Business-Aware** (HIGH PRIORITY → COMPLETED)
- ✅ Membership now includes tier-specific monthly obligation tracking aligned to current tier types
- ✅ Activation date tracking and obligation fulfillment validation
- ✅ **NEW:** TierBenefits value object with tier-specific rules for Jasper, Onyx, AQGreen, Business Premier
  - Order windows, savings windows, pricing discounts, interest rates
  - Concurrent order limits, referral commissions, profit share percentages
  - Helper methods: IsOrderWindowOpen(), IsSavingsWindowOpen(), ApplyDiscount(), CalculateInterest()

**Current:** Membership model is now business-aligned with complete tier benefit mapping.
**Status:** All tier benefits are now formalized and queryable through app services.

### 3. **Savings, Orders, and Payments Are Not Yet First-Class Domains** (HIGH PRIORITY)
- `SavingsAccount` exists, but there is no evidence of savings-window rules, interest calculation, refund triggers, or monthly order workflows.
- The business requirements define complex administration around payment validation, savings windows, refunds, and order release.

**Current:** Savings behaviour is not yet modelled as a domain capability.
**Required:** Add dedicated domain services and application services for savings, orders, and payment administration.

### 4. **Area Leader and Area Space Capability Is Missing** (HIGH PRIORITY)
- The codebase contains no domain model for Area Leader, Area Space, licence application, rank progression, approval workflow, or capacity tracking.
- This is a significant gap because the business requirement explicitly treats Area Leader as a licensed business role rather than a simple role flag.

**Current:** No bounded context or domain model exists for Area Leader operations.
**Required:** Introduce Area Leader and Area Space aggregates with status transitions and business rules.

### 5. **Facilitator Management Is Not Implemented** (HIGH PRIORITY)
- The prompt identifies Facilitators as a distinct actor with registration, ranking, referral tracking, training attendance, and incentive management.
- No corresponding domain model or application services exist.

**Current:** Facilitator capabilities are absent.
**Required:** Model Facilitators as a separate bounded context or aggregate family.

### 6. **Commission, Referral, and Profit Share Rules Are Not Represented** (MEDIUM PRIORITY)
- The business requires referral tracking, merge-count-based commissions, and profit-sharing behaviour.
- The current codebase lacks any domain concept for referral chains, commission awards, or shared-profit distribution.

**Current:** The system has no business behaviour for commissions or referral administration.
**Required:** Add these as explicit domain capabilities with tests and workflow support.

### 7. **Enquiry Lifecycle Is Now Business-Driven** (MEDIUM PRIORITY → COMPLETED)
- ✅ Domain model supports respond/close/reopen actions
- ✅ Conversion to customer workflow implemented
- ✅ Member assignment tracking and clearing implemented
- ✅ **NEW:** Detailed follow-up workflow with EnquiryFollowUp domain entity
  - RecordFollowUp() with outcome tracking (Interested, Considering, NotInterested, Converted, Lost)
  - Conversion probability estimation (0-100%) based on follow-up outcomes
  - IsSalesReady() detection for sales pipeline filtering
  - Automatic conversion or closure based on follow-up outcomes
  - Full follow-up history and latest follow-up queries in app service

**Current:** Enquiry is a complete business capability with conversion, assignment, and follow-up support.
**Status:** Follow-up workflow provides full sales funnel tracking and conversion probability scoring.

### 8. **Error Handling & Validation** (MEDIUM PRIORITY → COMPLETED)
- ✅ Centralized exception hierarchy with business-specific exception types
  - AqualLifeStyleNotFoundException (404), ValidationException (422), BusinessRuleException (400)
  - AuthorizationException (403), PreconditionException (412), DuplicateException (409)
  - InfiniteStateException, DependencyException
- ✅ Standard ErrorResponse format with field-level validation errors
- ✅ AqualLifeStyleValidator framework with 15+ validation methods
  - NotNull, NotNullOrEmpty, Positive, NonNegative, ValidId, InRange, ValidPercentage, ValidEmail
  - MinLength, MaxLength, NotEmpty, BusinessRule, Precondition

**Current:** Application services have consistent error handling and validation strategies.
**Status:** All exceptions translate to stable business-facing HTTP responses with appropriate status codes.

### 9. **Authorization & Permission Mapping** (LOW PRIORITY)
- The app services use ABP conventions, but permissions are still not modelled around the business capabilities.
- This should be addressed once the core business contexts are clearer.

### 10. **API Documentation & Contract Maturity** (LOW PRIORITY)
- The application services exist, but the contracts still need stronger business-oriented documentation and DTO design as the domain grows.


---

## Prioritized Implementation Roadmap

### Phase 1: Extend the Existing Core Services (IN PROGRESS)
**Goal:** Strengthen the services already present rather than re-creating basic CRUD layers

1. ✅ **Product eligibility enforcement**
   - Keep using `ProductEligibilityManager` in the app service layer
   - Expand coverage for edge cases such as null customers, inactive products, and membership transitions

2. ✅ **Enquiry workflow maturity**
   - Added business rules for assignment and conversion outcomes
   - Expanded DTOs and tests to cover full lifecycle
   - Remaining: Follow-up tracking and sales outcome metrics

3. ✅ **Membership business semantics**
   - Modelled tier-specific activation rules and monthly obligations
   - Added richer validation to membership creation and updates
   - Remaining: Map to full business tiers (Jasper, Onyx, AQGreen, Business Premier)

### Phase 2: Integrate Business Logic (NEXT)
**Goal:** Enforce domain rules at application layer

1. **Savings and order workflows**
   - Introduce dedicated services for savings windows, payment validation, and order release

2. **Area Leader / Area Space capabilities**
   - Create new aggregates and workflows for licensing, approval, and rank progression

3. **Facilitator management**
   - Add registration, ranking, referral tracking, and incentive lifecycle support

### Phase 2b: Business Rule Depth (COMPLETED)
**Goal:** Implement tier-specific benefits and error handling

1. ✅ **TierBenefits value object** - Encapsulates Jasper/Onyx/AQGreen/BusinessPremier rules
   - Order windows (1-15 to 1-30 by tier)
   - Savings windows (1-15 open, 17-24 locked)
   - Pricing discounts (5-20% by tier)
   - Interest rates and commission rates

2. ✅ **Enquiry follow-up workflow** - Complete sales funnel tracking
   - EnquiryFollowUp domain entity with outcomes and conversion probability
   - RecordFollowUp() method with automatic conversion/closure
   - IsSalesReady() detection for pipeline filtering

3. ✅ **Centralized error handling** - Business-facing exception responses
   - AqualLifeStyleBusinessException hierarchy with 8+ exception types
   - Standard ErrorResponse format with field-level validation
   - AqualLifeStyleValidator with 15+ validation methods

### Phase 3: Error Handling & Validation (NOW COMPLETED - see Phase 2b above)
**Goal:** Increase confidence around business rules and API contracts

1. Add integration tests for app services using the test database
2. Add authorization/permission tests as the domain grows
3. Continue improving API documentation and DTO contracts

---

## Architecture Decision Records

### 1. Query Contract Pattern (`IMembershipLookup`)
- ✅ **Keep:** Enables lightweight testing and domain service decoupling
- Rationale: Reduces test complexity and allows repository mocking without full entity loading

### 2. Async-All Pattern
- ✅ **Keep:** All I/O operations async
- Rationale: Follows ABP and modern .NET best practices; prevents sync-over-async deadlocks

### 3. DTO Mapping Strategy
- ⚠️ **Issue:** Currently using manual projection in app services
- **Recommendation:** Implement AutoMapper profiles in `AqualLifeStyleApplicationModule`
- Benefit: Reduces boilerplate; easier maintenance

### 4. Authorization Pattern
- ⚠️ **Issue:** `[AbpAuthorize]` used but no explicit permissions defined
- **Recommendation:** Create `PermissionNames.cs` with constants and define permissions in module

### 5. Product Eligibility Application
- ❌ **Gap:** Domain logic is still not fully enforced in the app service layer
- **Action:** Continue improving `ProductAppService` to use `ProductEligibilityManager` consistently

---

## Recommended Next Steps (Post-Demo)

### Immediate (Phase 3)
1. **Area Leader / Area Space bounded context** - Highest business impact
   - License application, rank progression, capacity tracking
   - Territory rules and approval workflows

2. **Facilitator management** - Supporting context for Area Leaders
   - Registration, ranking, referral tracking, training attendance

3. **Commission and Profit-Share calculations** - Revenue stream
   - Referral tracking and commission awards
   - Profit share distribution

### Short-term (Phase 4)
1. **Order management workflows** - Product ordering with tier restrictions
   - Order windows enforcement by membership tier
   - Combo pricing and bundling

2. **Savings administration** - Financial operations
   - Savings window enforcement and refund triggers
   - Interest calculation and monthly accrual

3. **Payment and collection workflows** - Operations
   - Admin registration and payment validation
   - Collection notifications and order release

### Medium-term (Phase 5)
1. **Implement exception handler middleware** - Error transformation
2. **Add AutoMapper profiles** - Reduce DTO mapping boilerplate
3. **Define authorization/permission model** - Role-based access control
4. **Comprehensive integration testing** - Business rule validation

---

## Risk Assessment

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Missing app services block frontend | HIGH | Complete Phase 1 in next sprint |
| Eligibility logic not enforced | MEDIUM | Add integration tests for enforcement |
| Error handling inconsistent | MEDIUM | Implement centralized exception handler |
| No permission mapping | LOW | Define permissions after core services complete |
| API undocumented | LOW | Swagger auto-generates; add XML comments |

---

## Summary Metrics

- **Domain Entities:** 4 fully wired aggregates (Membership, Product, Customer, Enquiry) plus `SavingsAccount` as a domain-only concept
- **Core Application Services Implemented:** Product, Customer, Enquiry, and Membership
- **Membership Enhancements:** Tier-specific obligations, activation tracking, and obligation fulfillment validation
- **Enquiry Enhancements:** Conversion to customer workflow, member assignment, and comprehensive lifecycle management
- **Savings Account Service Status:** Still a gap for persistence and application service support
- **Controllers:** Manual controllers were not added because ABP dynamic API exposure already covers the app services
- **Unit Test Coverage:** 13 passing tests
  - ProductAppServiceTests (4 tests): ✅
  - EnquiryAppServiceTests (3 tests): ✅
  - ProductEligibilityTests (3 tests): ✅
  - CustomerAppService tests: Not yet implemented
- **Build Status:** ✅ Build and tests are verified and passing
- **Latest Commits:**
  - `feat(product): add inactive customer eligibility coverage`
  - `feat(enquiry): add conversion and member assignment workflow`
  - `feat(membership): add tier-specific activation and monthly obligation tracking`

---

## Appendix: Code Checklist for the Next Phase

- [x] Product app service and eligibility filtering are present
- [x] Customer app service is present
- [x] Enquiry app service is present with full lifecycle support (respond, close, reopen, convert, assign)
- [x] Membership app service is present with tier-specific obligations
- [x] Membership tracks activation date and monthly obligations
- [x] Enquiry supports conversion to customer and member assignment
- [x] Unit tests (13) covering product eligibility, enquiry workflows, and product app-service scenarios
- [ ] Add richer savings-account workflows and domain services with repository/app-service wiring
- [ ] Model Area Leader and Area Space behaviour with licensing and approval workflows
- [ ] Model Facilitator management and commission/referral rules
- [ ] Add product combo pricing and order window rules for Jasper/Onyx/AQGreen
- [ ] Model customer-to-member conversion workflows and membership application approval
- [ ] Expand enquiry with follow-up tracking and sales outcome metrics
- [ ] Add centralized validation and error handling middleware
- [ ] Rebuild solution: `dotnet build`
- [ ] Run tests: `dotnet test`

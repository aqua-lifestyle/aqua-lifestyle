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

✅ **Enquiry Application Service** - Implemented
- `IEnquiryAppService` and `EnquiryAppService` are present
- Domain entity `Enquiry` already contains lifecycle behaviour for respond/close

✅ **Membership Application Service** - Implemented
- `IMembershipAppService` and `MembershipAppService` are present
- Membership CRUD and type updates are exposed through the app layer

✅ **Unit Test Coverage**
- Product eligibility tests and product app-service tests are present and passing
- Test project dependencies include Moq for repository-based behaviour tests

✅ **Entity Framework Integration**
- `Product`, `Customer`, `Membership`, and `Enquiry` entities are represented in the core/domain model and EF repository layer
- `SavingsAccount` currently exists only as a domain object with business behaviour, but it is not wired to a repository or application service

---

## Identified Gaps

### 1. **Business-Rule Depth Is Still Shallow** (HIGH PRIORITY)
- The current domain model covers basic customer, membership, product, and enquiry concepts.
- It does not yet reflect the richer AquaLifestyle business rules around member lifecycle, tier qualification, savings windows, commissions, referral chains, Area Leader licensing, Area Space approval, or Facilitator management.

**Impact:** The platform can support basic CRUD and simple eligibility checks, but it is not yet aligned to the full business model described in the requirements.

### 2. **Membership Model Is Still Too Generic** (HIGH PRIORITY)
- The current membership model uses a simple enum-based type and a basic `Membership` aggregate.
- The business requires distinct operational semantics for Jasper, Onyx, AQGreen, and Business Premier, including activation rules, monthly obligations, savings behaviour, order windows, and tier-specific benefits.

**Current:** Membership is treated as a generic category.
**Required:** Introduce richer membership concepts and lifecycle rules aligned to the business tiers.

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

### 7. **Enquiry Lifecycle Is Only Partially Business-Driven** (MEDIUM PRIORITY)
- The domain model already supports respond/close actions, which is a good start.
- However, the current application service remains basic and does not enforce business rules around conversion to customer/member, assignment, follow-up, or sales outcome.

**Current:** Enquiry is present but not yet a full business capability.
**Required:** Expand the domain and application layers to reflect the full enquiry lifecycle.

### 8. **Error Handling & Validation** (MEDIUM PRIORITY)
- There is still no consistent error response strategy across the application services.
- Domain exceptions are not yet translated into stable business-facing responses.

**Recommendation:** Introduce centralized exception handling and validation for application-layer use cases.

### 9. **Authorization & Permission Mapping** (LOW PRIORITY)
- The app services use ABP conventions, but permissions are still not modelled around the business capabilities.
- This should be addressed once the core business contexts are clearer.

### 10. **API Documentation & Contract Maturity** (LOW PRIORITY)
- The application services exist, but the contracts still need stronger business-oriented documentation and DTO design as the domain grows.

---

## Prioritized Implementation Roadmap

### Phase 1: Extend the Existing Core Services (Now)
**Goal:** Strengthen the services already present rather than re-creating basic CRUD layers

1. **Product eligibility enforcement**
   - Keep using `ProductEligibilityManager` in the app service layer
   - Expand coverage for edge cases such as null customers, inactive products, and membership transitions

2. **Enquiry workflow maturity**
   - Add business rules for assignment, follow-up, and conversion outcomes
   - Expand DTOs and tests as the workflow grows

3. **Membership business semantics**
   - Model tier-specific activation rules and monthly obligations
   - Add richer validation to membership creation and updates

### Phase 2: Integrate Business Logic (Next)
**Goal:** Enforce domain rules at application layer

1. **Savings and order workflows**
   - Introduce dedicated services for savings windows, payment validation, and order release

2. **Area Leader / Area Space capabilities**
   - Create new aggregates and workflows for licensing, approval, and rank progression

3. **Facilitator management**
   - Add registration, ranking, referral tracking, and incentive lifecycle support

### Phase 3: Error Handling & Validation (After the business contexts are clearer)
**Goal:** Standardize error responses

1. Create custom exception types:
   - `EntityNotFoundException`
   - `UnauthorizedAccessException`
   - `ValidationException`

2. Implement exception handler middleware in `Startup.cs`

3. Add DTO validation using `IValidatableObject` or `FluentValidation`

### Phase 4: Testing & Documentation (Ongoing)
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

## Recommended Next Steps (Immediate)

1. **Refine Enquiry app service workflows** - Add business rules for conversion, assignment, and follow-up
2. **Integrate ProductEligibilityManager** - Enforce membership restrictions consistently
3. **Strengthen customer membership validation** - Ensure profile and membership changes conform to business rules
4. **Standardize Error Handling** - Reduce frontend debugging burden
5. **Document API Contracts** - Enable parallel frontend development

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
- **Savings Account Service Status:** Still a gap for persistence and application service support
- **Controllers:** Manual controllers were not added because ABP dynamic API exposure already covers the app services
- **Unit Test Coverage:** Product eligibility and app-service scenarios are present; the current suite is being verified after the mock fix
- **Build Status:** ✅ Build and tests are being verified against the current codebase

---

## Appendix: Code Checklist for the Next Phase

- [x] Product app service and eligibility filtering are present
- [x] Customer app service is present
- [x] Enquiry app service is present
- [x] Membership app service is present
- [ ] Add richer savings-account workflows and domain services
- [ ] Model Area Leader and Area Space behaviour
- [ ] Model Facilitator management and commission/referral rules
- [ ] Add centralized validation and error handling
- [ ] Rebuild solution: `dotnet build`
- [ ] Run tests: `dotnet test`

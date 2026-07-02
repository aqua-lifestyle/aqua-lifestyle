# AqualLifeStyle Architecture Gap Report & Recommendations

## Executive Summary
This report outlines architectural gaps identified during domain refactoring and provides prioritized implementation recommendations. The solution follows ABP Classic patterns with async/await improvements in place. Key areas for immediate attention are missing application services and API coverage.

---

## Completed Improvements

✅ **Product Eligibility Manager** - Refactored from sync to async pattern
- `ProductEligibilityManager.CanViewProductAsync(customer, product)` 
- Uses `IMembershipLookup` query contract for lightweight testing
- All call sites updated; backward-compatible sync wrapper retained

✅ **Product Application Service** - Full layer implementation
- `IProductAppService` with GetAll/Get/Create operations
- `ProductDto` and `CreateProductDto` DTOs
- `ProductController` exposing `api/app/product/` endpoints
- Authorization via `[AbpAuthorize]` attribute

✅ **Unit Test Coverage**
- Product eligibility tests: 3 scenarios (19 total passing tests)
- Product app service tests: 2 scenarios with mocked repository
- Test project dependencies: Moq 4.20.72

✅ **Entity Framework Integration**
- `Product` entity with full persistence mapping
- `ProductRepository` implementing `IProductRepository`
- Database schema includes Products table with membership restriction support

---

## Identified Gaps

### 1. **Missing Application Services** (HIGH PRIORITY)
- ❌ `IEnquiryAppService` - No application layer for enquiries
- ❌ `ISavingsAccountAppService` - No savings account operations
- ❌ `IMembershipAppService` - Membership domain exists but no app service
- ❌ `ICustomerAppService` - Customer management not exposed

**Impact:** Incomplete API surface; frontend cannot perform core operations.

### 2. **Incomplete Product Eligibility Integration** (MEDIUM PRIORITY)
- Product eligibility manager exists but is **not integrated into ProductAppService**
- `ProductAppService.GetAllAsync()` returns all products without membership filtering
- Should validate product visibility per customer membership

**Current:** Public method access to all products
**Required:** Per-customer product filtering

### 3. **Enquiry Management Service** (HIGH PRIORITY)
- Domain entity `Enquiry` exists in DbContext
- No application service, DTOs, or repository implementation
- No controller endpoints

**Missing Components:**
- `IEnquiryAppService` interface
- `EnquiryAppService` implementation
- DTOs: `EnquiryDto`, `CreateEnquiryDto`, `RespondToEnquiryDto`
- Controller endpoints for CRUD + respond/close operations

### 4. **Savings Account Service** (MEDIUM PRIORITY)
- Domain entity `SavingsAccount` exists (implied from tests)
- No application layer discovered

**Missing Components:**
- `ISavingsAccountAppService`
- DTOs and controller endpoints
- Linked to customer and product membership eligibility

### 5. **Membership Management Gap** (MEDIUM PRIORITY)
- `Membership` domain + `MembershipBenefit` entities exist
- No application service for CRUD or membership assignment
- Benefits management not exposed

**Missing:**
- `IMembershipAppService` with benefits operations
- Controller for membership assignment to customers

### 6. **Customer Service Incomplete** (MEDIUM PRIORITY)
- `Customer` domain exists with email value object
- No app service for customer operations or profile management

### 7. **Error Handling & Validation** (MEDIUM PRIORITY)
- No consistent error response format across controllers
- No validation DTOs or exception mapping
- Domain exceptions not mapped to HTTP responses

**Recommendation:** Create `AppExceptionHandler` middleware for standardized error responses.

### 8. **Authorization & Permission Mapping** (LOW PRIORITY)
- `[AbpAuthorize]` attribute used but no explicit permission definitions
- No role-based access control per domain entity

### 9. **API Documentation** (LOW PRIORITY)
- Swagger/OpenAPI present but not fully configured
- No API contract documentation or client generation

### 10. **Migration Strategy** (LOW PRIORITY)
- EF Core migrations exist but no documented upgrade path
- No seed data strategy for test/prod environments

---

## Prioritized Implementation Roadmap

### Phase 1: Complete Core Application Services (Weeks 1-2)
**Goal:** Expose all domain entities through application layer

1. **EnquiryAppService** (HIGH)
   - Implement `IEnquiryAppService` with Create/Get/List/Respond/Close
   - Add DTOs: `EnquiryDto`, `CreateEnquiryDto`, `RespondToEnquiryDto`
   - Create `EnquiryController`
   - Add tests similar to `ProductAppServiceTests`

2. **MembershipAppService** (HIGH)
   - Implement `IMembershipAppService` with CRUD + benefits management
   - Add DTOs: `MembershipDto`, `CreateMembershipDto`, `MembershipBenefitDto`
   - Create `MembershipController`

3. **CustomerAppService** (HIGH)
   - Implement `ICustomerAppService` with CRUD
   - Add DTOs: `CustomerDto`, `CreateCustomerDto`, `UpdateCustomerDto`
   - Create `CustomerController`

### Phase 2: Integrate Business Logic (Week 2-3)
**Goal:** Enforce domain rules at application layer

1. **Product Eligibility Filtering**
   - Update `ProductAppService.GetAllAsync()` to accept optional `customerId`
   - Call `ProductEligibilityManager.CanViewProductAsync()` for each product
   - Return filtered list per customer membership

2. **Enquiry Eligibility Validation**
   - Validate customer can view product before creating enquiry
   - Implement `RespondToEnquiryAsync` with role-based access (admin/support only)

3. **Membership Assignment Validation**
   - Prevent customer from downgrading active membership
   - Enforce benefit rules per membership type

### Phase 3: Error Handling & Validation (Week 3-4)
**Goal:** Standardize error responses

1. Create custom exception types:
   - `EntityNotFoundException`
   - `UnauthorizedAccessException`
   - `ValidationException`

2. Implement exception handler middleware in `Startup.cs`

3. Add DTO validation using `IValidatableObject` or `FluentValidation`

### Phase 4: Testing & Documentation (Week 4)
**Goal:** Achieve >80% test coverage

1. Add integration tests for app services using test database
2. Add authorization/permission tests
3. Generate API documentation using Swagger

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
- ❌ **Gap:** Domain logic not enforced in app service layer
- **Action:** Inject `ProductEligibilityManager` into `ProductAppService`
- **Concern:** May require customer context in controller; consider middleware for ambient customer ID

---

## Recommended Next Steps (Immediate)

1. **Create IEnquiryAppService** - Unblocks frontend for enquiry feature
2. **Integrate ProductEligibilityManager** - Enforce membership restrictions
3. **Add ICustomerAppService** - Enable customer profile operations
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

- **Domain Entities:** 5 (Membership, Product, Customer, Enquiry, SavingsAccount)
- **Application Services Completed:** 1/5 (Product only)
- **Application Services Missing:** 4/5 (Enquiry, Membership, Customer, SavingsAccount)
- **Controllers Created:** 1/5 (ProductController only)
- **Unit Test Coverage:** 19/19 passing (domain + app service tests)
- **Build Status:** ✅ Clean (warnings only, no errors)

---

## Appendix: Code Checklist for Phase 1

- [ ] Create `EnquiryAppService`, `IEnquiryAppService`, DTOs
- [ ] Create `EnquiryController` with [AbpAuthorize]
- [ ] Add `EnquiryAppServiceTests` (min 3 scenarios)
- [ ] Create `MembershipAppService` + controller
- [ ] Create `CustomerAppService` + controller  
- [ ] Update `ProductAppService.GetAllAsync(customerId?)` with eligibility filtering
- [ ] Add integration tests for eligibility enforcement
- [ ] Rebuild solution: `dotnet build`
- [ ] Run tests: `dotnet test`
- [ ] Git commit: "Implement core application services"

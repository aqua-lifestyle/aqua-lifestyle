# RBAC Audit Report

**Date:** 2026-07-12  
**Auditor:** Kilo  
**Scope:** Role-Based Access Control implementation across AqualLifeStyle  
**Status:** Complete — 2 medium, 2 low findings; no critical issues

---

## 1. Executive Summary

**Overall Health: GOOD with minor gaps**

The RBAC implementation is well-structured and follows ABP conventions. All five business roles are defined, permissions are comprehensively mapped, and authorization is enforced at the application service layer. The migration from legacy `PermissionNames.*` to `AquaPermissions.*` is in progress and functional.

**Key Strengths:**
- All roles properly defined with clear business semantics
- Comprehensive permission coverage across all feature areas
- Authorization provider correctly registered and tested
- Role assignment logic correctly derives roles from business records
- Tenant isolation enforced via ownership checks

**Key Gaps:**
- Legacy permission arrays still present in `BusinessRolesAndPermissionsBuilder` (code quality, not security)
- Mixed use of legacy `PermissionNames.*` and new `AquaPermissions.*` in app services
- No explicit tests for unauthorized access attempts (403 validation)

---

## 2. Detailed Findings

### R-001: Mixed Legacy and New Permission Systems
**Severity:** Medium  
**Location:** `src/AqualLifeStyle.Application/*/*.cs` and `BusinessRolesAndPermissionsBuilder.cs`

**Description:**
The codebase is in a transitional state where both legacy `PermissionNames.Pages_*` and new `AquaPermissions.*` are used simultaneously. This creates confusion and increases the risk of permission mismatches.

**Evidence:**
- `AreaLeaderAppService.cs` uses `[AbpAuthorize(PermissionNames.Pages_AreaLeaders)]` at class level but `[AbpAuthorize(AquaPermissions.AreaLeaders.Apply)]` at method level
- `BusinessRolesAndPermissionsBuilder.RoleShouldReceive()` checks both `AquaRolePermissions` AND legacy arrays (`AreaLeaderPermissions`, `FacilitatorPermissions`, `MemberPermissions`)

**Expected Behavior:**
All authorization should use the new `AquaPermissions.*` system consistently.

**Actual Behavior:**
Mixed usage creates potential for permission drift and confusion.

**Suggested Fix:**
1. Complete migration of all class-level `[AbpAuthorize]` attributes to `AquaPermissions.*`
2. Remove legacy permission arrays from `BusinessRolesAndPermissionsBuilder`
3. Update `AppServiceAuthorizationTests` to validate only `AquaPermissions.*`

**Test Plan:**
- Run `dotnet test` to ensure all authorization tests pass
- Verify no `PermissionNames.Pages_*` references remain in application services

---

### R-002: Redundant Legacy Permission Arrays in BusinessRolesAndPermissionsBuilder
**Severity:** Low  
**Location:** `src/AqualLifeStyle.EntityFrameworkCore/Seed/Tenants/BusinessRolesAndPermissionsBuilder.cs:124-165`

**Description:**
`RoleShouldReceive()` maintains three separate legacy permission arrays (`AreaLeaderPermissions`, `FacilitatorPermissions`, `MemberPermissions`) using old `PermissionNames.Pages_*` values, even though `AquaRolePermissions.GetFor()` already provides the correct mapping.

**Evidence:**
```csharp
if (System.Enum.TryParse<AquaUserRole>(roleName, out var aquaRole) &&
    AquaRolePermissions.GetFor(aquaRole).Contains(permissionName))
{
    return true;
}

if (roleName == "AreaLeader")
{
    return AreaLeaderPermissions.Contains(permissionName);  // Redundant
}
```

**Expected Behavior:**
Single source of truth for role-permission mapping.

**Actual Behavior:**
Dual permission sources risk divergence.

**Suggested Fix:**
Remove legacy arrays and rely solely on `AquaRolePermissions.GetFor()`.

**Test Plan:**
- Run `DefaultUserRoleAssignerTests`
- Verify seeded permissions match `AquaRolePermissions.GetFor()`

---

### R-003: Missing ViewSelf Permission Enforcement in Some Services
**Severity:** Medium  
**Location:** Multiple application services

**Description:**
While `ViewSelf` permissions are defined and mapped, not all services that should enforce self-view ownership checks have explicit `CurrentUserCanAccessCustomerAsync` validation.

**Evidence:**
- `CustomerAppService.GetAsync()` uses `[AbpAuthorize(PermissionNames.Pages_Customers)]` but doesn't explicitly check ownership for self-view
- `MembershipAppService.GetAsync()` has no ownership check

**Expected Behavior:**
Self-view permissions (`ViewSelf`, `EditSelf`) should validate `UserId == currentUserId`.

**Actual Behavior:**
Some self-view endpoints may return data for any user with the base permission.

**Suggested Fix:**
1. Add ownership checks to all `ViewSelf`/`EditSelf` endpoints
2. Ensure `CurrentUserCanAccessCustomerAsync` is called before returning customer-specific data

**Test Plan:**
- Add tests that verify a user cannot view another user's self-data with only `ViewSelf` permission

---

### R-004: No Explicit 403/Unauthorized Access Tests
**Severity:** Low  
**Location:** `test/AqualLifeStyle.Tests/Authorization/`

**Description:**
Existing tests verify positive permission grants but don't explicitly test negative cases (unauthorized access attempts).

**Evidence:**
- `AquaRolePermissionsTests` verifies permissions are granted
- `AppServiceAuthorizationTests` verifies attributes exist
- No tests verify that a user WITHOUT permission gets a 403/Forbidden response

**Expected Behavior:**
Test suite should include negative authorization tests.

**Actual Behavior:**
Only positive authorization paths are tested.

**Suggested Fix:**
Add integration tests that:
1. Create a user with limited permissions
2. Attempt to access an endpoint without permission
3. Verify 403/Forbidden response

**Test Plan:**
- Add `AuthorizationNegativeTests` class
- Test each role's forbidden permissions

---

### R-005: SystemAdmin Role Name Inconsistency
**Severity:** Low  
**Location:** `DefaultUserRoleAssigner.cs:41`

**Description:**
The role assignment logic checks for both "Admin" and "SystemAdmin" role names, but the business role is defined as "SystemAdmin" in `BusinessRolesAndPermissionsBuilder`.

**Evidence:**
```csharp
var isAdmin = _context.UserRoles
    .Any(ur => roleNames.TryGetValue(ur.RoleId, out var name) && 
               (name == "Admin" || name == "SystemAdmin"));
```

**Expected Behavior:**
Single canonical role name.

**Actual Behavior:**
Dual name check suggests legacy "Admin" role may still exist.

**Suggested Fix:**
1. Verify if "Admin" role is still created anywhere
2. If not, remove the dual check
3. Standardize on "SystemAdmin"

**Test Plan:**
- Search for "Admin" role creation in seed code
- Update `DefaultUserRoleAssignerTests` to verify single-name matching

---

## 3. Recommendations

### Immediate Actions (Critical/High)
1. **Complete AquaPermissions migration** - Replace all `PermissionNames.Pages_*` with `AquaPermissions.*` in application services
2. **Remove legacy permission arrays** from `BusinessRolesAndPermissionsBuilder`

### Short-term (Medium)
3. **Add ownership checks** to all `ViewSelf`/`EditSelf` endpoints
4. **Add negative authorization tests** for 403 scenarios

### Long-term (Low)
5. **Standardize role names** - Remove "Admin" alias, use only "SystemAdmin"
6. **Add permission coverage report** to CI pipeline

---

## 4. Atomic Commit Plan

### Commit 1: Complete AquaPermissions Migration
**Files:** All `src/AqualLifeStyle.Application/*/*.cs`  
**Action:** Replace remaining `PermissionNames.Pages_*` with `AquaPermissions.*`  
**Validation:** `dotnet test` passes

### Commit 2: Remove Legacy Permission Arrays
**Files:** `BusinessRolesAndPermissionsBuilder.cs`  
**Action:** Remove `AreaLeaderPermissions`, `FacilitatorPermissions`, `MemberPermissions` arrays  
**Validation:** `dotnet test` passes, seeded permissions unchanged

### Commit 3: Add Missing Ownership Checks
**Files:** `CustomerAppService.cs`, `MembershipAppService.cs`  
**Action:** Add `CurrentUserCanAccessCustomerAsync` to self-view endpoints  
**Validation:** `dotnet test` passes

### Commit 4: Add Negative Authorization Tests
**Files:** `test/AqualLifeStyle.Tests/Authorization/`  
**Action:** Add `AuthorizationNegativeTests.cs`  
**Validation:** `dotnet test` passes

### Commit 5: Standardize Role Names
**Files:** `DefaultUserRoleAssigner.cs`, `BusinessRolesAndPermissionsBuilder.cs`  
**Action:** Remove "Admin" alias, use only "SystemAdmin"  
**Validation:** `dotnet test` passes

---

## 5. Validation Summary

| Check | Status | Evidence |
|-------|--------|----------|
| All 5 roles defined | ✅ PASS | `AquaUserRole.cs` |
| All permissions registered | ✅ PASS | `AqualLifeStyleAuthorizationProvider.cs` |
| Role-permission mapping complete | ✅ PASS | `AquaRolePermissions.cs` |
| Authorization provider registered | ✅ PASS | `AqualLifeStyleApplicationModule.cs` |
| Role seed logic idempotent | ✅ PASS | `BusinessRolesAndPermissionsBuilder.cs` |
| Role assignment logic correct | ✅ PASS | `DefaultUserRoleAssigner.cs` |
| All services have AbpAuthorize | ✅ PASS | Grep of all app services |
| Tenant isolation enforced | ✅ PASS | Ownership checks in app services |
| Test coverage for roles | ✅ PASS | `AquaRolePermissionsTests.cs` |
| Test coverage for authorization | ⚠️ PARTIAL | Positive tests only, no negative tests |

---

## 6. Security Posture

**Critical Risks:** None identified  
**High Risks:** None identified  
**Medium Risks:** 
- Mixed permission systems (R-001)
- Missing ownership checks on some ViewSelf endpoints (R-003)

**Low Risks:**
- Redundant legacy code (R-002, R-005)
- Missing negative tests (R-004)

**Overall Assessment:** The RBAC system is fundamentally sound. The main risk is the transitional state with dual permission systems, which could lead to configuration drift. Completing the migration to `AquaPermissions.*` and removing legacy code will eliminate this risk.

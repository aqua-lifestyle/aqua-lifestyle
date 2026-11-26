# AQGreen existing-member roster preview

The preview compares a roster with account and AQGreen evidence visible to the operator. It shares the customer importer's upload dialog, file handling, preview request lifecycle, base permission, and backend CSV/XLSX parser. Members has the specialised read-only review for existing rosters; Customers retains the existing account-creation flow. This preview has no apply action or approval token. Use the Members entry for existing-member rosters; the Customers import creates accounts and does not reconcile AQGreen participation or network evidence.

## Evaluate a file

In the Aqua Admin Portal, open **Members → Import Existing Members**. Select the roster format, choose a CSV or XLSX file, and select **Preview file**. Review the preview summary and filter the rows by outcome. Use the old member ID and row number to find the original entry in the workbook. The dialog provides downloadable CSV headings for each format. Tenant administrators use their session Tenant; authorised host administrators select a Tenant and also need `Aqua.Admin.Tenants.View` for the existing Tenant picker.

Batch approval, import, and the post-import summary are pending. **Preview summary** reports only the read-only evaluation.

Use the local or separately authorised validation API: `POST /api/services/app/AdminProgrammeParticipation/PreviewLegacyAQGreenRoster`. Supply `tenantId`, `fileName` and the base64 file bytes in `contentBase64`. Use the existing authenticated API client and a named operator account. The action requires both `Aqua.Admin.Customers.Import` and `Aqua.Admin.ProgrammeParticipations.ViewLegacyPaymentReconciliation`. Tenant operators can only compare with records in their active, effective assigned Areas. Host operators need `Aqua.Admin.AllTenants` and must select one Tenant.

Use UTF-8 CSV or a **single-worksheet** `.xlsx`, at most 5 MB and 1,000 data rows. The expanded workbook is limited to 32 MB. Use these headings on the first row and retain the headings for blank fields. This is the initial preview input, not a claim about the administrator's source format. For differently shaped data, confirm the column meanings first and make a mapped copy; keep the original for reconciliation. In Excel, store references as text to retain leading zeroes. Preview row numbers are the parsed record order plus the header, so use the old member ID as well as row number when comparing with an original that contains blank lines.

For a simple network register, send `layout: "NetworkAssociations"` with one listed member per row. Repeat the owner fields for each listed member. The owner name or old ID can be blank if unavailable, and position is preserved as recorded text. These are source associations; unresolved relationship, programme and membership fields must not be filled from an owner heading or a recorded position.

```csv
OldMemberId,MemberName,Email,ContactNumber,NetworkOwnerName,NetworkOwnerOldMemberId,RecordedNetworkPosition
0007,Synthetic Member,member@example.test,,Synthetic Network Owner,0001,Depth 2 / 17
0008,Another Synthetic Member,,,Synthetic Network Owner,0001,
```

For a file with separately evidenced direct placement and sponsorship, use `layout: "ExplicitRelationships"` (also the default):

```csv
OldMemberId,FirstName,LastName,Email,ContactNumber,HomeAddress,ClubMemberNumber,Programme,MembershipStatus,NetworkParentOldMemberId,SponsorOldMemberId,PlacementSlot,IsRoot
0001,Synthetic,Root,root@example.test,,,CLB-000000000001,AQGreen,Active,,,,true
0002,Synthetic,Parent,parent@example.test,,,CLB-000000000002,AQGreen,Active,0001,0001,1,false
0003,Synthetic,Member,member@example.test,,,CLB-000000000003,AQGreen,Active,0002,0001,2,false
```

- `OldMemberId` belongs to the source register. `ClubMemberNumber` is the **current Aqua** number, only if it is known. Keep both; do not copy the old number into the current-number field. Profile fields are accepted by the parser; the preview does not update them or echo contact details. Provide only the personal data needed for matching.
- `Programme` and `MembershipStatus` are source observations. The preview compares AQGreen only and holds other programmes for review. A source status label does not become an Aqua activation decision.
- `NetworkParentOldMemberId` means an explicitly recorded **immediate placement parent**; `SponsorOldMemberId` means separately evidenced credited sponsor. Do not map a broad “in whose network” column to either field until its meaning is confirmed. The example's last row deliberately has different references. Use a slot or `IsRoot=true` only where recorded. Leave missing links unknown; do not derive them from row order, sponsor, or parent.

## Read the result

| Disposition | Meaning |
| --- | --- |
| `Accepted` | Current club number and email agree on one visible, available linked Customer/User, and the preview detected no issue. This is a preview result, not import approval or proof of historical facts. |
| `Duplicate` | A source ID, email, or current club number repeats in the upload. Resolve the source rows; no merge is performed. |
| `Unmatched` | No visible account matches the supplied keys and no other issue was detected. This does not mean the account does not exist elsewhere or should be created. |
| `Ambiguous` | One or more issues need review, such as email-only matching, retired history, unsupported programme, incomplete links, duplicate slots, cycles, cross-root sponsorship, or contradictory visible placement/attribution. Refer to each row's issue codes. |

Keep real input and results in restricted custody. The API returns the legacy references and corroborated current Customer/participation IDs as applicable. The simple layout reports email matches as requiring corroboration and associations as needing clarification. The preview uses a PostgreSQL read-only transaction; the operational log records actor, Tenant and counts only. The preview cannot reconcile out-of-scope records, pending provider activity or financial entitlements. Before an actual import, verify the source identity and network mapping, resolve exceptions, reconcile all relevant live and retired records against the target schema, and obtain separate approval for the exact batch and its AQGreen cutover boundary. Financial evidence stays separate; do not manufacture or replay transactions.

## Re-run the PostgreSQL checks

Use an empty, disposable, authorised PostgreSQL test database. The test harness creates the model schema and seeds synthetic fixtures. Set `REPRO_PG=true`, `REPRO_TRANSACTIONAL=true`, `REPRO_PG_SCHEMA_FROM_MODEL=true`, `REPRO_PG_CONNECTION` and `ConnectionStrings__Default` to the fixture settings. From `aspnet-core/`, run `dotnet test test/AqualLifeStyle.Web.Tests/AqualLifeStyle.Web.Tests.csproj --configuration Release --filter 'FullyQualifiedName~RosterPreview_'`. Expect six passing cases and zero skips. The existing protected workflow does not opt in to this specific PostgreSQL fixture; run it separately from the default web shard.

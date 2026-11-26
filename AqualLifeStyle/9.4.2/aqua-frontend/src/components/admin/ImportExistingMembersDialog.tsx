"use client";

import { useState } from "react";

import { httpClient } from "@/src/shared/api";
import { Button, DataTable, DialogActions, SelectField, StatusMessage } from "@/src/shared/ui";
import { AdminAreaSelectionField } from "./AdminAreaSelectionField";
import { ImportSpreadsheetDialog, ImportSpreadsheetPicker, useSpreadsheetPreview } from "./ImportSpreadsheetDialog";

type Layout = "NetworkAssociations" | "ExplicitRelationships";
type Disposition = "Accepted" | "Duplicate" | "Unmatched" | "Ambiguous";

type RosterRow = {
  rowNumber: number;
  oldMemberId: string | null;
  programme: string | null;
  membershipStatus: string | null;
  networkOwnerName: string | null;
  networkOwnerOldMemberId: string | null;
  recordedNetworkPosition: string | null;
  networkParentOldMemberId: string | null;
  sponsorOldMemberId: string | null;
  placementSlot: string | null;
  isRoot: string | null;
  matchedCustomerId: number | null;
  existingParticipationStatus: string | null;
  disposition: Disposition;
  issues: string[];
};

type RosterPreview = {
  tenantId: number;
  acceptedRows: number;
  duplicateRows: number;
  unmatchedRows: number;
  ambiguousRows: number;
  rows: RosterRow[];
};

const layouts = {
  NetworkAssociations: {
    label: "Listed under a member’s network",
    headers: "OldMemberId,MemberName,Email,ContactNumber,NetworkOwnerName,NetworkOwnerOldMemberId,RecordedNetworkPosition",
  },
  ExplicitRelationships: {
    label: "Recorded placement and sponsorship",
    headers: "OldMemberId,FirstName,LastName,Email,ContactNumber,HomeAddress,ClubMemberNumber,Programme,MembershipStatus,NetworkParentOldMemberId,SponsorOldMemberId,PlacementSlot,IsRoot",
  },
} satisfies Record<Layout, { label: string; headers: string }>;

const outcomes = {
  Accepted: "No preview issues",
  Duplicate: "Duplicate",
  Unmatched: "Unmatched",
  Ambiguous: "Needs clarification",
} satisfies Record<Disposition, string>;

const issueMessages: Record<string, string> = {
  AccountMatchRequiresCorroboration: "An existing account may match. Check the current Aqua member number and email.",
  AccountInactiveDeletedOrUnverified: "The matching account is inactive, removed, or unverified.",
  NoVisibleAccountMatch: "No matching account was visible in your scope.",
  RepeatedOldMemberId: "The old member ID is repeated in the file.",
  RepeatedEmail: "The email address is repeated in the file.",
  RepeatedClubMemberNumber: "The current Aqua member number is repeated in the file.",
  NetworkAssociationNeedsClarification: "The listed network does not establish a placement or sponsor.",
  NetworkAssociationOwnerUnknown: "The person whose network is listed was not provided.",
  ProgrammeRequiresReview: "The AQGreen programme is missing or needs verification.",
  ExistingPlacementConflict: "The supplied placement conflicts with a recorded Aqua placement.",
  ExistingCreditedSponsorConflict: "The supplied sponsor conflicts with a recorded Aqua sponsor.",
  ExistingPlacementSlotOccupied: "The supplied slot is already occupied in Aqua.",
};

const humanize = (value: string) => value.replace(/([a-z])([A-Z])/g, "$1 $2");
const issueLabel = (code: string) => Object.hasOwn(issueMessages, code) ? issueMessages[code] : humanize(code);

const previewColumns = (layout: Layout) => [
  { header: "Row", key: "rowNumber", sortable: true },
  { header: "Old member ID", key: "oldMemberId", sortable: true, render: (row: RosterRow) => row.oldMemberId || "Not supplied" },
  { header: "Account match", key: "matchedCustomerId", render: (row: RosterRow) => <div>
    {row.matchedCustomerId == null ? "No confirmed match" : `Aqua customer #${row.matchedCustomerId}`}
    {row.existingParticipationStatus ? <p className="text-muted-foreground">AQGreen in Aqua: {humanize(row.existingParticipationStatus)}</p> : null}
  </div> },
  { header: layout === "NetworkAssociations" ? "Recorded network association" : "Source membership and network", key: "network", render: (row: RosterRow) => layout === "NetworkAssociations" ? <div className="max-w-64 wrap-anywhere">
    <p>Listed under: {[row.networkOwnerName, row.networkOwnerOldMemberId].filter(Boolean).join(" · ") || "Not supplied"}</p>
    <p>Recorded position: {row.recordedNetworkPosition || "Not supplied"}</p>
  </div> : <div className="max-w-64 wrap-anywhere">
    <p>Programme: {row.programme || "Not supplied"}</p>
    <p>Source status: {row.membershipStatus || "Not supplied"}</p>
    <p>Placement parent: {row.networkParentOldMemberId || "Not supplied"}</p>
    <p>Credited sponsor: {row.sponsorOldMemberId || "Not supplied"}</p>
    <p>Placement slot: {row.placementSlot || "Not supplied"}</p>
    <p>Root as recorded: {row.isRoot || "Not supplied"}</p>
  </div> },
  { header: "Outcome", key: "disposition", render: (row: RosterRow) => outcomes[row.disposition] ?? "Unknown outcome" },
  { header: "Review issues", key: "issues", render: (row: RosterRow) => row.issues.length ? (
    <ul className="max-w-72 list-disc space-y-1 wrap-anywhere pl-4">{row.issues.map((issue) => <li key={issue}>{issueLabel(issue)}</li>)}</ul>
  ) : "None reported" },
];

const RosterReview = ({ preview, layout }: { preview: RosterPreview; layout: Layout }) => {
  const [filter, setFilter] = useState("");
  return <section aria-label="Existing member preview" className="space-y-4">
    <h3 className="font-semibold">Preview summary</h3>
    <div role="status">
      <p>{preview.rows.length} rows checked. No members were imported.</p>
      <dl className="mt-2 grid grid-cols-2 gap-2 text-sm sm:grid-cols-4">
        <div><dt>No preview issues</dt><dd className="font-semibold">{preview.acceptedRows}</dd></div>
        <div><dt>Duplicate</dt><dd className="font-semibold">{preview.duplicateRows}</dd></div>
        <div><dt>Unmatched</dt><dd className="font-semibold">{preview.unmatchedRows}</dd></div>
        <div><dt>Needs clarification</dt><dd className="font-semibold">{preview.ambiguousRows}</dd></div>
      </dl>
    </div>
    <SelectField label="Filter by outcome" name="rosterOutcome" onChange={(event) => setFilter(event.target.value)} value={filter}>
      <option value="">All outcomes</option>
      {Object.entries(outcomes).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
    </SelectField>
    <DataTable key={filter} columns={previewColumns(layout)} data={preview.rows.filter((row) => !filter || row.disposition === filter)}
      emptyState="No rows for this outcome." keyExtractor={(row) => row.rowNumber} label="Existing member preview results" />
  </section>;
};

export const ImportExistingMembersDialog = ({ fixedTenantId }: { fixedTenantId?: number }) => (
  <ImportSpreadsheetDialog title="Import Existing Members">
    {(close) => <ExistingMemberRosterPreview close={close} fixedTenantId={fixedTenantId} />}
  </ImportSpreadsheetDialog>
);

const ExistingMemberRosterPreview = ({ fixedTenantId, close }: { fixedTenantId?: number; close: () => void }) => {
  const [selectedTenant, setSelectedTenant] = useState("");
  const [layout, setLayout] = useState<Layout>("NetworkAssociations");
  const { file, preview, error, loading, clearPreview, selectFile, rejectFile, requestPreview } = useSpreadsheetPreview<RosterPreview>();
  const tenantId = fixedTenantId ?? Number(selectedTenant);
  const validTenant = Number.isInteger(tenantId) && tenantId > 0;

  const previewFile = () => {
    if (!validTenant) return;
    void requestPreview(async (upload) => {
      const input = { ...upload, layout, tenantId };
      const report = await httpClient.post<RosterPreview, typeof input>(
        "/api/services/app/AdminProgrammeParticipation/PreviewLegacyAQGreenRoster", input);
      if (!report || report.tenantId !== tenantId || !Array.isArray(report.rows)) {
        throw new Error("Unexpected roster preview response.");
      }
      return report;
    }, "The roster could not be previewed. Check the format and selected tenant, then try again.");
  };

  return <>
    <StatusMessage title="Preview only" tone="info">Review the uploaded rows here. Batch approval and import are not available yet.</StatusMessage>
    <div className="grid gap-3 sm:grid-cols-2">
      <AdminAreaSelectionField fixedAreaId={fixedTenantId} scopeLabel="Tenant" value={selectedTenant}
        onChange={(value) => { setSelectedTenant(value); clearPreview(); }} />
      <SelectField label="Roster format" name="rosterLayout" value={layout}
        onChange={(event) => { setLayout(event.target.value as Layout); clearPreview(); }}>
        {Object.entries(layouts).map(([value, option]) => <option key={value} value={value}>{option.label}</option>)}
      </SelectField>
    </div>
    <p className="text-sm text-muted-foreground">Use the first format if the file only says whose network a member appears under. Use recorded placement and sponsorship only when those relationships were separately confirmed.</p>
    <a className="self-start text-sm text-accent underline" download="aqgreen-member-roster-template.csv"
      href={`data:text/csv;charset=utf-8,${encodeURIComponent(layouts[layout].headers + "\r\n")}`}>Download CSV headings for this format</a>
    <ImportSpreadsheetPicker file={file} inputLabel="Existing member roster file" onSelect={selectFile} onReject={rejectFile}>
      Use one UTF-8 CSV or single-sheet XLSX file, up to 5 MB and 1,000 rows. Keep the headings and store old IDs as text.
    </ImportSpreadsheetPicker>
    {error ? <StatusMessage title="Preview unavailable" tone="error">{error}</StatusMessage> : null}
    {preview ? <RosterReview preview={preview} layout={layout} /> : null}
    <DialogActions>
      <Button onClick={close} variant="ghost">Close</Button>
      <Button disabled={!file || !validTenant || loading} isLoading={loading} onClick={previewFile}>Preview file</Button>
    </DialogActions>
  </>;
};

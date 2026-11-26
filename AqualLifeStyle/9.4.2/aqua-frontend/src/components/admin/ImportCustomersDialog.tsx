"use client";

import { AlertTriangle, CheckCircle2 } from "lucide-react";
import { useMemo, useState } from "react";

import { useAuthState } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Button, DataTable, DialogActions, StatusMessage } from "@/src/shared/ui";
import { CUSTOMER_IMPORT_PERMISSION, ImportSpreadsheetDialog, ImportSpreadsheetPicker, useSpreadsheetPreview } from "./ImportSpreadsheetDialog";

type ImportError = {
  field: string;
  message: string;
  rowNumber: number;
};

type PreviewRow = {
  contactNumber: string;
  email: string;
  firstName: string;
  homeAddress: string;
  isActive: boolean;
  lastName: string;
  membershipId: number | null;
  rowNumber: number;
};

type ImportPreview = {
  canImport: boolean;
  errors: ImportError[];
  fileName: string;
  previewId: string | null;
  rows: PreviewRow[];
  totalRows: number;
  validRows: number;
};

type ImportResult = {
  errors: ImportError[];
  failedRows: number;
  importedRows: number;
  totalRows: number;
};

type ImportCustomersDialogProps = {
  onImported?: () => void | Promise<void>;
};

export const ImportCustomersDialog = ({ onImported }: ImportCustomersDialogProps) => {
  const { session } = useAuthState();
  const user = session?.user;
  if (!user?.permissions?.includes(CUSTOMER_IMPORT_PERMISSION)) return null;

  return <ImportSpreadsheetDialog key={`${user.id}:${user.tenantId}`} title="Import customers">
    {(close) => <CustomerImportContent close={close} onImported={onImported} />}
  </ImportSpreadsheetDialog>;
};

const CustomerImportContent = ({ close, onImported }: ImportCustomersDialogProps & { close: () => void }) => {
  const { file, preview, error, setError, loading, selectFile, rejectFile, requestPreview } = useSpreadsheetPreview<ImportPreview>();
  const [result, setResult] = useState<ImportResult | null>(null);
  const [isImporting, setIsImporting] = useState(false);

  const columns = useMemo(() => [
    { header: "Row", key: "rowNumber", sortable: true },
    {
      header: "Customer",
      key: "firstName",
      render: (row: PreviewRow) => <span className="font-medium">{row.firstName} {row.lastName}</span>,
    },
    { header: "Email", key: "email", sortable: true },
    { header: "Contact number", key: "contactNumber", sortable: true },
    { header: "Home address", key: "homeAddress" },
    {
      header: "Membership",
      key: "membershipId",
      render: (row: PreviewRow) => row.membershipId ?? "None",
    },
    {
      header: "Status",
      key: "isActive",
      render: (row: PreviewRow) => row.isActive ? "Active" : "Inactive",
    },
  ], []);

  const confirmImport = async () => {
    if (!preview?.canImport || !preview.previewId || isImporting) return;
    setIsImporting(true);
    setError(undefined);
    try {
      const response = await httpClient.post<ImportResult, { previewId: string }>(
        "/api/services/app/CustomerImport/Import",
        { previewId: preview.previewId },
      );
      setResult(response);
      if (response.importedRows > 0) await onImported?.();
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError, "The import could not be completed."));
    } finally {
      setIsImporting(false);
    }
  };

  return (
    <div className="flex flex-col gap-5">
      {!result && !isImporting ? (
        <ImportSpreadsheetPicker file={file} inputLabel="Customer import file" onSelect={selectFile} onReject={rejectFile}>
          FirstName, LastName, Email, ContactNumber and HomeAddress are required. MembershipId and IsActive are optional. Maximum 5 MB and 1,000 rows.
        </ImportSpreadsheetPicker>
      ) : null}

      {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}

      {preview ? (
        <section className="flex flex-col gap-4" aria-label="Import preview">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h3 className="font-semibold">Preview — first {preview.rows.length} rows</h3>
              <p className="text-sm text-muted-foreground">
                {preview.validRows} of {preview.totalRows} rows passed server validation.
              </p>
            </div>
            {preview.canImport ? (
              <span className="inline-flex items-center gap-2 text-sm font-medium text-success">
                <CheckCircle2 className="size-4" /> Ready to import
              </span>
            ) : (
              <span className="inline-flex items-center gap-2 text-sm font-medium text-error">
                <AlertTriangle className="size-4" /> Fix errors and upload again
              </span>
            )}
          </div>
          <DataTable columns={columns} data={preview.rows} keyExtractor={(row) => row.rowNumber} label="Customer import preview" pageSize={10} />
          {preview.errors.length ? (
            <div className="max-h-44 overflow-auto rounded-lg border border-error/20 bg-error/5 p-3">
              <h4 className="font-semibold text-error">Validation errors</h4>
              <ul className="mt-2 space-y-1 text-sm">
                {preview.errors.map((item, index) => (
                  <li key={`${item.rowNumber}-${item.field}-${index}`}>
                    {item.rowNumber ? `Row ${item.rowNumber}, ` : ""}{item.field}: {item.message}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </section>
      ) : null}

      {isImporting ? (
        <div aria-live="polite" className="rounded-lg bg-accent/10 p-4">
          <p className="font-medium">Importing validated customers…</p>
          <div className="mt-3 h-2 overflow-hidden rounded-full bg-accent/20">
            <div className="h-full w-1/2 animate-pulse rounded-full bg-accent" />
          </div>
        </div>
      ) : null}

      {result ? (
        <StatusMessage tone={result.failedRows ? "error" : "success"}>
          Imported {result.importedRows} of {result.totalRows} customers. {result.failedRows} failed.
          {result.errors.length ? (
            <ul className="mt-2 list-disc pl-5">
              {result.errors.map((item, index) => <li key={`${item.rowNumber}-${index}`}>Row {item.rowNumber}: {item.message}</li>)}
            </ul>
          ) : null}
        </StatusMessage>
      ) : null}

      <DialogActions>
        <Button onClick={close} variant="ghost">
          {result ? "Close" : "Cancel"}
        </Button>
        {!preview && !result ? (
          <Button disabled={!file} isLoading={loading} onClick={() => void requestPreview((upload) =>
            httpClient.post<ImportPreview, typeof upload>("/api/services/app/CustomerImport/Preview", upload))}>
            Preview file
          </Button>
        ) : null}
        {preview?.canImport && !result ? (
          <Button disabled={isImporting} isLoading={isImporting} onClick={confirmImport}>
            Confirm import of {preview.totalRows} customers
          </Button>
        ) : null}
      </DialogActions>
    </div>
  );
};

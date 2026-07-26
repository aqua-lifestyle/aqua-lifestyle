"use client";

import { AlertTriangle, CheckCircle2, FileSpreadsheet, UploadCloud } from "lucide-react";
import { useCallback, useMemo, useState } from "react";
import { useDropzone } from "react-dropzone";

import { useAuthState } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { cn } from "@/src/shared/lib/utils";
import { Button, DataTable, Dialog, StatusMessage } from "@/src/shared/ui";

const IMPORT_PERMISSION = "Aqua.Admin.Customers.Import";
const MAX_FILE_BYTES = 5 * 1024 * 1024;

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

const readAsBase64 = (file: File) =>
  new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error("The selected file could not be read."));
    reader.onload = () => {
      const value = String(reader.result ?? "");
      const separator = value.indexOf(",");
      resolve(separator >= 0 ? value.slice(separator + 1) : value);
    };
    reader.readAsDataURL(file);
  });

export const ImportCustomersDialog = ({ onImported }: ImportCustomersDialogProps) => {
  const { session } = useAuthState();
  const [open, setOpen] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [result, setResult] = useState<ImportResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isPreviewing, setIsPreviewing] = useState(false);
  const [isImporting, setIsImporting] = useState(false);
  const canImport = session?.user?.permissions?.includes(IMPORT_PERMISSION) ?? false;

  const reset = useCallback(() => {
    setFile(null);
    setPreview(null);
    setResult(null);
    setError(null);
    setIsPreviewing(false);
    setIsImporting(false);
  }, []);

  const onDropAccepted = useCallback((files: File[]) => {
    setFile(files[0] ?? null);
    setPreview(null);
    setResult(null);
    setError(null);
  }, []);

  const { getInputProps, getRootProps, isDragActive } = useDropzone({
    accept: {
      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": [".xlsx"],
      "text/csv": [".csv"],
    },
    maxFiles: 1,
    maxSize: MAX_FILE_BYTES,
    multiple: false,
    onDropAccepted,
    onDropRejected: (rejections) => {
      const rejection = rejections[0];
      setFile(null);
      setPreview(null);
      setResult(null);
      setError(rejection?.errors[0]?.message ?? "Choose one CSV or XLSX file no larger than 5 MB.");
    },
  });

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

  const requestPreview = async () => {
    if (!file) return;
    setIsPreviewing(true);
    setError(null);
    setResult(null);
    try {
      const contentBase64 = await readAsBase64(file);
      const response = await httpClient.post<ImportPreview, { contentBase64: string; fileName: string }>(
        "/api/services/app/CustomerImport/Preview",
        { contentBase64, fileName: file.name },
      );
      setPreview(response);
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError, "The file could not be previewed."));
    } finally {
      setIsPreviewing(false);
    }
  };

  const confirmImport = async () => {
    if (!preview?.canImport || !preview.previewId) return;
    setIsImporting(true);
    setError(null);
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

  if (!canImport) return null;

  return (
    <>
      <Button onClick={() => setOpen(true)} variant="outline">
        <UploadCloud className="size-4" />
        Import customers
      </Button>
      <Dialog
        onClose={() => { setOpen(false); reset(); }}
        open={open}
        size="xl"
        title="Import customers"
      >
        <div className="flex flex-col gap-5">
          {!result ? (
            <div
              {...getRootProps()}
              className={cn(
                "cursor-pointer rounded-xl border-2 border-dashed p-7 text-center transition",
                isDragActive ? "border-accent bg-accent/10" : "border-border bg-muted/30 hover:border-accent/60",
              )}
            >
              <input {...getInputProps()} aria-label="Customer import file" />
              <FileSpreadsheet className="mx-auto size-10 text-accent" />
              <p className="mt-3 font-semibold">
                {file ? file.name : isDragActive ? "Drop the file here" : "Drop a CSV or XLSX file here"}
              </p>
              <p className="mt-1 text-sm text-muted-foreground">
                FirstName, LastName, Email, ContactNumber and HomeAddress are required. MembershipId and IsActive are optional. Maximum 5 MB and 1,000 rows.
              </p>
            </div>
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
              <DataTable columns={columns} data={preview.rows} keyExtractor={(row) => row.rowNumber} pageSize={10} />
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

          <div className="flex flex-wrap justify-end gap-3">
            <Button onClick={() => { setOpen(false); reset(); }} variant="ghost">
              {result ? "Close" : "Cancel"}
            </Button>
            {!preview && !result ? (
              <Button disabled={!file} isLoading={isPreviewing} onClick={requestPreview}>
                Preview file
              </Button>
            ) : null}
            {preview?.canImport && !result ? (
              <Button disabled={isImporting} isLoading={isImporting} onClick={confirmImport}>
                Confirm import of {preview.totalRows} customers
              </Button>
            ) : null}
          </div>
        </div>
      </Dialog>
    </>
  );
};

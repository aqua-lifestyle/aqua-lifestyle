"use client";

import { FileSpreadsheet, UploadCloud } from "lucide-react";
import { useEffect, useRef, useState, type ReactNode } from "react";
import { useDropzone } from "react-dropzone";

import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { cn } from "@/src/shared/lib/utils";
import { Button, Dialog } from "@/src/shared/ui";

export const CUSTOMER_IMPORT_PERMISSION = "Aqua.Admin.Customers.Import";

const readSpreadsheetAsBase64 = (file: File) =>
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

export const ImportSpreadsheetDialog = ({ title, children }: { title: string; children: (close: () => void) => ReactNode }) => {
  const [open, setOpen] = useState(false);
  const close = () => setOpen(false);

  return <>
    <Button onClick={() => setOpen(true)} variant="outline"><UploadCloud aria-hidden="true" className="size-4" />{title}</Button>
    <Dialog onClose={close} open={open} size="xl" title={title}>{open ? children(close) : null}</Dialog>
  </>;
};

type SpreadsheetUpload = { contentBase64: string; fileName: string };

// Scoped to one open dialog. A new file or scope invalidates an earlier read/request.
export const useSpreadsheetPreview = <T,>() => {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<T | null>(null);
  const [error, setError] = useState<string>();
  const [loading, setLoading] = useState(false);
  const requestVersion = useRef(0);

  useEffect(() => () => { requestVersion.current++; }, []);

  const clearPreview = () => {
    requestVersion.current++;
    setPreview(null);
    setError(undefined);
    setLoading(false);
  };

  const selectFile = (selected: File | null) => {
    setFile(selected);
    clearPreview();
  };

  const rejectFile = (message: string) => {
    selectFile(null);
    setError(message);
  };

  const requestPreview = async (load: (upload: SpreadsheetUpload) => Promise<T>, fallback = "The file could not be previewed.") => {
    if (!file || loading) return;
    const version = ++requestVersion.current;
    setPreview(null);
    setError(undefined);
    setLoading(true);
    try {
      const contentBase64 = await readSpreadsheetAsBase64(file);
      if (version !== requestVersion.current) return;
      const result = await load({ contentBase64, fileName: file.name });
      if (version === requestVersion.current) setPreview(result);
    } catch (requestError) {
      if (version === requestVersion.current) setError(getRequestErrorMessage(requestError, fallback));
    } finally {
      if (version === requestVersion.current) setLoading(false);
    }
  };

  return { file, preview, error, setError, loading, clearPreview, selectFile, rejectFile, requestPreview };
};

type ImportSpreadsheetPickerProps = {
  file: File | null;
  inputLabel: string;
  onSelect: (file: File | null) => void;
  onReject: (message: string) => void;
  children: ReactNode;
};

export const ImportSpreadsheetPicker = ({ file, inputLabel, onSelect, onReject, children }: ImportSpreadsheetPickerProps) => {
  const { getInputProps, getRootProps, isDragActive } = useDropzone({
    accept: {
      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": [".xlsx"],
      "text/csv": [".csv"],
    },
    maxFiles: 1,
    maxSize: 5 * 1024 * 1024,
    multiple: false,
    onDropAccepted: (files) => onSelect(files[0] ?? null),
    onDropRejected: (rejections) => onReject(rejections[0]?.errors[0]?.message ?? "Choose one CSV or XLSX file no larger than 5 MB."),
  });

  return (
    <div {...getRootProps({
      role: "button",
      "aria-label": file ? `Change ${inputLabel}: ${file.name}` : `Choose ${inputLabel}`,
      className: cn(
        "cursor-pointer rounded-xl border-2 border-dashed p-7 text-center transition focus-visible:outline-2 focus-visible:outline-accent",
        isDragActive ? "border-accent bg-accent/10" : "border-control-border bg-muted/30 hover:border-accent",
      ),
    })}>
      <input {...getInputProps()} aria-label={inputLabel} />
      <FileSpreadsheet aria-hidden="true" className="mx-auto size-10 text-accent" />
      <p className="mt-3 font-semibold">{file?.name ?? (isDragActive ? "Drop the file here" : "Drop a CSV or XLSX file here")}</p>
      <p className="mt-1 text-sm text-muted-foreground">{children}</p>
    </div>
  );
};

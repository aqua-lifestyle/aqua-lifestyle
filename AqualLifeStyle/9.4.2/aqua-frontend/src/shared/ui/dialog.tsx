"use client";

import { X } from "lucide-react";
import { useEffect, useRef } from "react";

import { cn } from "@/src/shared/lib/utils";

type DialogProps = {
  children: React.ReactNode;
  className?: string;
  onClose: () => void;
  open: boolean;
  size?: "md" | "lg" | "xl";
  title: string;
};

const sizeClassNames = {
  lg: "w-[min(96vw,48rem)] max-w-3xl",
  md: "max-w-lg",
  xl: "w-[min(96vw,64rem)] max-w-5xl",
};

export const Dialog = ({
  children,
  className,
  onClose,
  open,
  size = "md",
  title,
}: DialogProps) => {
  const dialogRef = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;

    if (open) {
      dialog.showModal();
    } else {
      dialog.close();
    }
  }, [open]);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;

    const handleClose = () => {
      if (!dialog.open) {
        onClose();
      }
    };

    dialog.addEventListener("close", handleClose);
    return () => dialog.removeEventListener("close", handleClose);
  }, [onClose]);

  return (
    <dialog
      ref={dialogRef}
      className={cn(
        "m-auto rounded-2xl bg-card p-0 text-card-foreground shadow-lg backdrop:bg-primary/20 backdrop:backdrop-blur-sm",
        sizeClassNames[size],
        className,
      )}
      onClick={(event) => {
        if (event.target === dialogRef.current) {
          onClose();
        }
      }}
    >
      <div className="flex flex-col gap-4 p-6">
        <div className="flex items-center justify-between gap-4">
          <h2 className="text-lg font-semibold" id="dialog-title">
            {title}
          </h2>
          <button
            aria-label="Close dialog"
            className="rounded-md p-1 text-muted-foreground transition hover:bg-muted hover:text-foreground"
            onClick={onClose}
            type="button"
          >
            <X className="size-5" />
          </button>
        </div>
        {children}
      </div>
    </dialog>
  );
};

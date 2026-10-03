"use client";

import { X } from "lucide-react";
import { useEffect, useId, useRef } from "react";

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
  lg: "w-[min(calc(100vw-2rem),48rem)]",
  md: "w-[min(calc(100vw-2rem),32rem)]",
  xl: "w-[min(calc(100vw-2rem),64rem)]",
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
  const pointerStartedOnBackdrop = useRef(false);
  const titleId = useId();

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;

    if (open && !dialog.open) {
      dialog.showModal();
    } else if (!open && dialog.open) {
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
      aria-labelledby={titleId}
      ref={dialogRef}
      className={cn(
        "m-auto max-h-[calc(100dvh-2rem)] overflow-y-auto overscroll-contain rounded-2xl bg-card p-0 text-card-foreground shadow-lg backdrop:bg-primary/20 backdrop:backdrop-blur-sm",
        sizeClassNames[size],
        className,
      )}
      onClick={(event) => {
        const startedOutside = pointerStartedOnBackdrop.current;
        pointerStartedOnBackdrop.current = false;
        const bounds = event.currentTarget.getBoundingClientRect();
        const endedOutside = event.clientX < bounds.left || event.clientX > bounds.right ||
          event.clientY < bounds.top || event.clientY > bounds.bottom;
        if (event.target === event.currentTarget && startedOutside && endedOutside) {
          onClose();
        }
      }}
      onPointerDownCapture={(event) => {
        // A drag can start in a field and end on the backdrop, retargeting the
        // click to the dialog. Only an actual backdrop gesture should dismiss it.
        const bounds = event.currentTarget.getBoundingClientRect();
        pointerStartedOnBackdrop.current = event.clientX < bounds.left || event.clientX > bounds.right ||
          event.clientY < bounds.top || event.clientY > bounds.bottom;
      }}
      onPointerCancel={() => { pointerStartedOnBackdrop.current = false; }}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      onKeyDown={(event) => {
        if (event.key !== "Tab") return;
        const controls = Array.from(event.currentTarget.querySelectorAll<HTMLElement>(
          'button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), a[href], [tabindex]:not([tabindex="-1"])',
        )).filter((element) => element.tabIndex >= 0 && element.getClientRects().length > 0);
        const first = controls[0];
        const last = controls[controls.length - 1];
        if (event.shiftKey && document.activeElement === first) {
          event.preventDefault();
          last?.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
          event.preventDefault();
          first?.focus();
        }
      }}
    >
      <div className="flex min-w-0 flex-col gap-4 wrap-anywhere p-4 sm:p-6">
        <div className="flex items-start justify-between gap-4">
          <h2 className="min-w-0 wrap-anywhere pt-2 text-lg font-semibold" id={titleId}>
            {title}
          </h2>
          <button
            aria-label="Close dialog"
            className="inline-flex min-h-11 min-w-11 shrink-0 items-center justify-center rounded-lg text-muted-foreground transition hover:bg-muted hover:text-foreground"
            onClick={onClose}
            type="button"
          >
            <X aria-hidden="true" className="size-5" />
          </button>
        </div>
        {children}
      </div>
    </dialog>
  );
};

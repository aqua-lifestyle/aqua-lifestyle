"use client";

import { ChevronDown } from "lucide-react";
import { type ReactNode, useEffect, useId, useRef, useState } from "react";

import { cn } from "@/src/shared/lib/utils";
import { Button } from "@/src/shared/ui";

type AdminActionDisclosureProps = {
  accessibleLabel: string;
  children: ReactNode;
  className?: string;
  label?: string;
};

export const AdminActionDisclosure = ({
  accessibleLabel,
  children,
  className,
  label = "More actions",
}: AdminActionDisclosureProps) => {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const panelId = useId();

  useEffect(() => {
    if (!open) return;

    const dialogIsOpen = () => Boolean(containerRef.current?.querySelector("dialog[open]"));
    const handlePointerDown = (event: PointerEvent) => {
      if (!containerRef.current?.contains(event.target as Node) && !dialogIsOpen()) {
        setOpen(false);
      }
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key !== "Escape" || dialogIsOpen() || !containerRef.current?.contains(document.activeElement)) return;
      setOpen(false);
      containerRef.current?.querySelector<HTMLButtonElement>("button")?.focus();
    };

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open]);

  return (
    <div className={cn("flex flex-col items-start", className)} ref={containerRef}>
      <Button
        aria-controls={panelId}
        aria-expanded={open}
        aria-label={accessibleLabel}
        onClick={() => setOpen((current) => !current)}
        size="sm"
        variant="outline"
      >
        {label}
        <ChevronDown
          aria-hidden="true"
          className={cn("size-4 transition-transform", open && "rotate-180")}
        />
      </Button>
      {open ? (
        <div
          aria-label={accessibleLabel}
          className="mt-2 flex min-w-48 flex-col gap-2 rounded-lg border border-border bg-muted/30 p-2 [&>button]:w-full [&>button]:justify-start"
          id={panelId}
          role="group"
        >
          {children}
        </div>
      ) : null}
    </div>
  );
};

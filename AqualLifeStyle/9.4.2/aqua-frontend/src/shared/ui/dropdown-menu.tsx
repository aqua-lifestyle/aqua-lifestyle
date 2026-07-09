"use client";

import { ChevronDown } from "lucide-react";
import { useEffect, useRef, useState } from "react";

import { cn } from "@/src/shared/lib/utils";

type DropdownMenuItem = {
  icon?: React.ReactNode;
  id: string;
  label: React.ReactNode;
  onClick?: () => void;
};

type DropdownMenuProps = {
  children: React.ReactNode;
  className?: string;
  items: DropdownMenuItem[];
  label?: string;
};

export const DropdownMenu = ({
  children,
  className,
  items,
  label,
}: DropdownMenuProps) => {
  const [isOpen, setIsOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (
        containerRef.current &&
        !containerRef.current.contains(event.target as Node)
      ) {
        setIsOpen(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  return (
    <div ref={containerRef} className={cn("relative", className)}>
      <button
        aria-expanded={isOpen}
        aria-haspopup="true"
        className="inline-flex items-center gap-2 rounded-lg text-sm font-semibold transition hover:text-accent"
        onClick={() => setIsOpen((current) => !current)}
        type="button"
      >
        {children}
        <ChevronDown
          className={cn("size-4 transition-transform", isOpen && "rotate-180")}
        />
      </button>

      {isOpen ? (
        <div
          className={cn(
            "absolute right-0 z-50 mt-2 min-w-[12rem] overflow-hidden rounded-xl border border-border bg-card p-1 shadow-lg animate-fade-in",
          )}
          role="menu"
        >
          {label ? (
            <div className="px-3 py-2 text-xs font-semibold text-muted-foreground">
              {label}
            </div>
          ) : null}
          {items.map((item) => (
            <button
              key={item.id}
              className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm text-foreground transition hover:bg-muted"
              onClick={() => {
                item.onClick?.();
                setIsOpen(false);
              }}
              role="menuitem"
              type="button"
            >
              {item.icon ? (
                <span className="text-muted-foreground">{item.icon}</span>
              ) : null}
              {item.label}
            </button>
          ))}
        </div>
      ) : null}
    </div>
  );
};

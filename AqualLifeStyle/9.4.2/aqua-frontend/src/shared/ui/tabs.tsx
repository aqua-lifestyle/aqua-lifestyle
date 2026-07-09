"use client";

import type { ReactNode } from "react";

import { cn } from "@/src/shared/lib/utils";

export type TabItem = {
  content: ReactNode;
  id: string;
  label: ReactNode;
};

type TabsProps = {
  className?: string;
  onChange?: (id: string) => void;
  tabs: TabItem[];
  value: string;
};

export const Tabs = ({ className, onChange, tabs, value }: TabsProps) => {
  return (
    <div className={cn("flex flex-col gap-4", className)}>
      <div
        className="flex gap-1 overflow-x-auto border-b border-border"
        role="tablist"
      >
        {tabs.map((tab) => {
          const isActive = tab.id === value;

          return (
            <button
              key={tab.id}
              aria-selected={isActive}
              className={cn(
                "relative shrink-0 rounded-t-lg px-4 py-2.5 text-sm font-semibold transition",
                "focus:outline-none focus-visible:ring-2 focus-visible:ring-accent/50",
                isActive
                  ? "text-foreground"
                  : "text-muted-foreground hover:text-foreground",
              )}
              onClick={() => onChange?.(tab.id)}
              role="tab"
              type="button"
            >
              {tab.label}
              {isActive ? (
                <span className="absolute bottom-0 left-0 right-0 h-0.5 rounded-full bg-accent" />
              ) : null}
            </button>
          );
        })}
      </div>

      {tabs.map((tab) =>
        tab.id === value ? (
          <div key={tab.id} className="animate-fade-in" role="tabpanel">
            {tab.content}
          </div>
        ) : null,
      )}
    </div>
  );
};

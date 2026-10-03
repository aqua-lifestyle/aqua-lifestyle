"use client";

import { type KeyboardEvent, type ReactNode, useId } from "react";

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
  const tabsId = useId();
  const selectTab = (index: number) => {
    const tab = tabs[index];
    if (!tab) return;
    onChange?.(tab.id);
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLButtonElement>, index: number) => {
    let nextIndex: number | undefined;
    if (event.key === "ArrowRight") nextIndex = (index + 1) % tabs.length;
    if (event.key === "ArrowLeft") nextIndex = (index - 1 + tabs.length) % tabs.length;
    if (event.key === "Home") nextIndex = 0;
    if (event.key === "End") nextIndex = tabs.length - 1;
    if (nextIndex === undefined) return;
    event.preventDefault();
    selectTab(nextIndex);
    const tabList = event.currentTarget.parentElement;
    (tabList?.querySelectorAll<HTMLButtonElement>('[role="tab"]')[nextIndex])?.focus();
  };

  return (
    <div className={cn("flex flex-col gap-4", className)}>
      <div
        className="flex gap-1 overflow-x-auto border-b border-border"
        role="tablist"
      >
        {tabs.map((tab, index) => {
          const isActive = tab.id === value;

          return (
            <button
              key={tab.id}
              aria-controls={`${tabsId}-${tab.id}-panel`}
              aria-selected={isActive}
              className={cn(
                "relative min-h-11 shrink-0 rounded-t-lg px-4 py-2.5 text-sm font-semibold transition",
                "focus-visible:outline focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-accent",
                isActive
                  ? "text-foreground"
                  : "text-muted-foreground hover:text-foreground",
              )}
              id={`${tabsId}-${tab.id}-tab`}
              onKeyDown={(event) => handleKeyDown(event, index)}
              onClick={() => onChange?.(tab.id)}
              role="tab"
              tabIndex={isActive ? 0 : -1}
              type="button"
            >
              {tab.label}
              {isActive ? (
                <span aria-hidden="true" className="absolute bottom-0 left-0 right-0 h-0.5 rounded-full bg-accent" />
              ) : null}
            </button>
          );
        })}
      </div>

      {tabs.map((tab) => (
          <div
            aria-labelledby={`${tabsId}-${tab.id}-tab`}
            className="animate-fade-in"
            id={`${tabsId}-${tab.id}-panel`}
            hidden={tab.id !== value}
            key={tab.id}
            role="tabpanel"
            tabIndex={0}
          >
            {tab.id === value ? tab.content : null}
          </div>
      ))}
    </div>
  );
};

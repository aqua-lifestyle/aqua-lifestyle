import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

import { cn } from "@/src/shared/lib/utils";

type EmptyStateProps = {
  action?: ReactNode;
  className?: string;
  description?: string;
  icon: LucideIcon;
  title: string;
};

export const EmptyState = ({
  action,
  className,
  description,
  icon: Icon,
  title,
}: EmptyStateProps) => {
  return (
    <div
      className={cn(
        "flex flex-col items-center justify-center rounded-xl border border-dashed border-border bg-card p-8 text-center",
        className,
      )}
    >
      <div className="rounded-full bg-accent/10 p-3 text-accent">
        <Icon className="size-8" />
      </div>
      <h3 className="mt-4 text-lg font-semibold text-foreground">{title}</h3>
      {description ? (
        <p className="mt-2 max-w-md text-sm text-muted-foreground">{description}</p>
      ) : null}
      {action ? <div className="mt-6">{action}</div> : null}
    </div>
  );
};

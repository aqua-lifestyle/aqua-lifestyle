import type { ReactNode } from "react";

import { cn } from "@/src/shared/lib/utils";

type CardProps = {
  children: ReactNode;
  className?: string;
};

export const Card = ({ children, className }: CardProps) => {
  return (
    <article
      className={cn(
        "rounded-xl border border-border bg-card p-5 shadow-sm transition-all",
        className,
      )}
    >
      {children}
    </article>
  );
};

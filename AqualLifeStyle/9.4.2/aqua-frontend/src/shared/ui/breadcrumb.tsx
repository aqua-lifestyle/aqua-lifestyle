import Link from "next/link";
import { ChevronRight } from "lucide-react";

import { cn } from "@/src/shared/lib/utils";

export type BreadcrumbItem = {
  href?: string;
  label: string;
};

type BreadcrumbProps = {
  className?: string;
  items: BreadcrumbItem[];
};

export const Breadcrumb = ({ className, items }: BreadcrumbProps) => {
  return (
    <nav aria-label="Breadcrumb" className={cn(className)}>
      <ol className="flex flex-wrap items-center gap-2 text-sm">
        {items.map((item, index) => {
          const isLast = index === items.length - 1;

          return (
            <li key={item.label} className="flex items-center gap-2">
              {index > 0 ? (
                <ChevronRight className="size-4 text-muted-foreground" />
              ) : null}
              {isLast || !item.href ? (
                <span
                  className={cn(
                    "font-medium",
                    isLast ? "text-foreground" : "text-muted-foreground",
                  )}
                >
                  {item.label}
                </span>
              ) : (
                <Link
                  className="font-medium text-muted-foreground transition hover:text-accent"
                  href={item.href}
                >
                  {item.label}
                </Link>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
};

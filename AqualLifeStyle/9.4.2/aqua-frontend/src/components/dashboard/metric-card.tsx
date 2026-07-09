import { ArrowDown, ArrowUp, Minus, type LucideIcon } from "lucide-react";

import { cn } from "@/src/shared/lib/utils";
import { Skeleton } from "@/src/shared/ui";

type MetricCardProps = {
  className?: string;
  icon: LucideIcon;
  isLoading?: boolean;
  label: string;
  trend?: number;
  value: number | string;
};

export const MetricCard = ({
  className,
  icon: Icon,
  isLoading,
  label,
  trend,
  value,
}: MetricCardProps) => {
  const trendIsPositive = trend !== undefined && trend > 0;
  const trendIsNegative = trend !== undefined && trend < 0;
  const TrendIcon = trendIsPositive ? ArrowUp : trendIsNegative ? ArrowDown : Minus;

  return (
    <div
      className={cn(
        "relative overflow-hidden rounded-xl border border-border bg-card p-5 shadow-sm transition hover:shadow-md",
        className,
      )}
    >
      <div className="flex items-start justify-between">
        <div className="flex flex-col gap-1">
          <p className="text-sm font-medium text-muted-foreground">{label}</p>
          {isLoading ? (
            <Skeleton className="mt-2 h-8 w-20 rounded-md" />
          ) : (
            <p className="text-3xl font-bold tracking-tight text-foreground">{value}</p>
          )}
        </div>
        <div className="rounded-lg bg-accent/10 p-2.5 text-accent">
          <Icon className="size-5" />
        </div>
      </div>

      {trend !== undefined && !isLoading ? (
        <div className="mt-4 flex items-center gap-1.5 text-sm">
          <span
            className={cn(
              "flex items-center gap-1 rounded-full px-2 py-0.5 font-semibold",
              trendIsPositive
                ? "bg-success/10 text-success"
                : trendIsNegative
                  ? "bg-error/10 text-error"
                  : "bg-muted text-muted-foreground",
            )}
          >
            <TrendIcon className="size-3.5" />
            {trendIsPositive ? `+${trend}%` : `${trend}%`}
          </span>
          <span className="text-muted-foreground">vs last period</span>
        </div>
      ) : null}

      <div className="pointer-events-none absolute -right-6 -top-6 size-24 rounded-full bg-accent/5 blur-2xl" />
    </div>
  );
};

import { AlertCircle, AlertTriangle, CheckCircle2, Info } from "lucide-react";
import type { ReactNode } from "react";

import { cn } from "@/src/shared/lib/utils";

type StatusMessageTone = "error" | "info" | "neutral" | "success" | "warning";

type StatusMessageProps = {
  children: ReactNode;
  className?: string;
  tone?: StatusMessageTone;
  title?: string;
};

const toneConfig: Record<
  StatusMessageTone,
  { icon: typeof Info; className: string; title: string }
> = {
  error: {
    icon: AlertCircle,
    className: "bg-error/10 text-error border-error/20",
    title: "Error",
  },
  info: {
    icon: Info,
    className: "bg-info/10 text-info border-info/20",
    title: "Info",
  },
  neutral: {
    icon: Info,
    className: "bg-muted text-muted-foreground border-border",
    title: "Notice",
  },
  success: {
    icon: CheckCircle2,
    className: "bg-success/10 text-success border-success/20",
    title: "Success",
  },
  warning: {
    icon: AlertTriangle,
    className: "bg-warning/10 text-warning border-warning/20",
    title: "Warning",
  },
};

export const StatusMessage = ({
  children,
  className,
  tone = "neutral",
  title,
}: StatusMessageProps) => {
  const config = toneConfig[tone];
  const Icon = config.icon;

  return (
    <section
      className={cn(
        "rounded-xl border p-4 transition-all",
        config.className,
        className,
      )}
    >
      <div className="flex items-start gap-3">
        <Icon className="mt-0.5 size-5 shrink-0" aria-hidden="true" />
        <div className="min-w-0">
          {title ?? config.title !== "" ? (
            <p className="font-semibold">{title ?? config.title}</p>
          ) : null}
          <div className="text-sm leading-6">{children}</div>
        </div>
      </div>
    </section>
  );
};

import { ShieldAlert } from "lucide-react";

import { cn } from "@/src/shared/lib/utils";
import { Card } from "@/src/shared/ui";

type AccessDeniedProps = {
  className?: string;
  message?: string;
};

export const AccessDenied = ({
  className,
  message = "You do not have permission to view this resource.",
}: AccessDeniedProps) => {
  return (
    <Card
      className={cn(
        "border-error/30 bg-error/10 text-error",
        className,
      )}
    >
      <div className="flex items-start gap-3">
        <ShieldAlert className="mt-0.5 size-5 shrink-0" />
        <div className="min-w-0 flex-1">
          <p className="font-semibold">Access denied</p>
          <p className="text-sm leading-6 text-error/90">{message}</p>
        </div>
      </div>
    </Card>
  );
};

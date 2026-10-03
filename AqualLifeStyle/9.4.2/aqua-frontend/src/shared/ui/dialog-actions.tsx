import type { HTMLAttributes } from "react";

import { cn } from "@/src/shared/lib/utils";

export const DialogActions = ({
  className,
  ...props
}: HTMLAttributes<HTMLDivElement>) => (
  <div
    className={cn(
      "flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:justify-end",
      "[&>*]:w-full sm:[&>*]:w-auto",
      className,
    )}
    {...props}
  />
);

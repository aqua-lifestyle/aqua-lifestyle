import { Loader2 } from "lucide-react";

import { cn } from "@/src/shared/lib/utils";

type SpinnerProps = {
  className?: string;
  size?: "sm" | "md" | "lg";
};

const sizeClassNames: Record<NonNullable<SpinnerProps["size"]>, string> = {
  lg: "size-8",
  md: "size-6",
  sm: "size-4",
};

export const Spinner = ({ className, size = "md" }: SpinnerProps) => {
  return (
    <Loader2
      className={cn("animate-spin text-accent", sizeClassNames[size], className)}
      aria-hidden="true"
    />
  );
};

import { cn } from "@/src/shared/lib/utils";

type SkeletonProps = {
  className?: string;
};

export const Skeleton = ({ className }: SkeletonProps) => {
  return (
    <div
      className={cn("skeleton-shimmer rounded-lg", className)}
      aria-hidden="true"
    />
  );
};

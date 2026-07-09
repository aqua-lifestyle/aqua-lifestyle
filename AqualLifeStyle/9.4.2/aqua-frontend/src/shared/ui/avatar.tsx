import { cn } from "@/src/shared/lib/utils";

type AvatarProps = {
  alt?: string;
  className?: string;
  fallback: string;
  imageUrl?: string | null;
  size?: "sm" | "md" | "lg" | "xl";
};

const sizeClassNames: Record<NonNullable<AvatarProps["size"]>, string> = {
  sm: "size-8 text-[10px]",
  md: "size-10 text-sm",
  lg: "size-14 text-base",
  xl: "size-20 text-lg",
};

const getInitials = (name: string) => {
  const parts = name.trim().split(/\s+/);
  const first = parts[0]?.[0] ?? "";
  const last = parts.length > 1 ? parts[parts.length - 1]?.[0] : "";
  return `${first}${last}`.toUpperCase();
};

const getHue = (name: string) => {
  let hash = 0;
  for (let index = 0; index < name.length; index++) {
    hash = name.charCodeAt(index) + ((hash << 5) - hash);
  }
  return Math.abs(hash % 360);
};

export const Avatar = ({
  alt,
  className,
  fallback,
  imageUrl,
  size = "md",
}: AvatarProps) => {
  const initials = getInitials(fallback);
  const hue = getHue(fallback);

  return (
    <div
      className={cn(
        "inline-flex shrink-0 items-center justify-center overflow-hidden rounded-full font-semibold text-white ring-1 ring-inset ring-white/20",
        sizeClassNames[size],
        className,
      )}
      style={
        imageUrl
          ? undefined
          : {
              background: `linear-gradient(135deg, hsl(${hue} 70% 55%), hsl(${hue} 60% 40%))`,
            }
      }
      aria-label={alt ?? fallback}
      role="img"
    >
      {imageUrl ? (
        <img
          alt={alt ?? fallback}
          className="size-full object-cover"
          src={imageUrl}
        />
      ) : (
        <span aria-hidden="true">{initials}</span>
      )}
    </div>
  );
};

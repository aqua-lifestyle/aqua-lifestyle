import { ShieldAlert } from "lucide-react";

import { LinkButton } from "@/src/shared/ui";

export default function Forbidden() {
  return (
    <div className="flex min-h-dvh flex-col items-center justify-center bg-muted/30 p-4 text-foreground">
      <div className="mx-auto flex w-full max-w-md flex-col items-center gap-4 text-center">
        <div className="flex size-16 items-center justify-center rounded-full bg-warning/10">
          <ShieldAlert className="size-8 text-warning" />
        </div>
        <h1 className="text-2xl font-bold tracking-tight">Access denied</h1>
        <p className="text-muted-foreground">
          You do not have permission to access this resource. Contact your
          administrator if you believe this is a mistake.
        </p>
        <LinkButton href="/" variant="primary">
          Return to Dashboard
        </LinkButton>
      </div>
    </div>
  );
}

import Link from "next/link";
import { Button } from "@/src/shared/ui";

export default function NotFound() {
  return (
    <div className="flex min-h-dvh flex-col items-center justify-center bg-muted/30 p-4 text-foreground">
      <div className="mx-auto flex w-full max-w-md flex-col items-center gap-4 text-center">
        <h1 className="text-6xl font-bold tracking-tight text-muted-foreground">
          404
        </h1>
        <h2 className="text-2xl font-bold tracking-tight">Page not found</h2>
        <p className="text-muted-foreground">
          The page you are looking for does not exist or has been moved.
        </p>
        <Link href="/">
          <Button variant="primary">Return to Dashboard</Button>
        </Link>
      </div>
    </div>
  );
}

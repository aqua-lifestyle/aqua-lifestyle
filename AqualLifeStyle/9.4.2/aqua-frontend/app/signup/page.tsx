import { SignupForm } from "@/src/components/auth/signup-form";
import { publicEnv } from "@/src/shared/config";
import { Card, LinkButton } from "@/src/shared/ui";

export default function SignupPage() {
  if (!publicEnv.NEXT_PUBLIC_SELF_REGISTRATION_ENABLED) {
    return (
      <main className="min-h-dvh bg-muted/30 px-4 py-12 text-foreground sm:px-6">
        <Card className="mx-auto max-w-lg">
          <h1 className="text-2xl font-bold tracking-tight">Account registration</h1>
          <p className="mt-3 text-muted-foreground">
            New Club Member accounts are created by an authorised Aqua Lifestyle Club administrator.
            Contact the club team if you need access or return to sign in if your account already exists.
          </p>
          <div className="mt-6">
            <LinkButton href="/login" variant="primary">Return to sign in</LinkButton>
          </div>
        </Card>
      </main>
    );
  }

  return <SignupForm />;
}

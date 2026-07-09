"use client";

import { Droplets, Eye, EyeOff } from "lucide-react";
import { useState } from "react";
import { z } from "zod";

import { useAuthActions, useTenantState, useToast } from "@/src/providers";
import {
  Button,
  Card,
  LinkButton,
  SelectField,
  TextField,
} from "@/src/shared/ui";

const loginSchema = z.object({
  email: z.string().trim().email("Enter a valid email address."),
  password: z.string().min(1, "Password is required."),
  rememberMe: z.boolean().default(false),
});

type FieldErrors = Partial<Record<"email" | "password", string>>;

export const LoginForm = () => {
  const { setSession } = useAuthActions();
  const { currentTenant, isHost } = useTenantState();
  const { toast } = useToast();
  const [showPassword, setShowPassword] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [isLoading, setIsLoading] = useState(false);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [rememberMe, setRememberMe] = useState(false);

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const result = loginSchema.safeParse({ email, password, rememberMe });
    if (!result.success) {
      const errors: FieldErrors = {};
      for (const issue of result.error.issues) {
        const field = issue.path[0];
        if (field === "email" || field === "password") {
          errors[field] = issue.message;
        }
      }
      setFieldErrors(errors);
      return;
    }

    setFieldErrors({});
    setIsLoading(true);

    // Demo sign-in: set a synthetic session without a real OIDC flow.
    await new Promise((resolve) => setTimeout(resolve, 800));
    setSession({
      accessToken: "demo-access-token",
      expiresAt: new Date(Date.now() + 3600 * 1000).toISOString(),
      user: {
        id: "demo-user",
        email: result.data.email,
        name: result.data.email.split("@")[0],
      },
    });

    toast({
      message: `Signed in as ${result.data.email}`,
      title: "Welcome back",
      type: "success",
    });
    setIsLoading(false);
  };

  return (
    <div className="min-h-dvh bg-muted/30 px-4 py-12 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto grid min-h-[80vh] w-full max-w-6xl overflow-hidden rounded-2xl bg-card shadow-xl lg:grid-cols-2">
        <div className="relative hidden items-center justify-center bg-gradient-to-br from-primary to-primary-dark p-12 lg:flex">
          <div className="absolute inset-0 bg-[url('/aqua-pattern.svg')] bg-cover opacity-10" />
          <div className="relative z-10 text-center text-white">
            <div className="mx-auto flex size-20 items-center justify-center rounded-2xl bg-white/10 backdrop-blur">
              <Droplets className="size-10 text-white" />
            </div>
            <h2 className="mt-6 text-3xl font-bold">Aqua Lifestyle Club</h2>
            <p className="mt-2 text-white/80">
              Enterprise-grade club management, designed for modern teams.
            </p>
          </div>
        </div>

        <div className="flex flex-col justify-center p-8 sm:p-12 lg:p-16">
          <div className="mx-auto w-full max-w-md">
            <div className="mb-8 text-center lg:hidden">
              <div className="mx-auto flex size-12 items-center justify-center rounded-xl bg-gradient-to-br from-accent to-accent-dark text-white">
                <Droplets className="size-6" />
              </div>
              <h1 className="mt-4 text-2xl font-bold">Aqua Lifestyle Club</h1>
            </div>

            <h1 className="text-2xl font-bold tracking-tight">Sign in to your account</h1>
            <p className="mt-2 text-sm text-muted-foreground">
              Enter your credentials to access the dashboard.
            </p>

            <Card className="mt-6">
              <form className="flex flex-col gap-4" onSubmit={handleSubmit}>
                <SelectField
                  label="Tenant"
                  name="tenant"
                  value={isHost ? "" : currentTenant ?? ""}
                  onChange={(e) => {
                    // Tenant selection is handled via the top-level TenantSwitcher.
                    // This select is for display purposes on the login page.
                    e.preventDefault();
                  }}
                >
                  <option value="">Host mode</option>
                  <option value={currentTenant ?? ""}>{currentTenant ?? "Host mode"}</option>
                </SelectField>

                <TextField
                  autoComplete="email"
                  errorMessage={fieldErrors.email}
                  label="Email address"
                  name="email"
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="name@company.com"
                  required
                  type="email"
                  value={email}
                />

                <div className="relative">
                  <TextField
                    autoComplete="current-password"
                    errorMessage={fieldErrors.password}
                    label="Password"
                    name="password"
                    onChange={(e) => setPassword(e.target.value)}
                    placeholder="••••••••"
                    required
                    type={showPassword ? "text" : "password"}
                    value={password}
                  />
                  <button
                    aria-label={showPassword ? "Hide password" : "Show password"}
                    className="absolute right-3 top-8 text-muted-foreground hover:text-foreground"
                    onClick={() => setShowPassword((current) => !current)}
                    type="button"
                  >
                    {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                  </button>
                </div>

                <label className="flex items-center gap-2 text-sm text-muted-foreground">
                  <input
                    checked={rememberMe}
                    className="size-4 rounded border-border text-accent"
                    onChange={(e) => setRememberMe(e.target.checked)}
                    type="checkbox"
                  />
                  Remember me
                </label>

                <Button className="w-full" disabled={isLoading} isLoading={isLoading} type="submit">
                  Sign in
                </Button>
              </form>
            </Card>

            <div className="mt-6">
              <div className="relative">
                <div className="absolute inset-0 flex items-center">
                  <div className="w-full border-t border-border" />
                </div>
                <div className="relative flex justify-center text-xs uppercase">
                  <span className="bg-muted/30 px-2 text-muted-foreground">Or continue with</span>
                </div>
              </div>

              <div className="mt-4 grid grid-cols-2 gap-3">
                <button
                  className="inline-flex items-center justify-center gap-2 rounded-lg border border-border bg-card px-4 py-2 text-sm font-semibold transition hover:bg-muted"
                  type="button"
                >
                  Google
                </button>
                <button
                  className="inline-flex items-center justify-center gap-2 rounded-lg border border-border bg-card px-4 py-2 text-sm font-semibold transition hover:bg-muted"
                  type="button"
                >
                  GitHub
                </button>
              </div>
            </div>

            <p className="mt-6 text-center text-sm text-muted-foreground">
              Don’t have an account?{" "}
              <LinkButton href="/signup" variant="ghost">
                Sign up
              </LinkButton>
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};

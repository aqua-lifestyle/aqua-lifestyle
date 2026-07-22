"use client";

import { Droplets, Eye, EyeOff } from "lucide-react";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { z } from "zod";

import { login } from "@/src/shared/api/auth-service";
import { publicEnv } from "@/src/shared/config";
import { getLoginDestination } from "@/src/shared/auth/roles";
import { useTenantSelfRegistrationAvailability } from "@/src/shared/auth/use-tenant-self-registration-availability";
import { useHydrated } from "@/src/shared/lib/use-hydrated";
import { useAuthActions, useTenantActions, useTenantState, useToast } from "@/src/providers";
import {
  Button,
  Card,
  LinkButton,
  SelectField,
  StatusMessage,
  TextField,
} from "@/src/shared/ui";

const loginSchema = z.object({
  email: z.string().trim().min(1, "Enter your username or email address."),
  password: z.string().min(1, "Password is required."),
  rememberMe: z.boolean().default(false),
});

type FieldErrors = Partial<Record<"email" | "password", string>>;

const ACCOUNT_TYPE_WORKSPACE_NAMES = new Set([
  "admin", "arealeader", "customer", "facilitator", "guest", "member", "systemadmin",
]);

export const getLoginAreaName = (currentArea: string | null, defaultArea: string) => {
  const normalizedCurrentArea = currentArea?.replace(/[\s_-]/g, "").toLowerCase();
  return currentArea && !ACCOUNT_TYPE_WORKSPACE_NAMES.has(normalizedCurrentArea ?? "")
    ? currentArea
    : defaultArea;
};

const getSafeRedirect = () => {
  const redirect = new URLSearchParams(window.location.search).get("redirect");
  return redirect?.startsWith("/") && !redirect.startsWith("//")
    ? redirect
    : null;
};

export const LoginForm = () => {
  const router = useRouter();
  const { setSession } = useAuthActions();
  const { currentTenant } = useTenantState();
  const { clearTenant, setTenant } = useTenantActions();
  const { toast } = useToast();
  const [showPassword, setShowPassword] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [requestError, setRequestError] = useState<string>();
  const [isLoading, setIsLoading] = useState(false);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [rememberMe, setRememberMe] = useState(false);
  const [selectedWorkspace, setSelectedWorkspace] = useState(
    getLoginAreaName(currentTenant, publicEnv.NEXT_PUBLIC_DEFAULT_TENANT_NAME),
  );
  const hasMounted = useHydrated();
  const currentAreaName = getLoginAreaName(
    currentTenant,
    publicEnv.NEXT_PUBLIC_DEFAULT_TENANT_NAME,
  );
  const selfRegistrationAvailability =
    useTenantSelfRegistrationAvailability(selectedWorkspace);

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
    setRequestError(undefined);
    setIsLoading(true);

    // Real authentication via the ABP TokenAuth endpoint.
    const authResult = await login({
      email: result.data.email,
      password: result.data.password,
      rememberMe: result.data.rememberMe,
      tenant: selectedWorkspace || null,
    });

    if (!authResult.ok) {
      setRequestError(authResult.message);
      toast({
        message: authResult.message,
        title: "Sign in failed",
        type: "error",
      });
      setIsLoading(false);
      return;
    }

    setSession(authResult.session);
    if (selectedWorkspace) setTenant(selectedWorkspace); else clearTenant();

    toast({
      message: `Signed in as ${result.data.email}`,
      title: "Welcome back",
      type: "success",
    });
    setIsLoading(false);
    const role = authResult.session.user?.role;
    const destination = getLoginDestination(role, getSafeRedirect());
    router.push(destination);
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
              Manage memberships and the aQua network from one secure place.
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
                  label="Workspace"
                  name="tenant"
                  value={hasMounted ? selectedWorkspace : publicEnv.NEXT_PUBLIC_DEFAULT_TENANT_NAME}
                  onChange={(event) => setSelectedWorkspace(event.target.value)}
                >
                  <option value={publicEnv.NEXT_PUBLIC_DEFAULT_TENANT_NAME}>Area workspace</option>
                  {hasMounted && currentAreaName !== publicEnv.NEXT_PUBLIC_DEFAULT_TENANT_NAME ? <option value={currentAreaName}>{currentAreaName}</option> : null}
                  <option value="">Platform administration</option>
                </SelectField>
                <p className="-mt-2 text-xs text-muted-foreground">Choose Platform administration only when signing in with the platform administrator account.</p>

                <TextField
                  autoComplete="username"
                  errorMessage={fieldErrors.email}
                  label="Username or email"
                  name="email"
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="name@company.com"
                  required
                  type="text"
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
                {requestError ? <StatusMessage tone="error">{requestError}</StatusMessage> : null}
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

            {selfRegistrationAvailability === "enabled" ? (
              <p className="mt-6 text-center text-sm text-muted-foreground">
                Don’t have an account?{" "}
                <LinkButton
                  href={`/signup?area=${encodeURIComponent(selectedWorkspace)}`}
                  variant="ghost"
                >
                  Sign up
                </LinkButton>
              </p>
            ) : null}
          </div>
        </div>
      </div>
    </div>
  );
};

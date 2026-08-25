"use client";

import { Droplets, Eye, EyeOff } from "lucide-react";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { z } from "zod";

import { register } from "@/src/shared/api/auth-service";
import { publicEnv } from "@/src/shared/config";
import { securePasswordSchema } from "@/src/shared/auth/password-policy";
import { useTenantState, useToast } from "@/src/providers";
import { Button, Card, LinkButton, TextAreaField, TextField } from "@/src/shared/ui";
import { customerContactNumberSchema, customerFirstNameSchema, customerHomeAddressSchema, customerSurnameSchema } from "@/src/shared/validation/customer-personal-details";

const step1Schema = z
  .object({
    confirmPassword: z.string().min(1, "Confirm your password."),
    email: z.string().trim().email("Enter a valid email address."),
    password: securePasswordSchema,
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: "Passwords do not match.",
    path: ["confirmPassword"],
  });

const step2Schema = z.object({
  contactNumber: customerContactNumberSchema,
  firstName: customerFirstNameSchema,
  homeAddress: customerHomeAddressSchema,
  surname: customerSurnameSchema,
});

const steps = [
  { description: "Account details", title: "Create account" },
  { description: "Your profile", title: "Personal info" },
  { description: "Get started", title: "Review & terms" },
];

type SignupFormProps = {
  inviteCode?: string;
  redirectPath?: string;
  tenancyName?: string;
};

export const SignupForm = ({ inviteCode, redirectPath, tenancyName }: SignupFormProps) => {
  const router = useRouter();
  const { currentTenant } = useTenantState();
  const { toast } = useToast();
  const [step, setStep] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [acceptedTerms, setAcceptedTerms] = useState(false);
  const [formData, setFormData] = useState({
    confirmPassword: "",
    contactNumber: "",
    email: "",
    firstName: "",
    homeAddress: "",
    password: "",
    surname: "",
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const loginParams = new URLSearchParams();
  if (tenancyName) loginParams.set("area", tenancyName);
  if (inviteCode) loginParams.set("invite", inviteCode);
  if (redirectPath?.startsWith("/") && !redirectPath.startsWith("//")) {
    loginParams.set("redirect", redirectPath);
  }
  const loginHref = loginParams.size > 0
    ? `/login?${loginParams.toString()}`
    : "/login";

  const updateField = (field: keyof typeof formData, value: string) => {
    setFormData((current) => ({ ...current, [field]: value }));
    setErrors((current) => {
      const next = { ...current };
      delete next[field];
      return next;
    });
  };

  const validateCurrentStep = () => {
    let result;

    if (step === 0) {
      result = step1Schema.safeParse(formData);
    } else if (step === 1) {
      result = step2Schema.safeParse({
        contactNumber: formData.contactNumber,
        firstName: formData.firstName,
        homeAddress: formData.homeAddress,
        surname: formData.surname,
      });
    } else {
      return true;
    }

    if (!result.success) {
      const nextErrors: Record<string, string> = {};
      for (const issue of result.error.issues) {
        const field = issue.path[0];
        if (typeof field === "string" && !nextErrors[field]) {
          nextErrors[field] = issue.message;
        }
      }
      setErrors(nextErrors);
      return false;
    }

    setErrors({});
    return true;
  };

  const handleNext = () => {
    if (validateCurrentStep()) {
      setStep((current) => Math.min(current + 1, steps.length - 1));
    }
  };

  const handleBack = () => {
    setStep((current) => Math.max(current - 1, 0));
  };

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!acceptedTerms) {
      setErrors({ terms: "You must accept the terms and conditions." });
      return;
    }

    if (!validateCurrentStep()) return;

    setIsLoading(true);

    const resolvedTenant = tenancyName ??
      currentTenant ??
      publicEnv.NEXT_PUBLIC_DEFAULT_TENANT_NAME;

    const registerResult = await register({
      contactNumber: formData.contactNumber,
      email: formData.email,
      homeAddress: formData.homeAddress,
      inviteCode,
      password: formData.password,
      redirectPath,
      name: formData.firstName,
      surname: formData.surname,
      tenant: resolvedTenant,
    });

    if (!registerResult.ok) {
      const nextErrors: Record<string, string> = {};

      if (registerResult.fieldErrors) {
        for (const [field, message] of Object.entries(registerResult.fieldErrors)) {
          const key = field === "emailAddress" ? "email" : field === "userName" ? "email" : field;
          nextErrors[key] = message;
        }
      }

      setErrors(nextErrors);

      toast({
        message: registerResult.fieldErrors
          ? Object.values(registerResult.fieldErrors).join(", ")
          : registerResult.message,
        title: "Registration failed",
        type: "error",
      });
      setIsLoading(false);
      return;
    }

    toast({
      message: "Account created. Check your email to verify your address.",
      title: "Verify your email",
      type: "success",
    });
    setIsLoading(false);
    const verification = new URLSearchParams({ area: resolvedTenant });
    if (redirectPath) verification.set("redirect", redirectPath);
    router.push(`/verify-email-sent?${verification.toString()}`);
  };

  const getPasswordStrength = () => {
    const { password } = formData;
    let score = 0;
    if (password.length >= 8) score += 1;
    if (/[A-Z]/.test(password)) score += 1;
    if (/[a-z]/.test(password)) score += 1;
    if (/[0-9]/.test(password)) score += 1;
    if (/[^A-Za-z0-9]/.test(password)) score += 1;
    return score;
  };

  const strength = getPasswordStrength();
  const strengthLabel = ["Weak", "Fair", "Good", "Strong", "Very strong"][
    Math.min(strength, 4)
  ];
  const strengthColor = [
    "bg-error",
    "bg-warning",
    "bg-accent",
    "bg-success",
    "bg-success",
  ][Math.min(strength, 4)];

  return (
    <div className="min-h-dvh bg-muted/30 px-4 py-12 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto grid min-h-[80vh] w-full max-w-6xl overflow-hidden rounded-2xl bg-card shadow-xl lg:grid-cols-2">
        <div className="relative hidden items-center justify-center bg-gradient-to-br from-primary to-primary-dark p-12 lg:flex">
          <div className="relative z-10 text-center text-white">
            <div className="mx-auto flex size-20 items-center justify-center rounded-2xl bg-white/10 backdrop-blur">
              <Droplets className="size-10 text-white" />
            </div>
            <h2 className="mt-6 text-3xl font-bold">Aqua Lifestyle Club</h2>
            <p className="mt-2 text-white/80">
              Join the platform that powers modern club management.
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

            <h1 className="text-2xl font-bold tracking-tight">Create your customer account</h1>
            <p className="mt-2 text-sm text-muted-foreground">
              Join as a customer. Administrator access is issued securely by an existing administrator.
            </p>

            <div className="mt-6 flex items-center justify-between gap-2">
              {steps.map((s, index) => (
                <div key={s.title} className="flex flex-1 flex-col items-center gap-2">
                  <div
                    className={`
                      flex size-8 items-center justify-center rounded-full text-sm font-bold transition
                      ${index <= step ? "bg-accent text-white" : "bg-muted text-muted-foreground"}
                    `}
                  >
                    {index + 1}
                  </div>
                  <span className="hidden text-xs font-medium text-muted-foreground sm:inline">
                    {s.title}
                  </span>
                </div>
              ))}
            </div>
            <div className="mt-2 h-2 w-full rounded-full bg-muted">
              <div
                className="h-2 rounded-full bg-accent transition-all"
                style={{ width: `${((step + 1) / steps.length) * 100}%` }}
              />
            </div>

            <Card className="mt-6">
              <form className="flex flex-col gap-4" onSubmit={handleSubmit}>
                {step === 0 ? (
                  <>
                    <TextField
                      autoComplete="email"
                      errorMessage={errors.email}
                      label="Email address"
                      name="email"
                      onChange={(e) => updateField("email", e.target.value)}
                      placeholder="name@company.com"
                      required
                      type="email"
                      value={formData.email}
                    />
                    <div className="relative">
                      <TextField
                        autoComplete="new-password"
                        errorMessage={errors.password}
                        label="Password"
                        name="password"
                        onChange={(e) => updateField("password", e.target.value)}
                        placeholder="••••••••"
                        required
                        type={showPassword ? "text" : "password"}
                        value={formData.password}
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
                    <div className="h-1.5 w-full overflow-hidden rounded-full bg-muted">
                      <div
                        className={`h-full transition-all ${strengthColor}`}
                        style={{ width: `${(strength / 5) * 100}%` }}
                      />
                    </div>
                    <p className="text-xs text-muted-foreground">
                      Password strength: {strengthLabel}
                    </p>
                    <TextField
                      autoComplete="new-password"
                      errorMessage={errors.confirmPassword}
                      label="Confirm password"
                      name="confirmPassword"
                      onChange={(e) => updateField("confirmPassword", e.target.value)}
                      placeholder="••••••••"
                      required
                      type="password"
                      value={formData.confirmPassword}
                    />
                  </>
                ) : null}

                {step === 1 ? (
                  <>
                    <TextField
                      autoComplete="given-name"
                      errorMessage={errors.firstName}
                      label="First name"
                      name="firstName"
                      onChange={(e) => updateField("firstName", e.target.value)}
                      required
                      value={formData.firstName}
                    />
                    <TextField
                      autoComplete="family-name"
                      errorMessage={errors.surname}
                      label="Surname"
                      name="surname"
                      onChange={(e) => updateField("surname", e.target.value)}
                      required
                      value={formData.surname}
                    />
                    <TextField
                      autoComplete="tel"
                      errorMessage={errors.contactNumber}
                      label="Contact number"
                      name="contactNumber"
                      onChange={(e) => updateField("contactNumber", e.target.value)}
                      required
                      type="tel"
                      value={formData.contactNumber}
                    />
                    <TextAreaField
                      autoComplete="street-address"
                      errorMessage={errors.homeAddress}
                      label="Home address"
                      name="homeAddress"
                      onChange={(e) => updateField("homeAddress", e.target.value)}
                      required
                      rows={3}
                      value={formData.homeAddress}
                    />
                    {currentTenant ? (
                      <p className="text-sm text-muted-foreground">
                        Tenant context: <span className="font-semibold text-foreground">{currentTenant}</span>
                      </p>
                    ) : null}
                  </>
                ) : null}

                {step === 2 ? (
                  <div className="flex flex-col gap-4">
                    <div className="rounded-lg bg-muted p-4 text-sm">
                      <p className="font-semibold text-foreground">Review your details</p>
                      <dl className="mt-2 grid gap-1">
                        <div className="flex justify-between">
                          <dt className="text-muted-foreground">Email</dt>
                          <dd className="text-foreground">{formData.email}</dd>
                        </div>
                        <div className="flex justify-between">
                          <dt className="text-muted-foreground">Name</dt>
                          <dd className="text-foreground">{formData.firstName} {formData.surname}</dd>
                        </div>
                        <div className="flex justify-between gap-4">
                          <dt className="text-muted-foreground">Contact number</dt>
                          <dd className="text-right text-foreground">{formData.contactNumber}</dd>
                        </div>
                        <div className="flex justify-between gap-4">
                          <dt className="text-muted-foreground">Home address</dt>
                          <dd className="text-right text-foreground">{formData.homeAddress}</dd>
                        </div>
                      </dl>
                    </div>

                    <label className="flex items-start gap-3 rounded-lg border border-border p-3">
                      <input
                        checked={acceptedTerms}
                        className="mt-1 size-4 rounded border-border text-accent"
                        onChange={(e) => {
                          setAcceptedTerms(e.target.checked);
                          setErrors((current) => {
                            const next = { ...current };
                            delete next.terms;
                            return next;
                          });
                        }}
                        type="checkbox"
                      />
                      <span className="text-sm text-muted-foreground">
                        I agree to the{" "}
                        <a className="text-accent hover:underline" href="#">
                          Terms of Service
                        </a>{" "}
                        and{" "}
                        <a className="text-accent hover:underline" href="#">
                          Privacy Policy
                        </a>
                        .
                      </span>
                    </label>
                    {errors.terms ? (
                      <p className="text-sm text-error">{errors.terms}</p>
                    ) : null}
                  </div>
                ) : null}

                <div className="flex gap-2">
                  {step > 0 ? (
                    <Button
                      className="flex-1"
                      onClick={handleBack}
                      type="button"
                      variant="outline"
                    >
                      Back
                    </Button>
                  ) : null}
                  {step < steps.length - 1 ? (
                    <Button
                      className="flex-1"
                      onClick={handleNext}
                      type="button"
                      variant="primary"
                    >
                      Next
                    </Button>
                  ) : (
                    <Button
                      className="flex-1"
                      disabled={isLoading}
                      isLoading={isLoading}
                      type="submit"
                      variant="primary"
                    >
                      Create account
                    </Button>
                  )}
                </div>
              </form>
            </Card>

            <p className="mt-6 text-center text-sm text-muted-foreground">
              Already have an account?{" "}
              <LinkButton href={loginHref} variant="ghost">
                Sign in
              </LinkButton>
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};

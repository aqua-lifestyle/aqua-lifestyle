"use client";

import { type FormEvent, useEffect, useState } from "react";
import { z } from "zod";

import {
  useCustomersActions,
  useCustomersState,
  useMembershipsActions,
  useMembershipsState,
  useToast,
} from "@/src/providers";
import {
  Breadcrumb,
  Button,
  Card,
  LinkButton,
  SelectField,
  StatusMessage,
  TextField,
} from "@/src/shared/ui";

const customerRegistrationSchema = z.object({
  email: z.string().trim().email("Enter a valid email address."),
  membershipId: z
    .union([z.literal(""), z.coerce.number().int().positive()])
    .transform((value) => (value === "" ? null : value)),
  name: z
    .string()
    .trim()
    .min(2, "Customer name must be at least 2 characters.")
    .max(120, "Customer name must be 120 characters or fewer."),
});

type CustomerRegistrationFormValues = z.infer<
  typeof customerRegistrationSchema
>;

type FieldErrors = Partial<Record<keyof CustomerRegistrationFormValues, string>>;

const getFieldErrors = (
  error: z.ZodError<CustomerRegistrationFormValues>,
): FieldErrors => {
  return error.issues.reduce<FieldErrors>((errors, issue) => {
    const field = issue.path[0];

    if (field === "email" || field === "name" || field === "membershipId") {
      errors[field] = issue.message;
    }

    return errors;
  }, {});
};

type CustomerRegistrationFormProps = {
  initialMembershipId?: number | null;
};

export const CustomerRegistrationForm = ({
  initialMembershipId = null,
}: CustomerRegistrationFormProps) => {
  const { createCustomer } = useCustomersActions();
  const { getMemberships } = useMembershipsActions();
  const {
    createErrorMessage,
    isCreateError,
    isCreatePending,
    isCreateSuccess,
  } = useCustomersState();
  const {
    errorMessage: membershipsErrorMessage,
    isError: isMembershipsError,
    isPending: isMembershipsPending,
    memberships,
  } = useMembershipsState();
  const { toast } = useToast();
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  useEffect(() => {
    void getMemberships();
  }, [getMemberships]);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const form = event.currentTarget;
    const formData = new FormData(form);
    const result = customerRegistrationSchema.safeParse({
      email: formData.get("email"),
      membershipId: formData.get("membershipId"),
      name: formData.get("name"),
    });

    if (!result.success) {
      setFieldErrors(getFieldErrors(result.error));
      return;
    }

    setFieldErrors({});

    const wasCreated = await createCustomer({
      email: result.data.email,
      membershipId: result.data.membershipId,
      name: result.data.name,
    });

    if (wasCreated) {
      form.reset();
      toast({
        message: "Customer registered successfully.",
        title: "Success",
        type: "success",
      });
    }
  };

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-3xl flex-col gap-6">
        <header>
          <Breadcrumb
            items={[
              { href: "/", label: "Dashboard" },
              { href: "/customers", label: "Customers" },
              { label: "Register" },
            ]}
          />
          <h1 className="mt-2 text-3xl font-bold tracking-tight">
            Customer registration
          </h1>
          <p className="mt-2 text-base text-muted-foreground">
            Create a customer record in the ABP backend with optional membership
            assignment.
          </p>
        </header>

        <Card>
          <form noValidate onSubmit={handleSubmit} className="flex flex-col gap-5">
            <TextField
              autoComplete="name"
              errorMessage={fieldErrors.name}
              label="Full name"
              name="name"
              placeholder="Thandaza Mkhize"
              required
            />
            <TextField
              autoComplete="email"
              errorMessage={fieldErrors.email}
              label="Email address"
              name="email"
              placeholder="customer@example.com"
              required
              type="email"
            />
            <SelectField
              defaultValue={initialMembershipId ?? ""}
              disabled={isMembershipsPending || isCreatePending}
              errorMessage={fieldErrors.membershipId}
              label="Membership"
              name="membershipId"
            >
              <option value="">No membership assigned</option>
              {memberships.map((membership) => (
                <option key={membership.id} value={membership.id}>
                  {membership.name}
                </option>
              ))}
            </SelectField>

            {isMembershipsError ? (
              <StatusMessage tone="error">
                {membershipsErrorMessage ??
                  "Unable to load memberships for assignment."}
              </StatusMessage>
            ) : null}

            {isCreateError ? (
              <StatusMessage tone="error">
                {createErrorMessage ?? "Unable to register the customer."}
              </StatusMessage>
            ) : null}

            {isCreateSuccess ? (
              <StatusMessage tone="success">
                Customer registered successfully.{" "}
                <LinkButton
                  className="h-auto px-0 py-0 font-semibold underline"
                  href="/customers"
                  variant="ghost"
                >
                  View customers
                </LinkButton>
              </StatusMessage>
            ) : null}

            <div className="flex justify-end gap-2">
              <Button disabled={isCreatePending} isLoading={isCreatePending} type="submit">
                {isCreatePending ? "Registering..." : "Register customer"}
              </Button>
            </div>
          </form>
        </Card>
      </div>
    </main>
  );
};

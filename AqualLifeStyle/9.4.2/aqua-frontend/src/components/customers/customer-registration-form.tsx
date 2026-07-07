"use client";

import Link from "next/link";
import { type FormEvent, useState } from "react";
import { z } from "zod";

import { useCustomersActions, useCustomersState } from "@/src/providers";
import { Button, StatusMessage, TextField } from "@/src/shared/ui";

const customerRegistrationSchema = z.object({
  email: z.string().trim().email("Enter a valid email address."),
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

    if (field === "email" || field === "name") {
      errors[field] = issue.message;
    }

    return errors;
  }, {});
};

export const CustomerRegistrationForm = () => {
  const { createCustomer } = useCustomersActions();
  const {
    createErrorMessage,
    isCreateError,
    isCreatePending,
    isCreateSuccess,
  } = useCustomersState();
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const form = event.currentTarget;
    const formData = new FormData(form);
    const result = customerRegistrationSchema.safeParse({
      email: formData.get("email"),
      name: formData.get("name"),
    });

    if (!result.success) {
      setFieldErrors(getFieldErrors(result.error));
      return;
    }

    setFieldErrors({});

    const wasCreated = await createCustomer({
      email: result.data.email,
      membershipId: null,
      name: result.data.name,
    });

    if (wasCreated) {
      form.reset();
    }
  };

  return (
    <main className="min-h-dvh bg-zinc-50 px-6 py-8 text-zinc-950 sm:px-8 lg:px-12">
      <div className="mx-auto flex w-full max-w-3xl flex-col gap-8">
        <header className="flex flex-col gap-4">
          <Link
            className="text-sm font-medium text-emerald-700 hover:text-emerald-800"
            href="/products"
          >
            Back to products
          </Link>
          <div className="flex flex-col gap-2">
            <p className="text-sm font-medium uppercase tracking-wide text-emerald-700">
              Aqua Lifestyle Club
            </p>
            <h1 className="text-3xl font-semibold tracking-tight">
              Customer registration
            </h1>
            <p className="max-w-2xl text-base text-zinc-600">
              Create a customer record in the ABP backend. Membership assignment
              is intentionally deferred to the next validated slice.
            </p>
          </div>
        </header>

        <form
          onSubmit={handleSubmit}
          className="rounded-lg border border-zinc-200 bg-white p-6 shadow-sm"
          noValidate
        >
          <div className="flex flex-col gap-5">
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

            {isCreateError ? (
              <StatusMessage tone="error">
                {createErrorMessage ?? "Unable to register the customer."}
              </StatusMessage>
            ) : null}

            {isCreateSuccess ? (
              <StatusMessage tone="success">
                Customer registered successfully.{" "}
                <Link className="font-semibold underline" href="/customers">
                  View customers
                </Link>
              </StatusMessage>
            ) : null}

            <div className="flex justify-end">
              <Button disabled={isCreatePending} type="submit">
                {isCreatePending ? "Registering..." : "Register customer"}
              </Button>
            </div>
          </div>
        </form>
      </div>
    </main>
  );
};

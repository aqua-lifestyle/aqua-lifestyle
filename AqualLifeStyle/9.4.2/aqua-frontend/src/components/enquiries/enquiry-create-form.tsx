"use client";

import Link from "next/link";
import { type FormEvent, useEffect, useState } from "react";
import { z } from "zod";

import {
  useCustomersActions,
  useCustomersState,
  useEnquiriesActions,
  useEnquiriesState,
  useProductsActions,
  useProductsState,
} from "@/src/providers";
import {
  Button,
  SelectField,
  StatusMessage,
  TextAreaField,
} from "@/src/shared/ui";

const enquiryCreateSchema = z.object({
  customerId: z.coerce.number().int().positive("Select a customer."),
  message: z
    .string()
    .trim()
    .min(10, "Message must be at least 10 characters.")
    .max(1000, "Message must be 1000 characters or fewer."),
  productId: z.coerce.number().int().positive("Select a product."),
});

type EnquiryCreateFormValues = z.infer<typeof enquiryCreateSchema>;

type FieldErrors = Partial<Record<keyof EnquiryCreateFormValues, string>>;

const getFieldErrors = (
  error: z.ZodError<EnquiryCreateFormValues>,
): FieldErrors => {
  return error.issues.reduce<FieldErrors>((errors, issue) => {
    const field = issue.path[0];

    if (field === "customerId" || field === "message" || field === "productId") {
      errors[field] = issue.message;
    }

    return errors;
  }, {});
};

type EnquiryCreateFormProps = {
  initialCustomerId?: number | null;
  initialProductId?: number | null;
};

export const EnquiryCreateForm = ({
  initialCustomerId = null,
  initialProductId = null,
}: EnquiryCreateFormProps) => {
  const { getCustomers } = useCustomersActions();
  const { createEnquiry } = useEnquiriesActions();
  const { getProducts } = useProductsActions();
  const { customers, isLoadPending: isCustomersPending } = useCustomersState();
  const {
    createErrorMessage,
    isCreateError,
    isCreatePending,
    isCreateSuccess,
  } = useEnquiriesState();
  const { isPending: isProductsPending, products } = useProductsState();
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  useEffect(() => {
    void getCustomers();
    void getProducts();
  }, [getCustomers, getProducts]);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const form = event.currentTarget;
    const formData = new FormData(form);
    const result = enquiryCreateSchema.safeParse({
      customerId: formData.get("customerId"),
      message: formData.get("message"),
      productId: formData.get("productId"),
    });

    if (!result.success) {
      setFieldErrors(getFieldErrors(result.error));
      return;
    }

    setFieldErrors({});

    const wasCreated = await createEnquiry({
      customerId: result.data.customerId,
      message: result.data.message,
      productId: result.data.productId,
    });

    if (wasCreated) {
      form.reset();
    }
  };

  const isReferenceDataPending = isCustomersPending || isProductsPending;

  return (
    <main className="min-h-dvh bg-zinc-50 px-6 py-8 text-zinc-950 sm:px-8 lg:px-12">
      <div className="mx-auto flex w-full max-w-3xl flex-col gap-8">
        <header className="flex flex-col gap-4">
          <Link
            className="text-sm font-medium text-emerald-700 hover:text-emerald-800"
            href="/enquiries"
          >
            Back to enquiries
          </Link>
          <div className="flex flex-col gap-2">
            <p className="text-sm font-medium uppercase tracking-wide text-emerald-700">
              Aqua Lifestyle Club
            </p>
            <h1 className="text-3xl font-semibold tracking-tight">
              Create enquiry
            </h1>
            <p className="max-w-2xl text-base text-zinc-600">
              Capture a customer enquiry against an existing product. Contextual
              links can preselect the customer or product for a faster demo path.
            </p>
          </div>
        </header>

        <form
          className="rounded-lg border border-zinc-200 bg-white p-6 shadow-sm"
          noValidate
          onSubmit={handleSubmit}
        >
          <div className="flex flex-col gap-5">
            <SelectField
              disabled={isReferenceDataPending || isCreatePending}
              errorMessage={fieldErrors.customerId}
              label="Customer"
              name="customerId"
              required
              defaultValue={initialCustomerId ?? ""}
            >
              <option value="">Select customer</option>
              {customers.map((customer) => (
                <option key={customer.id} value={customer.id}>
                  {customer.name}
                </option>
              ))}
            </SelectField>

            <SelectField
              disabled={isReferenceDataPending || isCreatePending}
              errorMessage={fieldErrors.productId}
              label="Product"
              name="productId"
              required
              defaultValue={initialProductId ?? ""}
            >
              <option value="">Select product</option>
              {products.map((product) => (
                <option key={product.id} value={product.id}>
                  {product.name}
                </option>
              ))}
            </SelectField>

            <TextAreaField
              errorMessage={fieldErrors.message}
              label="Message"
              name="message"
              placeholder="Tell us what the customer wants to know..."
              required
              rows={6}
            />

            {isCreateError ? (
              <StatusMessage tone="error">
                {createErrorMessage ?? "Unable to create the enquiry."}
              </StatusMessage>
            ) : null}

            {isCreateSuccess ? (
              <StatusMessage tone="success">
                Enquiry created successfully.{" "}
                <Link className="font-semibold underline" href="/enquiries">
                  View enquiries
                </Link>
              </StatusMessage>
            ) : null}

            <div className="flex justify-end">
              <Button
                disabled={isReferenceDataPending || isCreatePending}
                type="submit"
              >
                {isCreatePending ? "Creating..." : "Create enquiry"}
              </Button>
            </div>
          </div>
        </form>
      </div>
    </main>
  );
};

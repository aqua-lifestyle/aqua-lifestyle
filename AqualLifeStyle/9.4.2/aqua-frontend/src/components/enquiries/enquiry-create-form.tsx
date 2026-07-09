"use client";

import { type FormEvent, useEffect, useState } from "react";
import { z } from "zod";

import {
  useCustomersActions,
  useCustomersState,
  useEnquiriesActions,
  useEnquiriesState,
  useProductsActions,
  useProductsState,
  useToast,
} from "@/src/providers";
import {
  Breadcrumb,
  Button,
  Card,
  LinkButton,
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
  const { toast } = useToast();
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
      toast({
        message: "Enquiry created successfully.",
        title: "Success",
        type: "success",
      });
    }
  };

  const isReferenceDataPending = isCustomersPending || isProductsPending;

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-3xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <Breadcrumb
              items={[
                { href: "/", label: "Dashboard" },
                { href: "/enquiries", label: "Enquiries" },
                { label: "Create" },
              ]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">Create enquiry</h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              Capture a customer enquiry against a product and route it to the team.
            </p>
          </div>
          <LinkButton href="/enquiries" variant="outline">
            Back to enquiries
          </LinkButton>
        </header>

        <Card>
          <form noValidate onSubmit={handleSubmit} className="flex flex-col gap-5">
            <SelectField
              defaultValue={initialCustomerId ?? ""}
              disabled={isReferenceDataPending || isCreatePending}
              errorMessage={fieldErrors.customerId}
              label="Customer"
              name="customerId"
              required
            >
              <option value="">Select customer</option>
              {customers.map((customer) => (
                <option key={customer.id} value={customer.id}>
                  {customer.name}
                </option>
              ))}
            </SelectField>

            <SelectField
              defaultValue={initialProductId ?? ""}
              disabled={isReferenceDataPending || isCreatePending}
              errorMessage={fieldErrors.productId}
              label="Product"
              name="productId"
              required
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
                <LinkButton
                  className="h-auto px-0 py-0 font-semibold underline"
                  href="/enquiries"
                  variant="ghost"
                >
                  View enquiries
                </LinkButton>
              </StatusMessage>
            ) : null}

            <div className="flex justify-end">
              <Button disabled={isReferenceDataPending || isCreatePending} isLoading={isCreatePending} type="submit">
                {isCreatePending ? "Creating..." : "Create enquiry"}
              </Button>
            </div>
          </form>
        </Card>
      </div>
    </main>
  );
};

"use client";

import { FormEvent, useEffect, useState } from "react";

import {
  type Customer,
  useCustomersActions,
  useCustomersState,
  useMembershipsActions,
  useMembershipsState,
  useProductsActions,
  useProductsState,
} from "@/src/providers";
import { getMembershipNameById } from "@/src/shared/domain";
import {
  Badge,
  Button,
  Card,
  LinkButton,
  SelectField,
  StatusMessage,
  TextField,
} from "@/src/shared/ui";

type CustomerFormState = {
  email: string;
  isActive: boolean;
  membershipId: string;
  name: string;
};

type CustomerDetailsProps = {
  customerId: number;
};

type CustomerEditFormProps = {
  customer: Customer;
  isUpdatePending: boolean;
  memberships: { id: number; name: string }[];
  updateCustomer: (input: Customer) => Promise<boolean>;
};

const toFormState = (customer: Customer): CustomerFormState => ({
  email: customer.email,
  isActive: customer.isActive,
  membershipId: customer.membershipId?.toString() ?? "",
  name: customer.name,
});

const CustomerEditForm = ({
  customer,
  isUpdatePending,
  memberships,
  updateCustomer,
}: CustomerEditFormProps) => {
  const [formState, setFormState] = useState<CustomerFormState>(
    toFormState(customer),
  );

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    await updateCustomer({
      id: customer.id,
      email: formState.email.trim(),
      isActive: formState.isActive,
      membershipId:
        formState.membershipId.length > 0 ? Number(formState.membershipId) : null,
      name: formState.name.trim(),
    });
  };

  return (
    <form className="flex flex-col gap-5" onSubmit={handleSubmit}>
      <TextField
        label="Name"
        name="name"
        onChange={(event) =>
          setFormState((current) => ({
            ...current,
            name: event.target.value,
          }))
        }
        required
        value={formState.name}
      />

      <TextField
        label="Email"
        name="email"
        onChange={(event) =>
          setFormState((current) => ({
            ...current,
            email: event.target.value,
          }))
        }
        required
        type="email"
        value={formState.email}
      />

      <SelectField
        label="Membership"
        name="membershipId"
        onChange={(event) =>
          setFormState((current) => ({
            ...current,
            membershipId: event.target.value,
          }))
        }
        value={formState.membershipId}
      >
        <option value="">No membership assigned</option>
        {memberships.map((membership) => (
          <option key={membership.id} value={membership.id}>
            {membership.name}
          </option>
        ))}
      </SelectField>

      <label className="flex items-start gap-3 rounded-lg border border-zinc-200 bg-zinc-50 p-4">
        <input
          checked={formState.isActive}
          className="mt-1 size-4 rounded border-zinc-300 text-emerald-700 focus:ring-emerald-700"
          onChange={(event) =>
            setFormState((current) => ({
              ...current,
              isActive: event.target.checked,
            }))
          }
          type="checkbox"
        />
        <span>
          <span className="block text-sm font-medium text-zinc-900">
            Active customer
          </span>
          <span className="mt-1 block text-sm leading-6 text-zinc-600">
            Inactive customers remain visible but are marked as inactive.
          </span>
        </span>
      </label>

      <Button disabled={isUpdatePending} type="submit">
        Save customer
      </Button>
    </form>
  );
};

export const CustomerDetails = ({ customerId }: CustomerDetailsProps) => {
  const { getCustomer, updateCustomer } = useCustomersActions();
  const { getMemberships } = useMembershipsActions();
  const { getEligibleProductsForCustomer } = useProductsActions();
  const {
    isSelectedError,
    isSelectedPending,
    isUpdateError,
    isUpdatePending,
    isUpdateSuccess,
    selectedCustomer,
    selectedErrorMessage,
    updateErrorMessage,
  } = useCustomersState();
  const { memberships } = useMembershipsState();
  const {
    eligibleErrorMessage,
    eligibleProducts,
    isEligibleError,
    isEligiblePending,
  } = useProductsState();

  useEffect(() => {
    if (!Number.isInteger(customerId) || customerId <= 0) {
      return;
    }

    void getCustomer(customerId);
    void getEligibleProductsForCustomer(customerId);
    void getMemberships();
  }, [
    customerId,
    getCustomer,
    getEligibleProductsForCustomer,
    getMemberships,
  ]);

  return (
    <main className="min-h-dvh bg-zinc-50 px-6 py-8 text-zinc-950 sm:px-8 lg:px-12">
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-8">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex flex-col gap-2">
            <p className="text-sm font-medium uppercase tracking-wide text-emerald-700">
              Aqua Lifestyle Club
            </p>
            <h1 className="text-3xl font-semibold tracking-tight">
              Customer details
            </h1>
            <p className="max-w-2xl text-base text-zinc-600">
              Validate customer updates against the ABP backend without leaving
              the demo flow.
            </p>
          </div>
          <LinkButton href="/customers">Back to customers</LinkButton>
        </header>

        {!Number.isInteger(customerId) || customerId <= 0 ? (
          <StatusMessage tone="error">This customer id is invalid.</StatusMessage>
        ) : null}

        {isSelectedPending ? (
          <StatusMessage>Loading customer...</StatusMessage>
        ) : null}

        {isSelectedError ? (
          <StatusMessage tone="error">
            {selectedErrorMessage ?? "Unable to load this customer."}
          </StatusMessage>
        ) : null}

        {selectedCustomer ? (
          <section className="grid gap-6 lg:grid-cols-[1fr_22rem]">
            <Card>
              <CustomerEditForm
                customer={selectedCustomer}
                isUpdatePending={isUpdatePending}
                key={[
                  selectedCustomer.id,
                  selectedCustomer.name,
                  selectedCustomer.email,
                  selectedCustomer.membershipId ?? "none",
                  selectedCustomer.isActive,
                ].join(":")}
                memberships={memberships}
                updateCustomer={updateCustomer}
              />
            </Card>

            <aside className="flex flex-col gap-6">
              <Card>
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <h2 className="text-lg font-semibold">
                      {selectedCustomer.name}
                    </h2>
                    <p className="mt-1 break-words text-sm text-zinc-600">
                      {selectedCustomer.email}
                    </p>
                  </div>
                  <Badge tone={selectedCustomer.isActive ? "success" : "neutral"}>
                    {selectedCustomer.isActive ? "Active" : "Inactive"}
                  </Badge>
                </div>

                <dl className="mt-6 grid gap-3 text-sm">
                  <div className="flex justify-between gap-4">
                    <dt className="text-zinc-600">Membership</dt>
                    <dd className="text-right font-medium text-zinc-950">
                      {getMembershipNameById(
                        memberships,
                        selectedCustomer.membershipId,
                        "No membership assigned",
                      )}
                    </dd>
                  </div>
                  <div className="flex justify-between gap-4">
                    <dt className="text-zinc-600">Customer ID</dt>
                    <dd className="font-medium text-zinc-950">
                      {selectedCustomer.id}
                    </dd>
                  </div>
                </dl>
              </Card>

              {isUpdateSuccess ? (
                <StatusMessage tone="success">Customer updated.</StatusMessage>
              ) : null}

              {isUpdateError ? (
                <StatusMessage tone="error">
                  {updateErrorMessage ?? "Unable to update this customer."}
                </StatusMessage>
              ) : null}
            </aside>

            <section className="lg:col-span-2">
              <Card>
                <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                  <div>
                    <h2 className="text-lg font-semibold">
                      Eligible products
                    </h2>
                    <p className="mt-2 max-w-2xl text-sm leading-6 text-zinc-600">
                      Products returned by the backend for this customer&apos;s
                      active status and membership access.
                    </p>
                  </div>
                  <LinkButton href="/products">Full catalog</LinkButton>
                </div>

                {isEligiblePending ? (
                  <StatusMessage>Loading eligible products...</StatusMessage>
                ) : null}

                {isEligibleError ? (
                  <StatusMessage tone="error">
                    {eligibleErrorMessage ??
                      "Unable to load eligible products for this customer."}
                  </StatusMessage>
                ) : null}

                {!isEligiblePending &&
                !isEligibleError &&
                eligibleProducts.length === 0 ? (
                  <StatusMessage>
                    No eligible products are available for this customer yet.
                  </StatusMessage>
                ) : null}

                {eligibleProducts.length > 0 ? (
                  <div className="mt-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                    {eligibleProducts.map((product) => (
                      <div
                        className="rounded-lg border border-zinc-200 bg-zinc-50 p-4"
                        key={product.id}
                      >
                        <div className="flex items-start justify-between gap-3">
                          <div>
                            <h3 className="font-medium text-zinc-950">
                              {product.name}
                            </h3>
                            <p className="mt-1 text-sm text-zinc-600">
                              {getMembershipNameById(
                                memberships,
                                product.membershipId,
                                "Open access",
                              )}
                            </p>
                          </div>
                          <Badge tone={product.isActive ? "success" : "neutral"}>
                            {product.isActive ? "Active" : "Inactive"}
                          </Badge>
                        </div>
                        <div className="mt-5">
                          <LinkButton href={`/products/${product.id}`}>
                            Open product
                          </LinkButton>
                        </div>
                      </div>
                    ))}
                  </div>
                ) : null}
              </Card>
            </section>
          </section>
        ) : null}
      </div>
    </main>
  );
};

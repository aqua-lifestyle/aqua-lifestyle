"use client";

import { FormEvent, useEffect, useState } from "react";

import {
  type Customer,
  type Membership,
  useCustomersActions,
  useCustomersState,
  useEnquiriesActions,
  useEnquiriesState,
  useMembershipsActions,
  useMembershipsState,
  useOrderIntentsActions,
  useOrderIntentsState,
  useProductsActions,
  useProductsState,
} from "@/src/providers";
import { getMembershipNameById } from "@/src/shared/domain";
import {
  Avatar,
  Badge,
  Breadcrumb,
  Button,
  Card,
  LinkButton,
  SelectField,
  Skeleton,
  StatusMessage,
  Tabs,
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

const toFormState = (customer: Customer): CustomerFormState => ({
  email: customer.email,
  isActive: customer.isActive,
  membershipId: customer.membershipId?.toString() ?? "",
  name: customer.name,
});

const formatCurrency = (amount: number) =>
  new Intl.NumberFormat("en-ZA", {
    currency: "ZAR",
    style: "currency",
  }).format(amount);

const CustomerEditForm = ({
  customer,
  isUpdatePending,
  memberships,
  updateCustomer,
}: {
  customer: Customer;
  isUpdatePending: boolean;
  memberships: Membership[];
  updateCustomer: (input: Customer) => Promise<boolean>;
}) => {
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
          setFormState((current) => ({ ...current, name: event.target.value }))
        }
        required
        value={formState.name}
      />
      <TextField
        label="Email"
        name="email"
        onChange={(event) =>
          setFormState((current) => ({ ...current, email: event.target.value }))
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

      <label className="flex items-start gap-3 rounded-lg border border-border bg-muted p-4">
        <input
          checked={formState.isActive}
          className="mt-1 size-4 rounded border-border text-accent focus:ring-accent"
          onChange={(event) =>
            setFormState((current) => ({
              ...current,
              isActive: event.target.checked,
            }))
          }
          type="checkbox"
        />
        <span>
          <span className="block text-sm font-medium text-foreground">
            Active customer
          </span>
          <span className="mt-1 block text-sm text-muted-foreground">
            Inactive customers remain visible but are marked as inactive.
          </span>
        </span>
      </label>

      <Button disabled={isUpdatePending} isLoading={isUpdatePending} type="submit">
        Save customer
      </Button>
    </form>
  );
};

const CustomerOverview = ({
  customer,
  isUpdatePending,
  isUpdateSuccess,
  isUpdateError,
  memberships,
  updateCustomer,
  updateErrorMessage,
}: {
  customer: Customer;
  isUpdateError: boolean;
  isUpdatePending: boolean;
  isUpdateSuccess: boolean;
  memberships: Membership[];
  updateCustomer: (input: Customer) => Promise<boolean>;
  updateErrorMessage: string | null;
}) => {
  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_22rem]">
      <Card>
        <h2 className="text-lg font-semibold">Edit customer</h2>
        <div className="mt-4">
          <CustomerEditForm
            customer={customer}
            isUpdatePending={isUpdatePending}
            key={[
              customer.id,
              customer.name,
              customer.email,
              customer.membershipId ?? "none",
              customer.isActive,
            ].join(":")}
            memberships={memberships}
            updateCustomer={updateCustomer}
          />
        </div>
      </Card>

      <aside className="flex flex-col gap-6">
        <Card>
          <div className="flex items-start justify-between gap-4">
            <div className="flex items-center gap-3">
              <Avatar fallback={customer.name} size="lg" />
              <div>
                <h2 className="text-lg font-semibold">{customer.name}</h2>
                <p className="text-sm text-muted-foreground">{customer.email}</p>
              </div>
            </div>
            <Badge tone={customer.isActive ? "success" : "neutral"}>
              {customer.isActive ? "Active" : "Inactive"}
            </Badge>
          </div>

          <dl className="mt-6 grid gap-3 text-sm">
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">Membership</dt>
              <dd className="text-right font-medium">
                {getMembershipNameById(
                  memberships,
                  customer.membershipId,
                  "No membership assigned",
                )}
              </dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">Customer ID</dt>
              <dd className="font-medium">{customer.id}</dd>
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
    </div>
  );
};

const CustomerEligibleProducts = ({
  customer,
}: {
  customer: Customer;
}) => {
  const { getEligibleProductsForCustomer } = useProductsActions();
  const { getMemberships } = useMembershipsActions();
  const { memberships } = useMembershipsState();
  const {
    eligibleErrorMessage,
    eligibleProducts,
    isEligibleError,
    isEligiblePending,
  } = useProductsState();

  useEffect(() => {
    void getEligibleProductsForCustomer(customer.id);
    void getMemberships();
  }, [customer.id, getEligibleProductsForCustomer, getMemberships]);

  return (
    <Card>
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h2 className="text-lg font-semibold">Eligible products</h2>
          <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
            Products returned by the backend for this customer&apos;s active status
            and membership access.
          </p>
        </div>
        <LinkButton href="/products">Full catalog</LinkButton>
      </div>

      {isEligiblePending ? <Skeleton className="mt-6 h-40" /> : null}
      {isEligibleError ? (
        <StatusMessage className="mt-6" tone="error">
          {eligibleErrorMessage ?? "Unable to load eligible products."}
        </StatusMessage>
      ) : null}
      {!isEligiblePending && !isEligibleError && eligibleProducts.length === 0 ? (
        <StatusMessage className="mt-6">
          No eligible products are available for this customer yet.
        </StatusMessage>
      ) : null}

      {eligibleProducts.length > 0 ? (
        <div className="mt-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {eligibleProducts.map((product) => (
            <div
              className="rounded-xl border border-border bg-muted p-4 transition hover:border-accent/50"
              key={product.id}
            >
              <div className="flex items-start justify-between gap-3">
                <div>
                  <h3 className="font-semibold text-foreground">{product.name}</h3>
                  <p className="text-sm text-muted-foreground">
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
              <div className="mt-4 flex items-center gap-2">
                <LinkButton href={`/products/${product.id}`} size="sm" variant="outline">
                  Open product
                </LinkButton>
                <LinkButton
                  href={`/enquiries/create?customerId=${customer.id}&productId=${product.id}`}
                  size="sm"
                  variant="ghost"
                >
                  Enquire
                </LinkButton>
              </div>
            </div>
          ))}
        </div>
      ) : null}
    </Card>
  );
};

const CustomerActivity = ({
  customerId,
}: {
  customerId: number;
}) => {
  const { getEnquiries } = useEnquiriesActions();
  const { getOrderIntents } = useOrderIntentsActions();
  const { enquiries, isLoadPending: isEnquiriesPending } = useEnquiriesState();
  const { orderIntents, isLoadPending: isOrderIntentsPending } = useOrderIntentsState();

  useEffect(() => {
    void getEnquiries();
    void getOrderIntents();
  }, [getEnquiries, getOrderIntents]);

  const customerEnquiries = enquiries.filter((e) => e.customerId === customerId);
  const customerIntents = orderIntents.filter((o) => o.customerId === customerId);

  const isLoading = isEnquiriesPending || isOrderIntentsPending;

  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <Card>
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold">Enquiries</h2>
          <LinkButton
            href={`/enquiries/create?customerId=${customerId}`}
            size="sm"
            variant="primary"
          >
            Create enquiry
          </LinkButton>
        </div>
        {isLoading ? (
          <Skeleton className="mt-4 h-40" />
        ) : customerEnquiries.length === 0 ? (
          <p className="mt-4 text-sm text-muted-foreground">No enquiries for this customer.</p>
        ) : (
          <ul className="mt-4 space-y-3">
            {customerEnquiries.map((enquiry) => (
              <li
                key={enquiry.id}
                className="flex items-start justify-between rounded-xl border border-border p-4 transition hover:bg-muted"
              >
                <div className="min-w-0">
                  <p className="font-semibold">Enquiry #{enquiry.id}</p>
                  <p className="truncate text-sm text-muted-foreground">{enquiry.message}</p>
                </div>
                <LinkButton href={`/enquiries/${enquiry.id}`} size="sm" variant="outline">
                  Open
                </LinkButton>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card>
        <h2 className="text-lg font-semibold">Order intents</h2>
        {isLoading ? (
          <Skeleton className="mt-4 h-40" />
        ) : customerIntents.length === 0 ? (
          <p className="mt-4 text-sm text-muted-foreground">No order intents for this customer.</p>
        ) : (
          <ul className="mt-4 space-y-3">
            {customerIntents.map((intent) => (
              <li
                key={intent.id}
                className="flex items-start justify-between rounded-xl border border-border p-4 transition hover:bg-muted"
              >
                <div className="min-w-0">
                  <p className="font-semibold">Intent #{intent.id}</p>
                  <p className="text-sm text-muted-foreground">
                    {formatCurrency(intent.reservedPrice)} ·{" "}
                    {intent.status === 1 ? "Reserved" : intent.status === 3 ? "Completed" : "Draft"}
                  </p>
                </div>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
};

export const CustomerDetails = ({ customerId }: CustomerDetailsProps) => {
  const { getCustomer, updateCustomer } = useCustomersActions();
  const { getMemberships } = useMembershipsActions();
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
  const [activeTab, setActiveTab] = useState("overview");

  useEffect(() => {
    if (!Number.isInteger(customerId) || customerId <= 0) {
      return;
    }

    void getCustomer(customerId);
    void getMemberships();
  }, [customerId, getCustomer, getMemberships]);

  const isInvalid = !Number.isInteger(customerId) || customerId <= 0;

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <Breadcrumb
              items={[
                { href: "/", label: "Dashboard" },
                { href: "/customers", label: "Customers" },
                { label: "Customer details" },
              ]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">Customer details</h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              Review customer information, update membership, and manage activity.
            </p>
          </div>
          <LinkButton href="/customers" variant="outline">
            Back to customers
          </LinkButton>
        </header>

        {isInvalid ? (
          <StatusMessage tone="error">This customer id is invalid.</StatusMessage>
        ) : null}
        {isSelectedPending ? (
          <Skeleton className="h-96" />
        ) : null}
        {isSelectedError ? (
          <StatusMessage tone="error">
            {selectedErrorMessage ?? "Unable to load this customer."}
          </StatusMessage>
        ) : null}

        {selectedCustomer ? (
          <Tabs
            onChange={setActiveTab}
            tabs={[
              {
                content: (
                  <CustomerOverview
                    customer={selectedCustomer}
                    isUpdateError={isUpdateError}
                    isUpdatePending={isUpdatePending}
                    isUpdateSuccess={isUpdateSuccess}
                    memberships={memberships}
                    updateCustomer={updateCustomer}
                    updateErrorMessage={updateErrorMessage}
                  />
                ),
                id: "overview",
                label: "Overview",
              },
              {
                content: <CustomerEligibleProducts customer={selectedCustomer} />,
                id: "products",
                label: "Eligible products",
              },
              {
                content: <CustomerActivity customerId={selectedCustomer.id} />,
                id: "activity",
                label: "Enquiries & intents",
              },
            ]}
            value={activeTab}
          />
        ) : null}
      </div>
    </main>
  );
};

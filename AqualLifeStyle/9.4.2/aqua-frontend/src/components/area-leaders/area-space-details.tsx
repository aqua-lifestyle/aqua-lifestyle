"use client";

import { FormEvent, useEffect, useState } from "react";

import {
  type AreaSpace,
  useAreaSpacesActions,
  useAreaSpacesState,
  useAuthState,
} from "@/src/providers";
import {
  Avatar,
  Badge,
  Breadcrumb,
  Button,
  Card,
  LinkButton,
  Skeleton,
  StatusMessage,
  Tabs,
  TextField,
} from "@/src/shared/ui";

type AreaSpaceFormState = {
  addressLine: string;
  capacity: string;
  interestedMembers: string;
};

type AreaSpaceDetailsProps = {
  areaSpaceId: number;
};

const toFormState = (areaSpace: AreaSpace): AreaSpaceFormState => ({
  addressLine: areaSpace.addressLine,
  capacity: areaSpace.capacity,
  interestedMembers: areaSpace.interestedMembers.toString(),
});

const statusLabel = (value: number) => {
  const labels = ["Applied", "Under Review", "Approved", "Suspended"];
  return labels[value] ?? `Status ${value}`;
};

const statusTone = (value: number): "neutral" | "info" | "success" | "error" => {
  if (value === 2) return "success";
  if (value === 3) return "error";
  if (value === 1) return "info";
  return "neutral";
};

const AreaSpaceEditForm = ({
  areaSpace,
  isUpdatePending,
  onUpdate,
}: {
  areaSpace: AreaSpace;
  isUpdatePending: boolean;
  onUpdate: (id: number, addressLine: string, capacity: string, interestedMembers: number) => Promise<boolean>;
}) => {
  const [formState, setFormState] = useState<AreaSpaceFormState>(() =>
    toFormState(areaSpace),
  );

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    await onUpdate(
      areaSpace.id,
      formState.addressLine,
      formState.capacity,
      Number(formState.interestedMembers),
    );
  };

  return (
    <form className="flex flex-col gap-5" onSubmit={handleSubmit}>
      <TextField
        label="Address"
        name="addressLine"
        onChange={(event) =>
          setFormState((current) => ({ ...current, addressLine: event.target.value }))
        }
        required
        value={formState.addressLine}
      />
      <TextField
        label="Capacity"
        name="capacity"
        onChange={(event) =>
          setFormState((current) => ({ ...current, capacity: event.target.value }))
        }
        required
        value={formState.capacity}
      />
      <TextField
        label="Interested club members"
        name="interestedMembers"
        onChange={(event) =>
          setFormState((current) => ({
            ...current,
            interestedMembers: event.target.value,
          }))
        }
        required
        type="number"
        value={formState.interestedMembers}
      />

      <Button disabled={isUpdatePending} isLoading={isUpdatePending} type="submit">
        Save changes
      </Button>
    </form>
  );
};

const AreaSpaceOverview = ({
  areaSpace,
  isApprovePending,
  approveAreaSpace,
  startReview,
  recordPresentation,
  recordStartupOrder,
  suspendAreaSpace,
}: {
  areaSpace: AreaSpace;
  isApprovePending: boolean;
  approveAreaSpace: (id: number) => Promise<boolean>;
  startReview: (id: number) => Promise<boolean>;
  recordPresentation: (id: number) => Promise<boolean>;
  recordStartupOrder: (id: number) => Promise<boolean>;
  suspendAreaSpace: (id: number) => Promise<boolean>;
}) => {
  const [addressLine, setAddressLine] = useState(areaSpace.addressLine);
  const [capacity, setCapacity] = useState(areaSpace.capacity);
  const [interestedMembers, setInterestedMembers] = useState(
    areaSpace.interestedMembers.toString(),
  );

  const handleUpdate = async (
    id: number,
    newAddressLine: string,
    newCapacity: string,
    newInterestedMembers: number,
  ) => {
    setAddressLine(newAddressLine);
    setCapacity(newCapacity);
    setInterestedMembers(newInterestedMembers.toString());
    return true;
  };

  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_22rem]">
      <Card>
        <h2 className="text-lg font-semibold">Edit area space</h2>
        <div className="mt-4">
          <AreaSpaceEditForm
            areaSpace={{ ...areaSpace, addressLine, capacity, interestedMembers: Number(interestedMembers) }}
            isUpdatePending={isApprovePending}
            onUpdate={handleUpdate}
          />
        </div>
      </Card>

      <aside className="flex flex-col gap-6">
        <Card>
          <div className="flex items-start justify-between gap-4">
            <div className="flex items-center gap-3">
              <Avatar fallback={`AS ${areaSpace.id}`} size="lg" />
              <div>
                <h2 className="text-lg font-semibold">Area Space #{areaSpace.id}</h2>
                <p className="text-sm text-muted-foreground">{areaSpace.addressLine}</p>
              </div>
            </div>
            <Badge tone={statusTone(areaSpace.status)}>
              {statusLabel(areaSpace.status)}
            </Badge>
          </div>

          <dl className="mt-6 grid gap-3 text-sm">
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">Capacity</dt>
              <dd className="font-medium">{areaSpace.capacity}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">Interested club members</dt>
              <dd className="font-medium">{areaSpace.interestedMembers}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">Presentations</dt>
              <dd className="font-medium">
                {areaSpace.presentationsCompleted} / 4
              </dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">Startup Orders</dt>
              <dd className="font-medium">
                {areaSpace.startupOrdersCompleted} / 20
              </dd>
            </div>
            {areaSpace.reviewStartedAt ? (
              <div className="flex justify-between gap-4">
                <dt className="text-muted-foreground">Review Started</dt>
                <dd className="font-medium">
                  {new Date(areaSpace.reviewStartedAt).toLocaleString()}
                </dd>
              </div>
            ) : null}
            {areaSpace.approvedAt ? (
              <div className="flex justify-between gap-4">
                <dt className="text-muted-foreground">Approved At</dt>
                <dd className="font-medium">
                  {new Date(areaSpace.approvedAt).toLocaleString()}
                </dd>
              </div>
            ) : null}
          </dl>
        </Card>

        <Card>
          <h3 className="text-lg font-semibold">Actions</h3>
          <div className="mt-4 flex flex-col gap-2">
            {areaSpace.status === 0 ? (
              <Button
                disabled={isApprovePending}
                onClick={() => startReview(areaSpace.id)}
                type="button"
                variant="primary"
              >
                Start Review
              </Button>
            ) : null}
            {areaSpace.status === 1 ? (
              <>
                <Button
                  disabled={isApprovePending}
                  onClick={() => recordPresentation(areaSpace.id)}
                  type="button"
                  variant="secondary"
                >
                  Record Presentation
                </Button>
                <Button
                  disabled={isApprovePending}
                  onClick={() => recordStartupOrder(areaSpace.id)}
                  type="button"
                  variant="secondary"
                >
                  Record Startup Order
                </Button>
                <Button
                  disabled={isApprovePending}
                  onClick={() => approveAreaSpace(areaSpace.id)}
                  type="button"
                  variant="primary"
                >
                  Approve Area Space
                </Button>
              </>
            ) : null}
            {areaSpace.status === 2 ? (
              <Button
                disabled={isApprovePending}
                onClick={() => suspendAreaSpace(areaSpace.id)}
                type="button"
                variant="danger"
              >
                Suspend Area Space
              </Button>
            ) : null}
          </div>
        </Card>
      </aside>
    </div>
  );
};

export const AreaSpaceDetails = ({ areaSpaceId }: AreaSpaceDetailsProps) => {
  const { getAreaSpace, approveAreaSpace, startReview, recordPresentation, recordStartupOrder, suspendAreaSpace } =
    useAreaSpacesActions();
  const {
    isApproveError,
    isApprovePending,
    isApproveSuccess,
    isSelectedError,
    isSelectedPending,
    selectedAreaSpace,
    selectedErrorMessage,
    approveErrorMessage,
  } = useAreaSpacesState();
  const { session } = useAuthState();
  const hasPermission = session?.user?.permissions?.includes("Pages.AreaSpaces") ?? false;
  const [activeTab, setActiveTab] = useState("overview");

  // ALL hooks before early returns
  useEffect(() => {
    if (!Number.isInteger(areaSpaceId) || areaSpaceId <= 0) {
      return;
    }

    void getAreaSpace(areaSpaceId);
  }, [areaSpaceId, getAreaSpace]);

  if (!hasPermission) {
    return (
      <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
        <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
          <StatusMessage tone="error">
            You do not have permission to view this area space.
          </StatusMessage>
        </div>
      </main>
    );
  }

  const isInvalid = !Number.isInteger(areaSpaceId) || areaSpaceId <= 0;

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <Breadcrumb
              items={[
                { href: "/", label: "Dashboard" },
                { href: "/area-leader/area-spaces", label: "Area Spaces" },
                { label: "Area Space details" },
              ]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">Area Space details</h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              Review area space information, manage review progress, and approve spaces.
            </p>
          </div>
          <LinkButton href="/area-leader/area-spaces" variant="outline">
            Back to area spaces
          </LinkButton>
        </header>

        {isInvalid ? (
          <StatusMessage tone="error">This area space id is invalid.</StatusMessage>
        ) : null}
        {isSelectedPending ? (
          <Skeleton className="h-96" />
        ) : null}
        {isSelectedError ? (
          <StatusMessage tone="error">
            {selectedErrorMessage ?? "Unable to load this area space."}
          </StatusMessage>
        ) : null}

        {selectedAreaSpace ? (
          <Tabs
            onChange={setActiveTab}
            tabs={[
              {
                content: (
                  <AreaSpaceOverview
                    areaSpace={selectedAreaSpace}
                    isApprovePending={isApprovePending}
                    approveAreaSpace={approveAreaSpace}
                    startReview={startReview}
                    recordPresentation={recordPresentation}
                    recordStartupOrder={recordStartupOrder}
                    suspendAreaSpace={suspendAreaSpace}
                  />
                ),
                id: "overview",
                label: "Overview",
              },
            ]}
            value={activeTab}
          />
        ) : null}

        {isApproveSuccess ? (
          <StatusMessage tone="success">Area space updated.</StatusMessage>
        ) : null}
        {isApproveError ? (
          <StatusMessage tone="error">
            {approveErrorMessage ?? "Unable to update this area space."}
          </StatusMessage>
        ) : null}
      </div>
    </main>
  );
};

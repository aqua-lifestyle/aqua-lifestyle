"use client";

import { FormEvent, useEffect, useState } from "react";

import {
  type Facilitator,
  useFacilitatorsActions,
  useFacilitatorsState,
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

type FacilitatorFormState = {
  areaLeaderId: string;
};

type FacilitatorDetailsProps = {
  facilitatorId: number;
};

const rankLabel = (value: number) => {
  const ranks = [
    "Bronze",
    "Gold",
    "Pearl",
    "Sapphire",
    "Ruby",
    "Platinum",
    "Premier T60",
  ];
  return ranks[value] ?? `Rank ${value}`;
};

const rankTone = (value: number): "neutral" | "success" | "info" | "warning" => {
  if (value >= 5) return "success";
  if (value >= 3) return "info";
  if (value >= 1) return "warning";
  return "neutral";
};

const FacilitatorEditForm = ({
  facilitator,
  isUpdatePending,
  onUpdate,
}: {
  facilitator: Facilitator;
  isUpdatePending: boolean;
  onUpdate: (id: number, areaLeaderId: number) => Promise<boolean>;
}) => {
  const [formState, setFormState] = useState<FacilitatorFormState>({
    areaLeaderId: facilitator.areaLeaderId.toString(),
  });

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    await onUpdate(facilitator.id, Number(formState.areaLeaderId));
  };

  return (
    <form className="flex flex-col gap-5" onSubmit={handleSubmit}>
      <TextField
        label="Customer ID"
        name="customerId"
        value={String(facilitator.customerId)}
        disabled
      />
      <TextField
        label="Area Leader ID"
        name="areaLeaderId"
        onChange={(event) =>
          setFormState((current) => ({
            ...current,
            areaLeaderId: event.target.value,
          }))
        }
        required
        type="number"
        value={formState.areaLeaderId}
      />

      <Button disabled={isUpdatePending} isLoading={isUpdatePending} type="submit">
        Save changes
      </Button>
    </form>
  );
};

const FacilitatorOverview = ({
  facilitator,
}: {
  facilitator: Facilitator;
}) => {
  const [areaLeaderId, setAreaLeaderId] = useState(facilitator.areaLeaderId.toString());

  const handleUpdate = async (id: number, newAreaLeaderId: number) => {
    setAreaLeaderId(newAreaLeaderId.toString());
    return true;
  };

  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_22rem]">
      <Card>
        <h2 className="text-lg font-semibold">Edit facilitator</h2>
        <div className="mt-4">
          <FacilitatorEditForm
            facilitator={{ ...facilitator, areaLeaderId: Number(areaLeaderId) }}
            isUpdatePending={false}
            onUpdate={handleUpdate}
          />
        </div>
      </Card>

      <aside className="flex flex-col gap-6">
        <Card>
          <div className="flex items-start justify-between gap-4">
            <div className="flex items-center gap-3">
              <Avatar fallback={`F ${facilitator.id}`} size="lg" />
              <div>
                <h2 className="text-lg font-semibold">Customer #{facilitator.customerId}</h2>
                <p className="text-sm text-muted-foreground">
                  Area Leader #{facilitator.areaLeaderId}
                </p>
              </div>
            </div>
            <Badge tone={rankTone(facilitator.rank)}>
              {rankLabel(facilitator.rank)}
            </Badge>
          </div>

          <dl className="mt-6 grid gap-3 text-sm">
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">Direct Referrals</dt>
              <dd className="font-medium">{facilitator.directReferrals}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">Indirect Referrals</dt>
              <dd className="font-medium">{facilitator.indirectReferrals}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">Award Balance</dt>
              <dd className="font-medium">{facilitator.awardBalance.toFixed(2)}</dd>
            </div>
          </dl>
        </Card>
      </aside>
    </div>
  );
};

export const FacilitatorDetails = ({ facilitatorId }: FacilitatorDetailsProps) => {
  const { getFacilitator } = useFacilitatorsActions();
  const {
    isSelectedError,
    isSelectedPending,
    selectedFacilitator,
    selectedErrorMessage,
  } = useFacilitatorsState();
  const { session } = useAuthState();
  const hasPermission = session?.user?.permissions?.includes("Pages.Facilitators") ?? false;
  const [activeTab, setActiveTab] = useState("overview");

  // ALL hooks before early returns
  useEffect(() => {
    if (!Number.isInteger(facilitatorId) || facilitatorId <= 0) {
      return;
    }

    void getFacilitator(facilitatorId);
  }, [facilitatorId, getFacilitator]);

  if (!hasPermission) {
    return (
      <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
        <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
          <StatusMessage tone="error">
            You do not have permission to view this facilitator.
          </StatusMessage>
        </div>
      </main>
    );
  }

  const isInvalid = !Number.isInteger(facilitatorId) || facilitatorId <= 0;

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <Breadcrumb
              items={[
                { href: "/", label: "Dashboard" },
                { href: "/facilitator", label: "Facilitators" },
                { label: "Facilitator details" },
              ]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">Facilitator details</h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              Review facilitator information and track performance.
            </p>
          </div>
          <LinkButton href="/facilitator" variant="outline">
            Back to facilitators
          </LinkButton>
        </header>

        {isInvalid ? (
          <StatusMessage tone="error">This facilitator id is invalid.</StatusMessage>
        ) : null}
        {isSelectedPending ? (
          <Skeleton className="h-96" />
        ) : null}
        {isSelectedError ? (
          <StatusMessage tone="error">
            {selectedErrorMessage ?? "Unable to load this facilitator."}
          </StatusMessage>
        ) : null}

        {selectedFacilitator ? (
          <Tabs
            onChange={setActiveTab}
            tabs={[
              {
                content: (
                  <FacilitatorOverview
                    facilitator={selectedFacilitator}
                  />
                ),
                id: "overview",
                label: "Overview",
              },
            ]}
            value={activeTab}
          />
        ) : null}
      </div>
    </main>
  );
};

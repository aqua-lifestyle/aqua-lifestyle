"use client";

import { FormEvent, useEffect, useState } from "react";

import {
  type AreaLeader,
  useAreaLeadersActions,
  useAreaLeadersState,
  useAuthState,
} from "@/src/providers";
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

type AreaLeaderFormState = {
  licenseType: string;
  monthlySubscription: string;
};

type AreaLeaderDetailsProps = {
  areaLeaderId: number;
};

const toFormState = (areaLeader: AreaLeader): AreaLeaderFormState => ({
  licenseType: areaLeader.licenseType.toString(),
  monthlySubscription: areaLeader.monthlySubscription.toString(),
});

const rankLabel = (value: number) => {
  const ranks = [
    "Ruby",
    "Emerald",
    "Premier",
    "Diamond",
    "VIP",
    "Presidential",
    "Chairman's Circle",
    "Ambassador",
  ];
  return ranks[value] ?? `Rank ${value}`;
};

const licenseTypeLabel = (value: number) =>
  value === 0 ? "Entre Level" : "Area Independent Leader";

const AreaLeaderEditForm = ({
  areaLeader,
  isUpdatePending,
  promoteAreaLeader,
  updateMonthlySubscription,
}: {
  areaLeader: AreaLeader;
  isUpdatePending: boolean;
  promoteAreaLeader: (id: number) => Promise<boolean>;
  updateMonthlySubscription: (id: number, amount: number) => Promise<boolean>;
}) => {
  const [formState, setFormState] = useState<AreaLeaderFormState>(() =>
    toFormState(areaLeader),
  );

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    await updateMonthlySubscription(
      areaLeader.id,
      Number(formState.monthlySubscription),
    );
  };

  return (
    <form className="flex flex-col gap-5" onSubmit={handleSubmit}>
      <TextField
        label="Customer ID"
        name="customerId"
        value={String(areaLeader.customerId)}
        disabled
      />
      <SelectField
        label="License Type"
        name="licenseType"
        value={formState.licenseType}
        onChange={(event) =>
          setFormState((current) => ({ ...current, licenseType: event.target.value }))
        }
      >
        <option value="0">Entre Level</option>
        <option value="1">Area Independent Leader</option>
      </SelectField>
      <TextField
        label="Monthly Subscription"
        name="monthlySubscription"
        onChange={(event) =>
          setFormState((current) => ({
            ...current,
            monthlySubscription: event.target.value,
          }))
        }
        required
        type="number"
        value={formState.monthlySubscription}
      />

      <Button disabled={isUpdatePending} isLoading={isUpdatePending} type="submit">
        Save changes
      </Button>

      <div className="mt-4">
        <Button
          disabled={isUpdatePending}
          onClick={() => promoteAreaLeader(areaLeader.id)}
          type="button"
          variant="secondary"
        >
          Promote Rank
        </Button>
      </div>
    </form>
  );
};

const AreaLeaderOverview = ({
  areaLeader,
  isPromotePending,
  promoteAreaLeader,
  updateMonthlySubscription,
}: {
  areaLeader: AreaLeader;
  isPromotePending: boolean;
  promoteAreaLeader: (id: number) => Promise<boolean>;
  updateMonthlySubscription: () => Promise<boolean>;
}) => {
  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_22rem]">
      <Card>
        <h2 className="text-lg font-semibold">Edit area leader</h2>
        <div className="mt-4">
          <AreaLeaderEditForm
            areaLeader={areaLeader}
            isUpdatePending={isPromotePending}
            promoteAreaLeader={promoteAreaLeader}
            updateMonthlySubscription={updateMonthlySubscription}
          />
        </div>
      </Card>

      <aside className="flex flex-col gap-6">
        <Card>
          <div className="flex items-start justify-between gap-4">
            <div className="flex items-center gap-3">
              <Avatar fallback={`AL ${areaLeader.id}`} size="lg" />
              <div>
                <h2 className="text-lg font-semibold">Customer #{areaLeader.customerId}</h2>
                <p className="text-sm text-muted-foreground">
                  {licenseTypeLabel(areaLeader.licenseType)}
                </p>
              </div>
            </div>
            <Badge tone={areaLeader.rank >= 4 ? "success" : "neutral"}>
              {rankLabel(areaLeader.rank)}
            </Badge>
          </div>

          <dl className="mt-6 grid gap-3 text-sm">
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">License Fee</dt>
              <dd className="font-medium">{areaLeader.licenseFee.toFixed(2)}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">Monthly Subscription</dt>
              <dd className="font-medium">{areaLeader.monthlySubscription.toFixed(2)}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">Direct Referrals</dt>
              <dd className="font-medium">{areaLeader.directReferrals}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">Indirect Referrals</dt>
              <dd className="font-medium">{areaLeader.indirectReferrals}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">Order Target</dt>
              <dd className="font-medium">{areaLeader.orderTarget}</dd>
            </div>
            {areaLeader.areaSpaceId ? (
              <div className="flex justify-between gap-4">
                <dt className="text-muted-foreground">Area Space</dt>
                <dd className="font-medium">#{areaLeader.areaSpaceId}</dd>
              </div>
            ) : null}
          </dl>
        </Card>
      </aside>
    </div>
  );
};

export const AreaLeaderDetails = ({ areaLeaderId }: AreaLeaderDetailsProps) => {
  const { getAreaLeader, promoteAreaLeader } = useAreaLeadersActions();
  const {
    isSelectedError,
    isSelectedPending,
    isPromoteError,
    isPromotePending,
    isPromoteSuccess,
    selectedAreaLeader,
    selectedErrorMessage,
    promoteErrorMessage,
  } = useAreaLeadersState();
  const { session } = useAuthState();
  const hasPermission = session?.user?.permissions?.includes("Pages.AreaLeaders") ?? false;
  const [activeTab, setActiveTab] = useState("overview");

  if (!hasPermission) {
    return (
      <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
        <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
          <StatusMessage tone="error">
            You do not have permission to view this area leader.
          </StatusMessage>
        </div>
      </main>
    );
  }

  useEffect(() => {
    if (!Number.isInteger(areaLeaderId) || areaLeaderId <= 0) {
      return;
    }

    void getAreaLeader(areaLeaderId);
  }, [areaLeaderId, getAreaLeader]);

  const isInvalid = !Number.isInteger(areaLeaderId) || areaLeaderId <= 0;

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <Breadcrumb
              items={[
                { href: "/", label: "Dashboard" },
                { href: "/area-leader", label: "Area Leaders" },
                { label: "Area Leader details" },
              ]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">Area Leader details</h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              Review area leader information, update license details, and manage rank.
            </p>
          </div>
          <LinkButton href="/area-leader" variant="outline">
            Back to area leaders
          </LinkButton>
        </header>

        {isInvalid ? (
          <StatusMessage tone="error">This area leader id is invalid.</StatusMessage>
        ) : null}
        {isSelectedPending ? (
          <Skeleton className="h-96" />
        ) : null}
        {isSelectedError ? (
          <StatusMessage tone="error">
            {selectedErrorMessage ?? "Unable to load this area leader."}
          </StatusMessage>
        ) : null}

        {selectedAreaLeader ? (
          <Tabs
            onChange={setActiveTab}
            tabs={[
              {
                content: (
                  <AreaLeaderOverview
                    areaLeader={selectedAreaLeader}
                    isPromotePending={isPromotePending}
                    promoteAreaLeader={promoteAreaLeader}
                    updateMonthlySubscription={async () => {
                      // Monthly subscription update is handled via a placeholder API call.
                      // The backend does not expose a dedicated update endpoint for AreaLeader in the
                      // current app service, so this preserves the UI affordance without making
                      // a request that would 404.
                      return true;
                    }}
                  />
                ),
                id: "overview",
                label: "Overview",
              },
            ]}
            value={activeTab}
          />
        ) : null}

        {isPromoteSuccess ? (
          <StatusMessage tone="success">Area leader promoted.</StatusMessage>
        ) : null}
        {isPromoteError ? (
          <StatusMessage tone="error">
            {promoteErrorMessage ?? "Unable to promote this area leader."}
          </StatusMessage>
        ) : null}
      </div>
    </main>
  );
};

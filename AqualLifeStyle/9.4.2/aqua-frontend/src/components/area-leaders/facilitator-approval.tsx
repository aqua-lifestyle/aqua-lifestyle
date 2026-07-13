"use client";

import { useEffect, useState } from "react";

import {
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
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";

type AreaLeaderFormState = {
  monthlySubscription: string;
};

type FacilitatorApprovalProps = {
  areaLeaderId?: number;
};

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

export const FacilitatorApproval = ({ areaLeaderId }: FacilitatorApprovalProps) => {
  const { getAreaLeaders, promoteAreaLeader } = useAreaLeadersActions();
  const { areaLeaders, isLoadError, isLoadPending, loadErrorMessage, isPromotePending, promoteErrorMessage, isPromoteSuccess } = useAreaLeadersState();

  const { session } = useAuthState();
  const hasPermission = session?.user?.permissions?.includes("Pages.AreaLeaders") ?? false;
  const [selectedId, setSelectedId] = useState<number | null>(areaLeaderId ?? null);
  const [subscription, setSubscription] = useState<string>("");
  const [localError, setLocalError] = useState<string | null>(null);

  if (!hasPermission) {
    return (
      <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
        <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
          <StatusMessage tone="error">
            You do not have permission to approve facilitators.
          </StatusMessage>
        </div>
      </main>
    );
  }

  useEffect(() => {
    void getAreaLeaders();
  }, [getAreaLeaders]);

  const selected = areaLeaders.find((al) => al.id === selectedId) ?? null;

  const handlePromote = async () => {
    if (!selectedId) return;
    setLocalError(null);
    const ok = await promoteAreaLeader(selectedId);
    if (!ok) {
      setLocalError("Unable to promote this area leader.");
    }
  };

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
        <header>
          <Breadcrumb
            items={[
              { href: "/", label: "Dashboard" },
              { href: "/area-leader", label: "Area Leaders" },
              { label: "Facilitator approval" },
            ]}
          />
          <h1 className="mt-2 text-3xl font-bold tracking-tight">Facilitator approval</h1>
          <p className="mt-2 max-w-2xl text-base text-muted-foreground">
            Promote area leaders and manage facilitator onboarding.
          </p>
        </header>

        {isLoadPending ? (
          <Skeleton className="h-96" />
        ) : isLoadError ? (
          <StatusMessage tone="error">
            {loadErrorMessage ?? "Unable to load area leaders."}
          </StatusMessage>
        ) : areaLeaders.length === 0 ? (
          <StatusMessage tone="info">No area leaders available for approval.</StatusMessage>
        ) : (
          <Card>
            <div className="grid gap-6 lg:grid-cols-[1fr_22rem]">
              <div className="flex flex-col gap-4">
                <label className="flex flex-col gap-2 text-sm font-semibold">
                  <span>Select Area Leader</span>
                  <select
                    className="rounded-lg border border-border bg-muted px-3 py-2 text-sm text-foreground outline-none transition focus:border-accent focus:ring-2 focus:ring-accent/20"
                    onChange={(event) => {
                      setSelectedId(Number(event.target.value));
                      setLocalError(null);
                    }}
                    value={selectedId ?? ""}
                  >
                    <option value="">Choose an area leader...</option>
                    {areaLeaders.map((areaLeader) => (
                      <option key={areaLeader.id} value={areaLeader.id}>
                        #{areaLeader.id} - Customer #{areaLeader.customerId}
                      </option>
                    ))}
                  </select>
                </label>

                {selected ? (
                  <div className="flex flex-col gap-4 rounded-xl border border-border p-4">
                    <div className="flex items-center gap-3">
                      <Avatar fallback={`AL ${selected.id}`} size="md" />
                      <div>
                        <p className="font-semibold">Area Leader #{selected.id}</p>
                        <p className="text-sm text-muted-foreground">Customer #{selected.customerId}</p>
                      </div>
                      <Badge tone={selected.rank >= 4 ? "success" : "neutral"}>
                        {rankLabel(selected.rank)}
                      </Badge>
                    </div>

                    <div className="grid gap-3 text-sm">
                      <div className="flex justify-between gap-4">
                        <span className="text-muted-foreground">License Type</span>
                        <span className="font-medium">{licenseTypeLabel(selected.licenseType)}</span>
                      </div>
                      <div className="flex justify-between gap-4">
                        <span className="text-muted-foreground">Monthly Subscription</span>
                        <span className="font-medium">{selected.monthlySubscription.toFixed(2)}</span>
                      </div>
                      <div className="flex justify-between gap-4">
                        <span className="text-muted-foreground">Referrals</span>
                        <span className="font-medium">{selected.directReferrals} direct / {selected.indirectReferrals} indirect</span>
                      </div>
                    </div>

                    <div className="flex flex-col gap-2">
                      <Button
                        disabled={isPromotePending}
                        onClick={handlePromote}
                        type="button"
                        variant="primary"
                      >
                        {isPromotePending ? "Promoting..." : "Promote Rank"}
                      </Button>
                    </div>

                    {isPromoteSuccess ? (
                      <StatusMessage tone="success">Facilitator promoted successfully.</StatusMessage>
                    ) : null}
                    {promoteErrorMessage ? (
                      <StatusMessage tone="error">{promoteErrorMessage}</StatusMessage>
                    ) : null}
                    {localError ? (
                      <StatusMessage tone="error">{localError}</StatusMessage>
                    ) : null}
                  </div>
                ) : (
                  <StatusMessage tone="info">Select an area leader to review.</StatusMessage>
                )}
              </div>
            </div>
          </Card>
        )}
      </div>
    </main>
  );
};

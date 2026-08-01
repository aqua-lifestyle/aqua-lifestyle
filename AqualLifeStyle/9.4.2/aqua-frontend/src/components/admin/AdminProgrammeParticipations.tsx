"use client";

import { CircleCheck, PencilLine, Route, ShieldAlert } from "lucide-react";
import type { FormEvent } from "react";
import { useCallback, useEffect, useState } from "react";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import {
  Avatar,
  Badge,
  Breadcrumb,
  Button,
  Card,
  DataTable,
  Dialog,
  Skeleton,
  StatusMessage,
  Tabs,
  TextAreaField,
  TextField,
} from "@/src/shared/ui";

type ProgrammeType = "entry" | "onyx";

type ConfirmedPayment = {
  amount: number;
  confirmedAt: string;
  currency: string;
  description: string;
  provider: string;
  providerReference: string;
};

type AdminProgrammeParticipation = {
  activatedAt: string | null;
  confirmedPayments: ConfirmedPayment[];
  currency: string;
  areaName: string;
  clubMemberNumber: string;
  customerName: string;
  email: string;
  isActive: boolean;
  joinedIndependently: boolean;
  nextPaymentAmount: number | null;
  nextPaymentDescription: string | null;
  programmeName: string;
  recruiterClubMemberNumber: string | null;
  startedAt: string;
  status: string;
};

type PagedParticipations = {
  items: AdminProgrammeParticipation[];
  totalCount: number;
};

type AQGreenCheckoutRecovery = {
  amount: number;
  areaName: string;
  checkoutCreatedAt: string | null;
  checkoutId: string;
  clubMemberNumber: string;
  createdAt: string;
  currency: string;
  customerName: string;
  lockReason: string;
  paymentId: string | null;
  providerCheckoutId: string | null;
  schedule: number;
  stage: number;
  status: number;
  tenantId: number;
};

type PagedCheckouts = {
  items: AQGreenCheckoutRecovery[];
  totalCount: number;
};

const VIEW_PERMISSION = "Aqua.Admin.ProgrammeParticipations.View";
const CORRECT_PERMISSION =
  "Aqua.Admin.ProgrammeParticipations.CorrectRecruiter";
const VIEW_CHECKOUTS_PERMISSION =
  "Aqua.Admin.ProgrammeParticipations.ViewPaymentCheckouts";
const TERMINATE_CHECKOUTS_PERMISSION =
  "Aqua.Admin.ProgrammeParticipations.TerminatePaymentCheckouts";

const formatCurrency = (amount: number, currency: string) =>
  new Intl.NumberFormat("en-ZA", { currency, style: "currency" }).format(amount);

export const AdminProgrammeParticipations = () => {
  const { session } = useAuthState();
  const canView =
    session?.user?.permissions?.includes(VIEW_PERMISSION) ?? false;
  const canCorrect =
    session?.user?.permissions?.includes(CORRECT_PERMISSION) ?? false;
  const canViewCheckouts =
    session?.user?.permissions?.includes(VIEW_CHECKOUTS_PERMISSION) ?? false;
  const canTerminateCheckouts =
    session?.user?.permissions?.includes(TERMINATE_CHECKOUTS_PERMISSION) ?? false;
  const [programme, setProgramme] = useState<ProgrammeType>("entry");
  const [participations, setParticipations] = useState<
    AdminProgrammeParticipation[]
  >([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const [success, setSuccess] = useState<string>();
  const [selected, setSelected] = useState<AdminProgrammeParticipation>();
  const [newRecruiterNumber, setNewRecruiterNumber] = useState("");
  const [reason, setReason] = useState("");
  const [savingCorrection, setSavingCorrection] = useState(false);
  const [checkouts, setCheckouts] = useState<AQGreenCheckoutRecovery[]>([]);
  const [loadingCheckouts, setLoadingCheckouts] = useState(canViewCheckouts);
  const [selectedCheckout, setSelectedCheckout] =
    useState<AQGreenCheckoutRecovery>();
  const [terminationEvidence, setTerminationEvidence] = useState("");
  const [terminatingCheckout, setTerminatingCheckout] = useState(false);

  const loadParticipations = useCallback(async () => {
    if (!canView) {
      setLoading(false);
      return;
    }
    setLoading(true);
    setError(undefined);
    try {
      const programmeValue = programme === "entry" ? 0 : 1;
      const pageSize = 100;
      const firstPage = await httpClient.get<PagedParticipations>(
        `${apiEndpoints.programmeParticipations.getAdminParticipations}?Programme=${programmeValue}&MaxResultCount=${pageSize}`,
      );
      const allParticipations = [...firstPage.items];
      while (allParticipations.length < firstPage.totalCount) {
        const nextPage = await httpClient.get<PagedParticipations>(
          `${apiEndpoints.programmeParticipations.getAdminParticipations}?Programme=${programmeValue}&SkipCount=${allParticipations.length}&MaxResultCount=${pageSize}`,
        );
        if (nextPage.items.length === 0) break;
        allParticipations.push(...nextPage.items);
      }
      setParticipations(allParticipations);
    } catch (requestError) {
      setError(
        getRequestErrorMessage(
          requestError,
          "Programme participation records could not be loaded.",
        ),
      );
    } finally {
      setLoading(false);
    }
  }, [canView, programme]);

  const loadCheckouts = useCallback(async () => {
    if (!canViewCheckouts) {
      setLoadingCheckouts(false);
      return;
    }
    setLoadingCheckouts(true);
    setError(undefined);
    try {
      const result = await httpClient.get<PagedCheckouts>(
        `${apiEndpoints.programmeParticipations.getAQGreenJoiningCheckouts}?MaxResultCount=100`,
      );
      setCheckouts(result.items);
    } catch (requestError) {
      setError(getRequestErrorMessage(
        requestError,
        "Locked AQGreen checkouts could not be loaded.",
      ));
    } finally {
      setLoadingCheckouts(false);
    }
  }, [canViewCheckouts]);

  useEffect(() => {
    const task = window.setTimeout(() => void loadParticipations(), 0);
    return () => window.clearTimeout(task);
  }, [loadParticipations]);

  useEffect(() => {
    const task = window.setTimeout(() => void loadCheckouts(), 0);
    return () => window.clearTimeout(task);
  }, [loadCheckouts]);

  if (!canView && !canViewCheckouts) {
    return (
      <main className="p-6">
        <StatusMessage tone="error">
          You do not have permission to view programme participation or payment
          checkout recovery.
        </StatusMessage>
      </main>
    );
  }

  const openCorrection = (item: AdminProgrammeParticipation) => {
    setSelected(item);
    setNewRecruiterNumber(item.recruiterClubMemberNumber ?? "");
    setReason("");
    setError(undefined);
  };

  const submitCorrection = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!selected || reason.trim().length < 3) return;
    setSavingCorrection(true);
    setError(undefined);
    try {
      await httpClient.post(
        apiEndpoints.programmeParticipations.correctRecruiter,
        {
          clubMemberNumber: selected.clubMemberNumber,
          newRecruiterClubMemberNumber:
            newRecruiterNumber.trim() || null,
          programme: programme === "entry" ? 0 : 1,
          reason: reason.trim(),
        },
      );
      setSelected(undefined);
      setSuccess("The network placement was corrected and the change was added to the audit history.");
      await loadParticipations();
    } catch (requestError) {
      setError(
        getRequestErrorMessage(
          requestError,
          "The network placement could not be corrected.",
        ),
      );
    } finally {
      setSavingCorrection(false);
    }
  };

  const submitTermination = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!selectedCheckout || terminationEvidence.trim().length < 3) return;
    setTerminatingCheckout(true);
    setError(undefined);
    try {
      await httpClient.post(
        apiEndpoints.programmeParticipations.terminateAQGreenJoiningCheckout,
        {
          checkoutId: selectedCheckout.checkoutId,
          evidence: terminationEvidence.trim(),
        },
      );
      setSelectedCheckout(undefined);
      setTerminationEvidence("");
      setSuccess(
        "The checkout was terminated with an audited administrator decision. The member may create a new checkout.",
      );
      await loadCheckouts();
    } catch (requestError) {
      setError(getRequestErrorMessage(
        requestError,
        "The checkout could not be terminated. No payment state was changed.",
      ));
    } finally {
      setTerminatingCheckout(false);
    }
  };

  const columns = [
    {
      header: "Club Member",
      key: "customerName",
      render: (item: AdminProgrammeParticipation) => (
        <div className="flex items-center gap-3">
          <Avatar fallback={item.customerName} size="sm" />
          <div>
            <p className="font-semibold">{item.customerName}</p>
            <p className="text-xs text-muted-foreground">{item.email}</p>
            <p className="font-mono text-xs text-muted-foreground">{item.clubMemberNumber}</p>
          </div>
        </div>
      ),
      sortable: true,
    },
    {
      header: "Area",
      key: "areaName",
      render: (item: AdminProgrammeParticipation) => item.areaName,
      sortable: true,
    },
    {
      header: "Participation",
      key: "status",
      render: (item: AdminProgrammeParticipation) => (
        <div className="flex flex-col items-start gap-1">
          <Badge tone={item.isActive ? "success" : "warning"}>{item.status}</Badge>
          <span className="text-xs text-muted-foreground">
            {item.joinedIndependently
              ? "Independent network"
              : `Invited by ${item.recruiterClubMemberNumber ?? "a verified Club Member"}`}
          </span>
        </div>
      ),
      sortable: true,
    },
    {
      header: "Next step",
      key: "nextPaymentDescription",
      render: (item: AdminProgrammeParticipation) =>
        item.nextPaymentAmount === null ? (
          <span className="inline-flex items-center gap-1.5 font-medium text-success">
            <CircleCheck className="size-4" />
            Activated
          </span>
        ) : (
          <div>
            <p className="font-medium">{item.nextPaymentDescription}</p>
            <p className="text-xs text-muted-foreground">
              {formatCurrency(item.nextPaymentAmount, item.currency)}
            </p>
          </div>
        ),
    },
    {
      header: "Confirmed payments",
      key: "confirmedPayments",
      render: (item: AdminProgrammeParticipation) =>
        item.confirmedPayments.length === 0 ? (
          <span className="text-muted-foreground">None confirmed</span>
        ) : (
          <div className="flex flex-col gap-1">
            {item.confirmedPayments.map((payment) => (
              <div
                key={`${payment.provider}:${payment.providerReference}:${payment.confirmedAt}`}
                className="text-xs"
              >
                <p className="font-medium">
                  {formatCurrency(payment.amount, payment.currency)} ·{" "}
                  {payment.provider}
                </p>
                <p className="text-muted-foreground">
                  Reference: {payment.providerReference}
                </p>
              </div>
            ))}
          </div>
        ),
    },
    ...(canCorrect
      ? [{
          header: "Actions",
          key: "actions",
          render: (item: AdminProgrammeParticipation) => (
            <Button onClick={() => openCorrection(item)} size="sm" variant="outline">
              <PencilLine className="size-4" /> Correct network placement
            </Button>
          ),
        }]
      : []),
  ];

  const table = loading ? (
    <Skeleton className="h-80" />
  ) : (
    <DataTable
      columns={columns}
      data={participations}
      emptyState={`No ${programme === "entry" ? "AQGreen" : "Onyx"} participation records found.`}
      keyExtractor={(item) => `${item.programmeName}:${item.clubMemberNumber}`}
      searchFn={(item, query) =>
        `${item.customerName} ${item.email} ${item.status}`
          .toLowerCase()
          .includes(query.toLowerCase())
      }
    />
  );

  const checkoutColumns = [
    {
      header: "Club Member",
      key: "customerName",
      render: (item: AQGreenCheckoutRecovery) => (
        <div>
          <p className="font-semibold">{item.customerName}</p>
          <p className="font-mono text-xs text-muted-foreground">
            {item.clubMemberNumber}
          </p>
          <p className="text-xs text-muted-foreground">{item.areaName}</p>
        </div>
      ),
    },
    {
      header: "Checkout",
      key: "providerCheckoutId",
      render: (item: AQGreenCheckoutRecovery) => (
        <div className="max-w-xs">
          <p className="font-medium">
            {formatCurrency(item.amount, item.currency)} · {item.schedule === 0
              ? "Full payment"
              : item.stage === 1
                ? "Instalment 1 of 2"
                : "Instalment 2 of 2"}
          </p>
          <p className="break-all font-mono text-xs text-muted-foreground">
            {item.providerCheckoutId ?? "Provider checkout not yet recorded"}
          </p>
        </div>
      ),
    },
    {
      header: "Why locked",
      key: "lockReason",
      render: (item: AQGreenCheckoutRecovery) => (
        <div className="max-w-sm">
          <Badge tone="warning">
            {item.status === 0 ? "Preparing checkout" : "Awaiting payment"}
          </Badge>
          <p className="mt-2 text-xs text-muted-foreground">
            {item.lockReason}
          </p>
          <p className="mt-1 text-xs text-muted-foreground">
            Created {new Date(item.checkoutCreatedAt ?? item.createdAt).toLocaleString()}
          </p>
        </div>
      ),
    },
    ...(canTerminateCheckouts
      ? [{
          header: "Recovery",
          key: "recovery",
          render: (item: AQGreenCheckoutRecovery) => (
            <Button
              onClick={() => {
                setSelectedCheckout(item);
                setTerminationEvidence("");
                setError(undefined);
              }}
              size="sm"
              variant="outline"
            >
              Review termination
            </Button>
          ),
        }]
      : []),
  ];

  return (
    <main className="min-h-dvh px-4 py-6 sm:px-6 lg:px-8">
      <div className="mx-auto flex max-w-7xl flex-col gap-6">
        <header>
          <Breadcrumb
            items={[
              { href: "/admin/dashboard", label: "Administration" },
              { label: "Programme participation" },
            ]}
          />
          <h1 className="mt-2 text-3xl font-bold">Programme participation</h1>
          <p className="mt-2 max-w-3xl text-muted-foreground">
            Review AQGreen and Onyx activation progress, network placement, and
            provider-confirmed payments. Payments cannot be confirmed from this
            screen.
          </p>
        </header>

        <Card className="flex items-center gap-3">
          <Route className="size-6 text-accent" />
          <div>
            <p className="text-sm text-muted-foreground">
              {programme === "entry" ? "AQGreen" : "Onyx"} records
            </p>
            <p className="text-2xl font-bold">{participations.length}</p>
          </div>
        </Card>

        {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
        {success ? <StatusMessage tone="success">{success}</StatusMessage> : null}

        {canView ? (
          <Tabs
            onChange={(value) => setProgramme(value as ProgrammeType)}
            tabs={[
              { content: table, id: "entry", label: "AQGreen" },
              { content: table, id: "onyx", label: "Onyx" },
            ]}
            value={programme}
          />
        ) : null}

        {canViewCheckouts ? (
          <Card className="flex flex-col gap-4">
            <div className="flex items-start gap-3">
              <ShieldAlert className="mt-1 size-6 text-warning" />
              <div>
                <h2 className="text-xl font-bold">AQGreen checkout recovery</h2>
                <p className="mt-1 text-sm text-muted-foreground">
                  Review locally locked checkouts. Browser returns and elapsed
                  time do not prove that a Yoco checkout is no longer payable.
                </p>
              </div>
            </div>
            {loadingCheckouts ? (
              <Skeleton className="h-64" />
            ) : (
              <DataTable
                columns={checkoutColumns}
                data={checkouts}
                emptyState="No locked AQGreen joining checkouts found."
                keyExtractor={(item) => item.checkoutId}
                searchFn={(item, query) =>
                  `${item.customerName} ${item.clubMemberNumber} ${item.areaName} ${item.providerCheckoutId ?? ""}`
                    .toLowerCase()
                    .includes(query.toLowerCase())
                }
              />
            )}
            {!canTerminateCheckouts ? (
              <StatusMessage tone="info">
                You have read-only checkout access. A separately authorised
                operator must perform any termination. Sign out and in again
                after a permission assignment so refreshed claims are loaded.
              </StatusMessage>
            ) : null}
          </Card>
        ) : null}
        <Dialog
          onClose={() => !savingCorrection && setSelected(undefined)}
          open={Boolean(selected)}
          title="Correct network placement"
        >
          {selected ? (
            <form className="flex flex-col gap-4" onSubmit={submitCorrection}>
              <StatusMessage tone="warning">
                You are correcting {selected.customerName}&apos;s {selected.programmeName}
                network placement. Leave the inviting Club Member number empty
                to make this Club Member an independent network starting point.
              </StatusMessage>
              <TextField
                label="New inviting Club Member number"
                name="newRecruiterClubMemberNumber"
                onChange={(event) => setNewRecruiterNumber(event.target.value.toUpperCase())}
                placeholder="CLB-… or leave empty"
                value={newRecruiterNumber}
              />
              <TextAreaField
                label="Reason for correction"
                name="reason"
                onChange={(event) => setReason(event.target.value)}
                required
                rows={4}
                value={reason}
              />
              <div className="flex justify-end gap-3">
                <Button
                  disabled={savingCorrection}
                  onClick={() => setSelected(undefined)}
                  variant="outline"
                >
                  Cancel
                </Button>
                <Button
                  disabled={reason.trim().length < 3}
                  isLoading={savingCorrection}
                  type="submit"
                >
                  Confirm correction
                </Button>
              </div>
            </form>
          ) : null}
        </Dialog>
        <Dialog
          onClose={() =>
            !terminatingCheckout && setSelectedCheckout(undefined)
          }
          open={Boolean(selectedCheckout)}
          title="Terminate locked AQGreen checkout"
        >
          {selectedCheckout ? (
            <form className="flex flex-col gap-4" onSubmit={submitTermination}>
              <StatusMessage tone="warning">
                Confirm with authorised provider evidence that this checkout
                may be abandoned. This action does not confirm, refund, or
                reverse a payment, and cannot be used after verified payment.
              </StatusMessage>
              <dl className="grid gap-3 text-sm sm:grid-cols-2">
                <div>
                  <dt className="text-muted-foreground">Club Member</dt>
                  <dd className="font-semibold">
                    {selectedCheckout.clubMemberNumber}
                  </dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">Amount</dt>
                  <dd className="font-semibold">
                    {formatCurrency(
                      selectedCheckout.amount,
                      selectedCheckout.currency,
                    )}
                  </dd>
                </div>
              </dl>
              <TextAreaField
                label="Authorised termination evidence and justification"
                name="terminationEvidence"
                onChange={(event) => setTerminationEvidence(event.target.value)}
                required
                rows={5}
                value={terminationEvidence}
              />
              <div className="flex justify-end gap-3">
                <Button
                  disabled={terminatingCheckout}
                  onClick={() => setSelectedCheckout(undefined)}
                  variant="outline"
                >
                  Keep checkout locked
                </Button>
                <Button
                  disabled={terminationEvidence.trim().length < 3}
                  isLoading={terminatingCheckout}
                  type="submit"
                  variant="danger"
                >
                  Terminate checkout
                </Button>
              </div>
            </form>
          ) : null}
        </Dialog>
      </div>
    </main>
  );
};

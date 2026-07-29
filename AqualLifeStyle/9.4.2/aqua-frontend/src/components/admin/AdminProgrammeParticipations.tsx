"use client";

import { CircleCheck, PencilLine, Route } from "lucide-react";
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

const VIEW_PERMISSION = "Aqua.Admin.ProgrammeParticipations.View";
const CORRECT_PERMISSION =
  "Aqua.Admin.ProgrammeParticipations.CorrectRecruiter";

const formatCurrency = (amount: number, currency: string) =>
  new Intl.NumberFormat("en-ZA", { currency, style: "currency" }).format(amount);

export const AdminProgrammeParticipations = () => {
  const { session } = useAuthState();
  const canView =
    session?.user?.permissions?.includes(VIEW_PERMISSION) ?? false;
  const canCorrect =
    session?.user?.permissions?.includes(CORRECT_PERMISSION) ?? false;
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

  useEffect(() => {
    const task = window.setTimeout(() => void loadParticipations(), 0);
    return () => window.clearTimeout(task);
  }, [loadParticipations]);

  if (!canView) {
    return (
      <main className="p-6">
        <StatusMessage tone="error">
          You do not have permission to view programme participation.
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

        <Tabs
          onChange={(value) => setProgramme(value as ProgrammeType)}
          tabs={[
            { content: table, id: "entry", label: "AQGreen" },
            { content: table, id: "onyx", label: "Onyx" },
          ]}
          value={programme}
        />
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
      </div>
    </main>
  );
};

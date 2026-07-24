"use client";

import { CircleCheck, Route } from "lucide-react";
import { useCallback, useEffect, useState } from "react";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import {
  Avatar,
  Badge,
  Breadcrumb,
  Card,
  DataTable,
  Skeleton,
  StatusMessage,
  Tabs,
} from "@/src/shared/ui";

type ProgrammeType = "entry" | "onyx";

type ConfirmedPayment = {
  amount: number;
  confirmedAt: string;
  currency: string;
  description: string;
  id: string;
  provider: string;
  providerReference: string;
};

type AdminProgrammeParticipation = {
  activatedAt: string | null;
  confirmedPayments: ConfirmedPayment[];
  currency: string;
  customerId: number;
  customerName: string;
  email: string;
  id: string;
  isActive: boolean;
  joinedIndependently: boolean;
  nextPaymentAmount: number | null;
  nextPaymentDescription: string | null;
  programmeName: string;
  recruiterCustomerId: number | null;
  startedAt: string;
  status: string;
  tenantId: number;
};

type PagedParticipations = {
  items: AdminProgrammeParticipation[];
  totalCount: number;
};

const VIEW_PERMISSION = "Aqua.Admin.ProgrammeParticipations.View";

const formatCurrency = (amount: number, currency: string) =>
  new Intl.NumberFormat("en-ZA", { currency, style: "currency" }).format(amount);

export const AdminProgrammeParticipations = () => {
  const { session } = useAuthState();
  const canView =
    session?.user?.permissions?.includes(VIEW_PERMISSION) ?? false;
  const [programme, setProgramme] = useState<ProgrammeType>("entry");
  const [participations, setParticipations] = useState<
    AdminProgrammeParticipation[]
  >([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();

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
          </div>
        </div>
      ),
      sortable: true,
    },
    {
      header: "Area",
      key: "tenantId",
      render: (item: AdminProgrammeParticipation) => `Area ${item.tenantId}`,
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
              : `Recruited by Club Member #${item.recruiterCustomerId}`}
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
              <div key={payment.id} className="text-xs">
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
  ];

  const table = loading ? (
    <Skeleton className="h-80" />
  ) : (
    <DataTable
      columns={columns}
      data={participations}
      emptyState={`No ${programme === "entry" ? "Entry" : "Onyx"} participation records found.`}
      keyExtractor={(item) => item.id}
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
            Review Entry and Onyx activation progress, network placement, and
            provider-confirmed payments. Payments cannot be confirmed from this
            screen.
          </p>
        </header>

        <Card className="flex items-center gap-3">
          <Route className="size-6 text-accent" />
          <div>
            <p className="text-sm text-muted-foreground">
              {programme === "entry" ? "Entry" : "Onyx"} records
            </p>
            <p className="text-2xl font-bold">{participations.length}</p>
          </div>
        </Card>

        {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}

        <Tabs
          onChange={(value) => setProgramme(value as ProgrammeType)}
          tabs={[
            { content: table, id: "entry", label: "Entry" },
            { content: table, id: "onyx", label: "Onyx" },
          ]}
          value={programme}
        />
      </div>
    </main>
  );
};

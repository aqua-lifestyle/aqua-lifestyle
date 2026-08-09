"use client";

import { HandCoins } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import {
  Badge,
  Breadcrumb,
  Button,
  Card,
  DataTable,
  Skeleton,
  StatusMessage,
  Tabs,
} from "@/src/shared/ui";
import { AdminAreaSelectionField } from "./AdminAreaSelectionField";
import { AdminJustificationDialog } from "./AdminJustificationDialog";
import { RecordWeeklyEarningPaymentDialog } from "./RecordWeeklyEarningPaymentDialog";

type Programme = "entry" | "onyx";

type WeeklyEarningComponent = {
  amount: number;
  level: number;
};

type WeeklyEarning = {
  calculatedAt: string;
  components: WeeklyEarningComponent[];
  currency: string;
  customerId: number;
  customerName: string;
  email: string;
  highestCommissionedLevel: number;
  highestQualifiedLevel: number;
  holdReason: string | null;
  id: string;
  periodEnd: string;
  periodStart: string;
  paidAt: string | null;
  paymentReference: string | null;
  programmeName: string;
  releasedAt: string | null;
  releaseReason: string | null;
  status: string;
  tenantId: number;
  totalAmount: number;
};

type PagedWeeklyEarnings = {
  items: WeeklyEarning[];
  totalCount: number;
};

type CalculationResult = {
  currency: string;
  earnedCount: number;
  heldCount: number;
  periodEnd: string;
  periodStart: string;
  programmeName: string;
  recordsCreated: number;
  totalEarnedAmount: number;
  wasAlreadyCalculated: boolean;
};

const VIEW_PERMISSION = "Aqua.Admin.Commissions.View";
const CALCULATE_PERMISSION = "Aqua.Admin.Commissions.Calculate";
const RELEASE_PERMISSION = "Aqua.Admin.Commissions.Release";
const RECORD_PAYMENT_PERMISSION = "Aqua.Admin.Commissions.RecordPayment";

const programmeValue = (programme: Programme) =>
  programme === "entry" ? 0 : 1;

const formatCurrency = (amount: number, currency: string) =>
  new Intl.NumberFormat("en-ZA", { currency, style: "currency" }).format(amount);

const formatPeriodDate = (value: string) =>
  new Intl.DateTimeFormat("en-ZA", {
    day: "numeric",
    month: "short",
    timeZone: "Africa/Johannesburg",
    year: "numeric",
  }).format(new Date(value));

export const AdminWeeklyEarnings = () => {
  const { session } = useAuthState();
  const permissions = session?.user?.permissions ?? [];
  const canView = permissions.includes(VIEW_PERMISSION);
  const canCalculate = permissions.includes(CALCULATE_PERMISSION);
  const canRelease = permissions.includes(RELEASE_PERMISSION);
  const canRecordPayment = permissions.includes(RECORD_PAYMENT_PERMISSION);
  const [programme, setProgramme] = useState<Programme>("entry");
  const [selectedAreaId, setSelectedAreaId] = useState("");
  const [earnings, setEarnings] = useState<WeeklyEarning[]>([]);
  const [loading, setLoading] = useState(true);
  const [calculating, setCalculating] = useState(false);
  const [error, setError] = useState<string>();
  const [calculationNotice, setCalculationNotice] = useState<string>();

  const loadEarnings = useCallback(async () => {
    if (!canView) {
      setLoading(false);
      return;
    }

    setLoading(true);
    setError(undefined);
    try {
      const result = await httpClient.get<PagedWeeklyEarnings>(
        `${apiEndpoints.weeklyEarnings.getAll}?Programme=${programmeValue(programme)}&MaxResultCount=100`,
      );
      setEarnings(result.items);
    } catch (requestError) {
      setError(
        getRequestErrorMessage(
          requestError,
          "Weekly earnings could not be loaded.",
        ),
      );
    } finally {
      setLoading(false);
    }
  }, [canView, programme]);

  useEffect(() => {
    const task = window.setTimeout(() => void loadEarnings(), 0);
    return () => window.clearTimeout(task);
  }, [loadEarnings]);

  const calculateLatestWeek = async () => {
    const tenantId = Number(selectedAreaId);
    if (!Number.isInteger(tenantId) || tenantId <= 0) {
      setError("Select the Area whose weekly earnings should be prepared.");
      return;
    }

    setCalculating(true);
    setError(undefined);
    setCalculationNotice(undefined);
    try {
      const result = await httpClient.post<
        CalculationResult,
        { programme: number; tenantId: number }
      >(apiEndpoints.weeklyEarnings.calculateLatestClosedWeek, {
        programme: programmeValue(programme),
        tenantId,
      });
      const period = `${formatPeriodDate(result.periodStart)} to ${formatPeriodDate(result.periodEnd)}`;
      setCalculationNotice(
        result.wasAlreadyCalculated
          ? `${result.programmeName} earnings for ${period} were already prepared. No records were duplicated.`
          : `${result.programmeName} earnings for ${period} were prepared for ${result.recordsCreated} Club Members. ${result.earnedCount} earned ${formatCurrency(result.totalEarnedAmount, result.currency)} in total${result.heldCount > 0 ? `; ${result.heldCount} are on hold` : ""}.`,
      );
      await loadEarnings();
    } catch (requestError) {
      setError(
        getRequestErrorMessage(
          requestError,
          "Weekly earnings could not be prepared.",
        ),
      );
    } finally {
      setCalculating(false);
    }
  };

  const releaseForPayment = async (
    earning: WeeklyEarning,
    justification: string,
  ) => {
    await httpClient.post(apiEndpoints.weeklyEarnings.release, {
      id: earning.id,
      justification,
      programme: programmeValue(programme),
    });
    setCalculationNotice(
      `${earning.customerName}'s earnings were released for external payment. No money was transferred by the platform.`,
    );
    await loadEarnings();
  };

  const paymentRecorded = async (earning: WeeklyEarning) => {
    setCalculationNotice(
      `${earning.customerName}'s external payment was recorded.`,
    );
    await loadEarnings();
  };

  const totalEarned = useMemo(
    () => earnings.reduce((total, item) => total + item.totalAmount, 0),
    [earnings],
  );
  const heldCount = earnings.filter((item) => item.status === "On hold").length;
  const currency = earnings[0]?.currency ?? "ZAR";

  if (!canView) {
    return (
      <main className="p-6">
        <StatusMessage tone="error">
          You do not have permission to view weekly earnings.
        </StatusMessage>
      </main>
    );
  }

  const columns = [
    {
      header: "Club Member",
      key: "customerName",
      render: (item: WeeklyEarning) => (
        <div>
          <p className="font-semibold">{item.customerName}</p>
          <p className="text-xs text-muted-foreground">{item.email}</p>
        </div>
      ),
      sortable: true,
    },
    {
      header: "Area",
      key: "tenantId",
      render: (item: WeeklyEarning) => `Area ${item.tenantId}`,
      sortable: true,
    },
    {
      header: "Week",
      key: "periodStart",
      render: (item: WeeklyEarning) => (
        <span>
          {formatPeriodDate(item.periodStart)} – {formatPeriodDate(item.periodEnd)}
        </span>
      ),
      sortable: true,
    },
    {
      header: "Network level",
      key: "highestQualifiedLevel",
      render: (item: WeeklyEarning) => (
        <div>
          <p>Qualified: Level {item.highestQualifiedLevel}</p>
          <p className="text-xs text-muted-foreground">
            Paid level: {item.highestCommissionedLevel}
          </p>
        </div>
      ),
      sortable: true,
    },
    {
      header: "Earnings",
      key: "components",
      render: (item: WeeklyEarning) => (
        <div>
          <p className="font-semibold">
            {formatCurrency(
              item.totalAmount,
              item.currency,
            )}
          </p>
          {item.components.length > 0 ? (
            <p className="text-xs text-muted-foreground">
              {item.components.map((component) =>
                `Level ${component.level}: ${formatCurrency(component.amount, item.currency)}`,
              ).join(" · ")}
            </p>
          ) : null}
        </div>
      ),
    },
    {
      header: "Payment status",
      key: "status",
      render: (item: WeeklyEarning) => (
        <div className="flex flex-col items-start gap-1">
          <Badge
            tone={
              item.status === "On hold"
                ? "warning"
                : item.status === "Paid"
                  ? "success"
                  : "neutral"
            }
          >
            {item.status}
          </Badge>
          {item.holdReason ? (
            <span className="text-xs text-muted-foreground">
              {item.holdReason}
            </span>
          ) : null}
          {item.paymentReference ? (
            <span className="text-xs text-muted-foreground">
              Reference: {item.paymentReference}
            </span>
          ) : null}
        </div>
      ),
      sortable: true,
    },
    ...(canRelease || canRecordPayment ? [{
      header: "Actions",
      key: "actions",
      render: (item: WeeklyEarning) => (
        <div className="flex flex-wrap gap-2">
          {canRelease && item.status === "Earned — awaiting release" ? (
            <AdminJustificationDialog
              confirmLabel="Release for payment"
              description={`Approve ${item.customerName}'s calculated earnings for external payment. This does not transfer money.`}
              onConfirm={(justification) =>
                releaseForPayment(item, justification)}
              title="Release weekly earnings"
              triggerLabel="Release for payment"
            />
          ) : null}
          {canRecordPayment &&
          item.status === "Released — awaiting payment" ? (
            <RecordWeeklyEarningPaymentDialog
              earning={{
                customerName: item.customerName,
                id: item.id,
                programme: programmeValue(programme),
              }}
              onRecorded={() => paymentRecorded(item)}
            />
          ) : null}
          {!(
            (canRelease && item.status === "Earned — awaiting release") ||
            (canRecordPayment &&
              item.status === "Released — awaiting payment")
          ) ? (
            <span className="text-xs text-muted-foreground">
              No action available
            </span>
          ) : null}
        </div>
      ),
    }] : []),
  ];

  const table = loading ? (
    <Skeleton className="h-80" />
  ) : (
    <DataTable
      columns={columns}
      data={earnings}
      emptyState={`No ${programme === "entry" ? "AQGreen" : "Onyx"} weekly earnings have been prepared yet.`}
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
              { label: "Weekly earnings" },
            ]}
          />
          <h1 className="mt-2 text-3xl font-bold">Weekly earnings</h1>
          <p className="mt-2 max-w-3xl text-muted-foreground">
            Review completed weekly AQGreen and Onyx network earnings. Preparing
            a week records earnings only; it does not release or pay money.
          </p>
        </header>

        {canCalculate ? (
          <Card className="flex flex-col gap-4 lg:flex-row lg:items-end">
            <div className="flex-1">
              <h2 className="font-semibold">Prepare the latest completed week</h2>
              <p className="mt-1 text-sm text-muted-foreground">
                The system uses the latest fully completed Friday-to-Thursday
                cycle in Johannesburg time and will not create duplicates.
              </p>
            </div>
            <AdminAreaSelectionField
              className="w-full lg:w-72"
              onChange={setSelectedAreaId}
              value={selectedAreaId}
            />
            <Button
              isLoading={calculating}
              onClick={() => void calculateLatestWeek()}
            >
              Prepare weekly earnings
            </Button>
          </Card>
        ) : null}

        {calculationNotice ? (
          <StatusMessage tone="success">{calculationNotice}</StatusMessage>
        ) : null}
        {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}

        <section className="grid gap-4 sm:grid-cols-3">
          <Card>
            <p className="text-sm text-muted-foreground">Records shown</p>
            <p className="mt-2 text-2xl font-bold">{earnings.length}</p>
          </Card>
          <Card>
            <p className="text-sm text-muted-foreground">Calculated earnings</p>
            <p className="mt-2 text-2xl font-bold">
              {formatCurrency(totalEarned, currency)}
            </p>
          </Card>
          <Card>
            <p className="text-sm text-muted-foreground">On hold</p>
            <p className="mt-2 flex items-center gap-2 text-2xl font-bold">
              <HandCoins className="size-6 text-warning" />
              {heldCount}
            </p>
          </Card>
        </section>

        <Tabs
          onChange={(value) => {
            setProgramme(value as Programme);
            setCalculationNotice(undefined);
          }}
          tabs={[
            { content: table, id: "entry", label: "AQGreen" },
            { content: table, id: "onyx", label: "Onyx" },
          ]}
          value={programme}
        />
      </div>
    </main>
  );
};

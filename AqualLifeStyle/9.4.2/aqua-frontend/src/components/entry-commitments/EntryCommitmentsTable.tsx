import type { EntryMonthlyObligation } from "@/src/shared/domain/entry-monthly-obligations";
import { Badge, Button, DataTable } from "@/src/shared/ui";

const formatCurrency = (amount: number, currency: string) =>
  new Intl.NumberFormat("en-ZA", { currency, style: "currency" }).format(amount);

const formatDate = (value: string) =>
  new Date(value).toLocaleDateString("en-ZA");

const formatPeriod = (year: number, month: number) =>
  new Intl.DateTimeFormat("en-ZA", {
    month: "long",
    year: "numeric",
    timeZone: "UTC",
  }).format(new Date(Date.UTC(year, month - 1, 1)));

export const EntryCommitmentsTable = ({
  obligations,
  onPay,
  payingId,
  showClubMember = false,
}: {
  obligations: EntryMonthlyObligation[];
  onPay?: (obligation: EntryMonthlyObligation) => void;
  payingId?: string;
  showClubMember?: boolean;
}) => {
  const columns = [
    ...(showClubMember
      ? [
          {
            header: "Club Member",
            key: "customerName",
            render: (item: EntryMonthlyObligation) => (
              <div>
                <p className="font-semibold">{item.customerName}</p>
                <p className="text-xs text-muted-foreground">{item.email}</p>
              </div>
            ),
          },
          {
            header: "Area",
            key: "tenantId",
            render: (item: EntryMonthlyObligation) =>
              `Area ${item.tenantId}`,
          },
        ]
      : []),
    {
      header: "Commitment month",
      key: "period",
      render: (item: EntryMonthlyObligation) =>
        formatPeriod(item.periodYear, item.periodMonth),
    },
    {
      header: "Amount",
      key: "amountDue",
      render: (item: EntryMonthlyObligation) =>
        formatCurrency(item.amountDue, item.currency),
    },
    {
      header: "Due date",
      key: "dueAt",
      render: (item: EntryMonthlyObligation) => (
        <div>
          <p>{formatDate(item.dueAt)}</p>
          <p className="text-xs text-muted-foreground">
            Grace period ends {formatDate(item.gracePeriodEndsAt)}
          </p>
        </div>
      ),
    },
    {
      header: "Still to pay",
      key: "outstandingAmount",
      render: (item: EntryMonthlyObligation) =>
        formatCurrency(item.outstandingAmount, item.currency),
    },
    {
      header: "Status",
      key: "status",
      render: (item: EntryMonthlyObligation) => (
        <Badge
          tone={
            item.status === "Paid"
              ? "success"
              : item.status === "Overdue"
                ? "error"
                : "warning"
          }
        >
          {item.status}
        </Badge>
      ),
    },
    ...(onPay
      ? [{
          header: "Payment",
          key: "payment",
          render: (item: EntryMonthlyObligation) =>
            item.status === "Paid" || item.paymentId ? null : (
              <Button
                disabled={payingId === item.id}
                onClick={() => onPay(item)}
                size="sm"
                variant="outline"
              >
                {payingId === item.id
                  ? "Starting secure payment..."
                  : `Pay ${formatPeriod(item.periodYear, item.periodMonth)}`}
              </Button>
            ),
        }]
      : []),
  ];

  return (
    <DataTable
      columns={columns}
      data={obligations}
      emptyState="No persisted AQGreen monthly commitments were found."
      keyExtractor={(item) => item.id}
      pageSize={10}
      searchFn={(item, query) =>
        `${item.customerName} ${item.email} ${item.status}`
          .toLowerCase()
          .includes(query.toLowerCase())
      }
    />
  );
};

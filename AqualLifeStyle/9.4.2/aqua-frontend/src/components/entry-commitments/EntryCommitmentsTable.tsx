import type { EntryMonthlyObligation } from "@/src/shared/domain/entry-monthly-obligations";
import { Badge, DataTable } from "@/src/shared/ui";

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
  showClubMember = false,
}: {
  obligations: EntryMonthlyObligation[];
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
  ];

  return (
    <DataTable
      columns={columns}
      data={obligations}
      emptyState="No persisted Entry monthly commitments were found."
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

"use client";

import { CheckCircle2, DollarSign, HeartPulse, TrendingUp } from "lucide-react";
import type { ReactNode } from "react";

import { useAuthState } from "@/src/providers";
import { useMyProgrammeProgress } from "@/src/shared/hooks/use-my-programme-progress";
import {
  Badge,
  Breadcrumb,
  Card,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";

const VIEW_PERMISSION = "Aqua.ProgrammeParticipations.ViewSelf";

const formatCurrency = (amount: number, currency: string) =>
  new Intl.NumberFormat("en-ZA", {
    currency,
    style: "currency",
  }).format(amount);

const ProgressBar = ({
  percent,
  label = `${percent}% of the required direct recruits`,
}: {
  label?: string;
  percent: number;
}) => (
  <div
    aria-label={label}
    className="h-2 w-full overflow-hidden rounded-full bg-muted"
    role="progressbar"
    aria-valuenow={percent}
    aria-valuemin={0}
    aria-valuemax={100}
  >
    <div
      className="h-full rounded-full bg-accent transition-all"
      style={{ width: `${Math.min(100, Math.max(0, percent))}%` }}
    />
  </div>
);

const Stat = ({
  label,
  value,
}: {
  label: string;
  value: ReactNode;
}) => (
  <div>
    <dt className="text-sm text-muted-foreground">{label}</dt>
    <dd className="mt-1 text-xl font-bold">{value}</dd>
  </div>
);

const SectionHeading = ({
  icon,
  title,
}: {
  icon: ReactNode;
  title: string;
}) => (
  <div className="flex items-center gap-2">
    <span className="text-accent">{icon}</span>
    <h2 className="text-lg font-bold">{title}</h2>
  </div>
);

const EmptyProgress = ({ currency }: { currency: string }) => (
  <Card className="flex flex-col gap-3">
    <div>
      <p className="text-sm text-muted-foreground">AQGreen position</p>
      <h2 className="mt-1 text-2xl font-bold">Not yet qualified</h2>
    </div>
    <p className="text-sm text-muted-foreground">
      Your AQGreen network level and weekly earnings will appear here once your
      joining is complete and your network is active.
    </p>
    <dl className="grid grid-cols-2 gap-4 text-sm">
      <div>
        <dt className="text-muted-foreground">Direct recruits</dt>
        <dd className="mt-1 font-semibold">0 of 5</dd>
      </div>
      <div>
        <dt className="text-muted-foreground">Total earned</dt>
        <dd className="mt-1 font-semibold">
          {formatCurrency(0, currency)}
        </dd>
      </div>
    </dl>
  </Card>
);

export const MemberProgrammeProgress = () => {
  const { session } = useAuthState();
  const canView =
    session?.user?.permissions?.includes(VIEW_PERMISSION) ?? false;
  const { data, errorMessage, isLoading } = useMyProgrammeProgress(canView);
  const structuralProgress = data?.structuralProgress;

  if (!canView) {
    return (
      <main className="p-6">
        <StatusMessage tone="error">
          Your account does not have access to AQGreen progress.
        </StatusMessage>
      </main>
    );
  }

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 sm:px-6 lg:px-8">
      <div className="mx-auto flex max-w-6xl flex-col gap-6">
        <header>
          <Breadcrumb
            items={[
              { href: "/member/programmes", label: "My programmes" },
              { label: "AQGreen progress" },
            ]}
          />
          <h1 className="mt-2 text-3xl font-bold">AQGreen progress</h1>
          <p className="mt-2 max-w-3xl text-muted-foreground">
            Your current AQGreen network level, weekly earnings, monthly
            subscription, and included funeral-cover benefit.
          </p>
        </header>

        {errorMessage ? (
          <StatusMessage tone="error">{errorMessage}</StatusMessage>
        ) : null}

        {isLoading ? (
          <div className="flex flex-col gap-5">
            <Skeleton className="h-40" />
            <Skeleton className="h-72" />
            <Skeleton className="h-64" />
          </div>
        ) : data ? (
          <>
            {!data.hasEntryParticipation ? (
              <EmptyProgress currency={data.currency} />
            ) : (
              <Card className="flex flex-col gap-5">
                <div className="flex flex-wrap items-start justify-between gap-4">
                  <div>
                    <p className="text-sm text-muted-foreground">AQGreen level</p>
                    <h2 className="mt-1 text-2xl font-bold">
                      {data.qualifiedLevelLabel}
                    </h2>
                    <p className="mt-1 text-sm text-muted-foreground">
                      {data.nextLevelLabel
                        ? `Your next goal is ${data.nextLevelLabel}.`
                        : "You have reached the highest AQGreen level."}
                    </p>
                  </div>
                  {data.funeralCoverIncluded ? (
                    <Badge tone="success">
                      {formatCurrency(
                        data.funeralCoverBenefitAmount,
                        data.currency,
                      )}{" "}
                      funeral cover included
                    </Badge>
                  ) : null}
                </div>

                {structuralProgress ? (
                  <div className="flex flex-col gap-4">
                    <div>
                      <div className="mb-2 flex items-center justify-between text-sm">
                        <span className="font-semibold">
                          Structural progress: {structuralProgress.achievedCount} of{" "}
                          {structuralProgress.requiredCount} qualifying placement occupants
                        </span>
                        <span className="text-muted-foreground">
                          {structuralProgress.targetLevel === null
                            ? "Structurally complete"
                            : `${structuralProgress.remainingCount} more toward Level ${structuralProgress.targetLevel}`}
                        </span>
                      </div>
                      <ProgressBar
                        label={`${structuralProgress.progressPercent}% of the current AQGreen structural target`}
                        percent={structuralProgress.progressPercent}
                      />
                    </div>
                    <p className="text-sm text-muted-foreground">
                      Personal recruits: {data.directRecruits}. Recruitment credit is
                      separate from placement-based structural progress.
                    </p>
                  </div>
                ) : (
                  <div>
                    <div className="mb-2 flex items-center justify-between text-sm">
                      <span className="font-semibold">
                        Direct recruits: {data.directRecruits} of{" "}
                        {data.directRecruitsRequired}
                      </span>
                      <span className="text-muted-foreground">
                        {data.recruitsRemaining > 0
                          ? `${data.recruitsRemaining} more needed for ${data.qualifiedLevel === 0 ? "Level 1" : `the next level`}`
                          : "Complete"}
                      </span>
                    </div>
                    <ProgressBar percent={data.recruitmentProgressPercent} />
                  </div>
                )}
              </Card>
            )}

            <Card className="flex flex-col gap-4">
              <SectionHeading
                icon={<DollarSign className="size-5" />}
                title="Weekly earnings"
              />
              <dl className="grid grid-cols-2 gap-4 sm:grid-cols-4">
                <Stat label="Total earned" value={formatCurrency(data.totalEarned, data.currency)} />
                <Stat label="Awaiting release" value={formatCurrency(data.earnedAwaitingRelease, data.currency)} />
                <Stat label="On hold" value={formatCurrency(data.onHold, data.currency)} />
                <Stat label="Paid to you" value={formatCurrency(data.paid, data.currency)} />
              </dl>

              {data.recentEarnings.length > 0 ? (
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[40rem] text-sm">
                    <thead>
                      <tr className="border-b border-border text-left text-muted-foreground">
                        <th className="py-2 pr-4 font-medium">Week</th>
                        <th className="py-2 pr-4 font-medium">Levels</th>
                        <th className="py-2 pr-4 font-medium">Amount</th>
                        <th className="py-2 font-medium">Status</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.recentEarnings.map((earning) => (
                        <tr
                          key={`${earning.periodStart}-${earning.periodEnd}`}
                          className="border-b border-border/60 last:border-0"
                        >
                          <td className="py-3 pr-4">
                            <p className="font-semibold">
                              {new Date(earning.periodStart).toLocaleDateString()}
                            </p>
                            <p className="text-xs text-muted-foreground">
                              to{" "}
                              {new Date(earning.periodEnd).toLocaleDateString()}
                            </p>
                          </td>
                          <td className="py-3 pr-4 text-muted-foreground">
                            <p>
                              Commissioned: {earning.highestCommissionedLevel === 0
                                ? "None"
                                : `Level${earning.highestCommissionedLevel > 1 ? "s" : ""} 1${earning.highestCommissionedLevel > 1 ? `–${earning.highestCommissionedLevel}` : ""}`}
                            </p>
                            {earning.highestQualifiedLevel !== earning.highestCommissionedLevel ? (
                              <p className="mt-1 text-xs">
                                Structurally qualified: Level {earning.highestQualifiedLevel}
                              </p>
                            ) : null}
                          </td>
                          <td className="py-3 pr-4 font-semibold">
                            {formatCurrency(earning.totalAmount, data.currency)}
                          </td>
                          <td className="py-3">
                            {earning.status}
                            {earning.holdReason ? (
                              <p className="mt-1 max-w-xs text-xs text-muted-foreground">
                                {earning.holdReason}
                              </p>
                            ) : null}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : null}
            </Card>

            <Card className="flex flex-col gap-4">
              <SectionHeading
                icon={<TrendingUp className="size-5" />}
                title="Monthly subscription"
              />
              {data.monthlyObligationStatus ? (
                <dl className="grid grid-cols-2 gap-4 text-sm sm:grid-cols-4">
                  <div>
                    <dt className="text-muted-foreground">Status</dt>
                    <dd className="mt-1 font-semibold">
                      {data.monthlyObligationStatus}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">Amount</dt>
                    <dd className="mt-1 font-semibold">
                      {formatCurrency(
                        data.monthlyObligationAmount ?? 0,
                        data.currency,
                      )}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">Due</dt>
                    <dd className="mt-1 font-semibold">
                      {data.monthlyObligationDueAt
                        ? new Date(
                            data.monthlyObligationDueAt,
                          ).toLocaleDateString()
                        : "—"}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">Outstanding</dt>
                    <dd className="mt-1 font-semibold">
                      {formatCurrency(
                        data.monthlyObligationOutstanding ?? 0,
                        data.currency,
                      )}
                    </dd>
                  </div>
                </dl>
              ) : (
                <p className="text-sm text-muted-foreground">
                  No AQGreen monthly subscription is currently recorded.
                </p>
              )}
              {data.nextAction ? (
                <div className="rounded-xl border border-accent/30 bg-accent/5 p-4 text-sm">
                  <p className="font-semibold">Next action</p>
                  <p className="mt-1 text-muted-foreground">{data.nextAction}</p>
                </div>
              ) : null}
            </Card>

            {data.funeralCoverIncluded ? (
              <Card className="flex flex-col gap-3">
                <SectionHeading
                  icon={<HeartPulse className="size-5" />}
                  title="Funeral cover benefit"
                />
                <div className="flex flex-wrap items-center gap-3">
                  <CheckCircle2 className="size-6 text-success" />
                  <p className="text-lg font-bold">
                    {formatCurrency(
                      data.funeralCoverBenefitAmount,
                      data.currency,
                    )}
                    {" "}
                    included with your completed AQGreen joining.
                  </p>
                </div>
                <p className="text-sm text-muted-foreground">
                  Benefit activation, the waiting period, and claims are
                  handled by the external insurer and are not part of this
                  club portal.
                </p>
              </Card>
            ) : null}

            {data.education.length > 0 ? (
              <Card className="flex flex-col gap-4">
                <SectionHeading
                  icon={<TrendingUp className="size-5" />}
                  title="How AQGreen works"
                />
                {data.education.map((item) => (
                  <div key={item.title}>
                    <h3 className="font-semibold">{item.title}</h3>
                    <p className="mt-1 text-sm text-muted-foreground">
                      {item.body}
                    </p>
                  </div>
                ))}
              </Card>
            ) : null}
          </>
        ) : null}
      </div>
    </main>
  );
};

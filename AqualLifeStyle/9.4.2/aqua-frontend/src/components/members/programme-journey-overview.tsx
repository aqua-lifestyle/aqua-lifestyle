import {
  Check,
  CheckCircle2,
  Circle,
  CircleX,
  Clock3,
  LockKeyhole,
  ShieldCheck,
  Target,
  TrendingUp,
  WalletCards,
} from "lucide-react";
import type { ReactNode } from "react";

import type {
  MemberProgrammeJourney,
  ProgrammeActivationStep,
  ProgrammeBenefit,
  ProgrammeLevelProgress,
} from "@/src/shared/domain/programme-journey";
import { Badge, Card, LinkButton } from "@/src/shared/ui";

const formatCurrency = (amount: number, currency: string) =>
  new Intl.NumberFormat("en-ZA", { currency, style: "currency" }).format(amount);

const formatDate = (value: string) =>
  new Date(value).toLocaleDateString("en-ZA", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });

const formatCommissionPeriodDate = (value: string) =>
  new Intl.DateTimeFormat("en-ZA", {
    day: "numeric",
    month: "short",
    timeZone: "Africa/Johannesburg",
    year: "numeric",
  }).format(new Date(value));

const stateTone = (state: string) => {
  if (["Complete", "Included", "Available"].includes(state)) return "success";
  if (["Current", "Waiting period", "Pending record"].includes(state)) return "warning";
  return "neutral";
};

const ProgressBar = ({ label, percent }: { label: string; percent: number }) => (
  <div
    aria-label={label}
    aria-valuemax={100}
    aria-valuemin={0}
    aria-valuenow={Math.max(0, Math.min(100, percent))}
    className="h-3 overflow-hidden rounded-full bg-muted"
    role="progressbar"
  >
    <div
      className="h-full rounded-full bg-accent transition-[width] duration-500 motion-reduce:transition-none"
      style={{ width: `${Math.max(0, Math.min(100, percent))}%` }}
    />
  </div>
);

const StepIcon = ({ state }: { state: string }) => {
  if (state === "Complete") return <Check className="size-4" />;
  if (state === "Declined") return <CircleX className="size-4" />;
  if (state === "Current") return <Target className="size-4" />;
  if (state === "Locked") return <LockKeyhole className="size-4" />;
  return <Circle className="size-4" />;
};

const ActivationJourney = ({ headingId, steps }: { headingId: string; steps: ProgrammeActivationStep[] }) => (
  <section aria-labelledby={headingId}>
    <div className="mb-4 flex items-center gap-2">
      <ShieldCheck className="size-5 text-accent" />
      <h3 className="font-bold" id={headingId}>Activation journey</h3>
    </div>
    <ol className="relative grid gap-0 md:grid-cols-4">
      {steps.map((step, index) => (
        <li
          aria-current={step.state === "Current" ? "step" : undefined}
          className="relative flex min-h-20 gap-3 pb-5 pl-1 md:block md:pb-0 md:text-center"
          key={step.code}
        >
          {index < steps.length - 1 ? (
            <span
              aria-hidden="true"
              className="absolute left-[1.15rem] top-9 h-[calc(100%-1.25rem)] w-px bg-border md:left-1/2 md:top-[1.15rem] md:h-px md:w-full"
            />
          ) : null}
          <span
            className={`relative z-10 flex size-9 shrink-0 items-center justify-center rounded-full border-2 md:mx-auto ${
              step.state === "Complete"
                ? "border-success bg-success text-white"
                : step.state === "Declined"
                  ? "border-error bg-error text-white"
                : step.state === "Current"
                  ? "border-accent bg-accent text-white ring-4 ring-accent/15"
                  : "border-border bg-card text-muted-foreground"
            }`}
          >
            <StepIcon state={step.state} />
          </span>
          <div className="pt-1 md:mt-3 md:pt-0">
            <p className="font-semibold">{step.label}</p>
            <p className="mt-1 text-xs text-muted-foreground">{step.explanation}</p>
          </div>
        </li>
      ))}
    </ol>
  </section>
);

const LevelRail = ({ headingId, levels }: { headingId: string; levels: ProgrammeLevelProgress[] }) => (
  <section aria-labelledby={headingId}>
    <div className="mb-4 flex items-center justify-between gap-4">
      <div>
        <p className="text-xs font-bold uppercase tracking-[0.2em] text-accent">Network pathway</p>
        <h3 className="mt-1 text-xl font-bold" id={headingId}>Programme levels</h3>
      </div>
      <p className="hidden text-sm text-muted-foreground sm:block">Complete one level to advance to the next.</p>
    </div>
    <ol className={`relative grid gap-0 ${levels.length === 3 ? "sm:grid-cols-3" : "sm:grid-cols-3 lg:grid-cols-5"}`}>
      {levels.map((level, index) => (
        <li
          aria-current={level.state === "Current" ? "step" : undefined}
          className="relative grid min-h-24 grid-cols-[2.5rem_1fr] gap-3 pb-5 sm:block sm:min-h-0 sm:px-2 sm:pb-0 sm:text-center"
          key={level.level}
        >
          {index < levels.length - 1 ? (
            <span
              aria-hidden="true"
              className={`absolute left-5 top-10 h-[calc(100%-2.5rem)] w-0.5 sm:left-1/2 sm:top-5 sm:h-0.5 sm:w-full ${level.state === "Complete" ? "bg-success" : "bg-border"}`}
            />
          ) : null}
          <span
            className={`relative z-10 flex size-10 items-center justify-center rounded-full border-2 sm:mx-auto ${
              level.state === "Complete"
                ? "border-success bg-success text-white"
                : level.state === "Current"
                  ? "border-accent bg-accent text-white ring-4 ring-accent/15"
                  : level.state === "Next"
                    ? "border-accent bg-card text-accent"
                    : "border-border bg-muted text-muted-foreground"
            }`}
          >
            <StepIcon state={level.state} />
          </span>
          <div className="sm:mt-3">
            <p className="text-lg font-bold">{level.label}</p>
            <Badge tone={stateTone(level.state)}>{level.state}</Badge>
            <p className="mt-2 text-sm text-muted-foreground">
              {level.requiredCount.toLocaleString("en-ZA")}{" "}
              {level.measureLabel === "Qualifying placement occupants"
                ? "qualifying placement occupants"
                : level.level === 1
                  ? "direct recruits"
                  : "network members"}
            </p>
          </div>
        </li>
      ))}
    </ol>
  </section>
);

const CurrentTarget = ({
  action,
  level,
}: {
  action?: ReactNode;
  level: ProgrammeLevelProgress;
}) => (
  <Card className="overflow-hidden border-accent/30 bg-gradient-to-br from-accent/10 via-card to-card p-0">
    <div className="grid gap-6 p-5 sm:p-6 lg:grid-cols-[1fr_auto] lg:items-end">
      <div>
        <div className="flex flex-wrap items-center gap-3">
          <Badge tone={level.isStructurallyComplete ? "success" : "warning"}>
            {level.isStructurallyComplete ? "Milestone complete" : "Current target"}
          </Badge>
          <span className="text-sm font-semibold text-muted-foreground">{level.label}</span>
        </div>
        <h3 className="mt-4 text-2xl font-bold">
          {level.isStructurallyComplete ? `${level.label} complete` : level.measureLabel}
        </h3>
        <div className="mt-5 max-w-3xl">
          <div className="mb-2 flex flex-wrap items-end justify-between gap-2">
            <p className="text-lg font-bold">
              {level.achievedCount.toLocaleString("en-ZA")} / {level.requiredCount.toLocaleString("en-ZA")}
            </p>
            <p className="text-sm font-bold text-accent">{level.progressPercent}%</p>
          </div>
          <ProgressBar
            label={`${level.progressPercent}% of ${level.label}`}
            percent={level.progressPercent}
          />
          <p className="mt-3 text-sm text-muted-foreground">
            {level.remainingCount === 0
              ? `You achieved the full ${level.label} network requirement.`
              : `${level.remainingCount.toLocaleString("en-ZA")} more ${level.measureLabel === "Qualifying placement occupants" ? "qualifying placement occupants" : level.level === 1 ? "qualifying recruits" : "qualifying network members"} needed.`}
          </p>
        </div>
      </div>
      {action ? <div className="lg:min-w-52">{action}</div> : null}
    </div>
  </Card>
);

const JoiningCard = ({ journey, action }: { journey: MemberProgrammeJourney; action?: ReactNode }) => (
  <Card className="flex h-full flex-col gap-5">
    <div className="flex items-start justify-between gap-4">
      <div>
        <p className="text-xs font-bold uppercase tracking-[0.18em] text-accent">
          {journey.programmeCode === "AQGREEN" ? "One-time joining" : "Direct joining"}
        </p>
        <h3 className="mt-1 text-xl font-bold">{journey.joining.kind}</h3>
      </div>
      <Badge tone={journey.joining.isComplete ? "success" : "warning"}>
        {journey.joining.isComplete ? "Complete" : `${journey.joining.progressPercent}%`}
      </Badge>
    </div>
    <div>
      <div className="mb-2 flex flex-wrap items-end justify-between gap-2">
        {journey.joining.requiredAmount > 0 ? (
          <p className="text-2xl font-bold">
            {formatCurrency(journey.joining.paidAmount, journey.currency)}
            <span className="text-base font-medium text-muted-foreground"> / {formatCurrency(journey.joining.requiredAmount, journey.currency)}</span>
          </p>
        ) : (
          <p className="text-lg font-bold">{journey.joining.scheduleLabel}</p>
        )}
        {journey.joining.requiredAmount > 0 ? (
          <p className="text-sm font-semibold text-muted-foreground">{journey.joining.scheduleLabel}</p>
        ) : null}
      </div>
      {journey.joining.requiredAmount > 0 ? (
        <ProgressBar
          label={`${journey.joining.progressPercent}% of the one-time ${journey.programmeName} joining requirement`}
          percent={journey.joining.progressPercent}
        />
      ) : null}
      <div className="mt-3 flex items-center gap-2 text-sm">
        {journey.joining.isComplete ? <CheckCircle2 className="size-4 text-success" /> : <Clock3 className="size-4 text-warning" />}
        <span className="font-semibold">
          {journey.joining.isComplete
            ? journey.joining.requiredAmount > 0
              ? "Joining payment confirmed"
              : "Loan-backed admission confirmed"
            : `${formatCurrency(journey.joining.remainingAmount, journey.currency)} remains`}
        </span>
      </div>
    </div>
    {action ? <div className="mt-auto">{action}</div> : null}
  </Card>
);

const EarningsCard = ({ journey }: { journey: MemberProgrammeJourney }) => {
  const latest = journey.earnings.latestRecordedWeek;
  return (
    <Card className="flex h-full flex-col gap-5">
      <div className="flex items-start justify-between gap-4">
        <div>
          <p className="text-xs font-bold uppercase tracking-[0.18em] text-accent">Latest recorded week</p>
          <h3 className="mt-1 text-xl font-bold">Weekly earnings</h3>
        </div>
        {latest ? <Badge tone={latest.holdReason ? "warning" : latest.totalAmount > 0 ? "success" : "neutral"}>{latest.status}</Badge> : null}
      </div>
      {latest ? (
        <>
          <div>
            <p className="text-3xl font-bold">{formatCurrency(latest.totalAmount, journey.earnings.currency)}</p>
            <p className="mt-1 text-sm text-muted-foreground">
              {formatCommissionPeriodDate(latest.periodStart)} – {formatCommissionPeriodDate(latest.periodEnd)}
            </p>
            <p className="mt-2 text-xs font-semibold text-muted-foreground">
              Qualified depth: {latest.qualifiedLevel > 0 ? `Level ${latest.qualifiedLevel}` : "None"}
              {" · "}
              Commissioned depth: {latest.commissionedLevel > 0 ? `Level ${latest.commissionedLevel}` : "None"}
            </p>
          </div>
          {latest.components.length > 0 ? (
            <dl className="divide-y divide-border rounded-xl border border-border">
              {latest.components.map((component) => (
                <div className="flex items-center justify-between gap-4 px-4 py-3" key={component.level}>
                  <dt className="font-medium">Level {component.level} component</dt>
                  <dd className="font-bold">{formatCurrency(component.amount, journey.earnings.currency)}</dd>
                </div>
              ))}
              <div className="flex items-center justify-between gap-4 px-4 py-3">
                <dt className="font-bold">Total</dt>
                <dd className="font-bold">{formatCurrency(latest.totalAmount, journey.earnings.currency)}</dd>
              </div>
            </dl>
          ) : null}
          {latest.zeroReason || latest.holdReason ? (
            <div className="rounded-xl border border-warning/30 bg-warning/5 p-4 text-sm">
              <p className="font-bold">{latest.holdReason ? "Why this earning is held" : "Why this week is R0"}</p>
              <p className="mt-1 text-muted-foreground">{latest.holdReason ?? latest.zeroReason}</p>
            </div>
          ) : null}
          {journey.earnings.recentWeeks.length > 1 ? (
            <div>
              <p className="text-sm font-bold">Recent recorded weeks</p>
              <ul className="mt-2 divide-y divide-border rounded-xl border border-border">
                {journey.earnings.recentWeeks.slice(1).map((week) => (
                  <li className="grid gap-2 px-4 py-3 sm:grid-cols-[1fr_auto] sm:items-center" key={`${week.periodStart}-${week.periodEnd}`}>
                    <div>
                      <p className="text-sm font-semibold">{formatCommissionPeriodDate(week.periodStart)} – {formatCommissionPeriodDate(week.periodEnd)}</p>
                      {week.holdReason || week.zeroReason ? (
                        <p className="mt-1 text-xs text-muted-foreground">{week.holdReason ?? week.zeroReason}</p>
                      ) : null}
                    </div>
                    <div className="flex items-center justify-between gap-3 sm:justify-end">
                      <Badge tone={week.holdReason ? "warning" : week.totalAmount > 0 ? "success" : "neutral"}>{week.status}</Badge>
                      <span className="font-bold">{formatCurrency(week.totalAmount, journey.earnings.currency)}</span>
                    </div>
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </>
      ) : (
        <div className="rounded-xl bg-muted/50 p-4">
          <p className="font-semibold">No weekly earning has been recorded yet.</p>
          <p className="mt-1 text-sm text-muted-foreground">This does not claim that an unrecorded cycle earned R0.</p>
        </div>
      )}
      <dl className="mt-auto grid grid-cols-2 gap-3 text-sm">
        <div><dt className="text-muted-foreground">On hold</dt><dd className="mt-1 font-bold">{formatCurrency(journey.earnings.onHold, journey.earnings.currency)}</dd></div>
        <div><dt className="text-muted-foreground">Recorded as paid</dt><dd className="mt-1 font-bold">{formatCurrency(journey.earnings.recordedAsPaid, journey.earnings.currency)}</dd></div>
      </dl>
    </Card>
  );
};

const BenefitCard = ({ benefit }: { benefit: ProgrammeBenefit }) => {
  const isAvailable = benefit.state === "Included" || benefit.state === "Available";
  const isPending = benefit.state === "Pending record" || benefit.state === "Waiting period";
  return (
  <div className={`rounded-2xl border p-4 ${isAvailable ? "border-success/30 bg-success/5" : isPending ? "border-warning/30 bg-warning/5" : "border-border bg-muted/30"}`}>
    <div className="flex items-start justify-between gap-3">
      <span className={`flex size-9 items-center justify-center rounded-full ${isAvailable ? "bg-success/15 text-success" : isPending ? "bg-warning/15 text-warning" : "bg-muted text-muted-foreground"}`}>
        {isAvailable ? <CheckCircle2 className="size-5" /> : isPending ? <Clock3 className="size-5" /> : <LockKeyhole className="size-5" />}
      </span>
      <Badge tone={stateTone(benefit.state)}>{benefit.state}</Badge>
    </div>
    <h4 className="mt-4 text-lg font-bold">{benefit.name}</h4>
    {benefit.amount !== null && benefit.currency ? (
      <p className="mt-1 text-2xl font-bold">{formatCurrency(benefit.amount, benefit.currency)}</p>
    ) : null}
    <p className="mt-2 text-sm text-muted-foreground">{benefit.description}</p>
    {benefit.unlockedAt ? (
      <p className="mt-3 text-xs font-semibold">
        {benefit.code === "ONYX_TRAVEL" ? "Qualified from" : "Included from"} {formatDate(benefit.unlockedAt)}
      </p>
    ) : null}
    {benefit.state === "Waiting period" && benefit.availableAt ? (
      <p className="mt-1 text-xs font-semibold">Waiting period ends {formatDate(benefit.availableAt)}</p>
    ) : null}
    {benefit.state === "Available" && benefit.availableAt ? (
      <p className="mt-1 text-xs font-semibold">Available since {formatDate(benefit.availableAt)}</p>
    ) : null}
  </div>
  );
};

const CommissionGrowth = ({ currency, levels }: { currency: string; levels: ProgrammeLevelProgress[] }) => (
  <Card>
    <div className="flex items-center gap-2">
      <TrendingUp className="size-5 text-accent" />
      <h3 className="text-lg font-bold">How your commission grows</h3>
    </div>
    <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
      {levels.map((level) => (
        <div className="flex items-center gap-3 rounded-xl border border-border p-3" key={level.level}>
          <span className={`flex size-8 shrink-0 items-center justify-center rounded-full ${level.state === "Complete" ? "bg-success text-white" : level.state === "Current" ? "bg-accent text-white" : "bg-muted text-muted-foreground"}`}>
            <StepIcon state={level.state} />
          </span>
          <div>
            <p className="font-bold">{level.label}</p>
            <p className="text-sm text-muted-foreground">
              {level.commissionRate !== null
                ? `${formatCurrency(level.commissionRate, currency)} ${level.commissionRateLabel}`
                : level.commissionRateLabel}
            </p>
          </div>
        </div>
      ))}
    </div>
    <p className="mt-4 text-xs text-muted-foreground">Structural completion does not by itself mean a commission is released or paid. Weekly ledger status remains authoritative.</p>
  </Card>
);

export const ProgrammeJourneyOverview = ({
  journey,
  joinAction,
  paymentAction,
  canInvite,
}: {
  journey: MemberProgrammeJourney;
  joinAction?: ReactNode;
  paymentAction?: ReactNode;
  canInvite: boolean;
}) => {
  const target = journey.levels.find((level) => level.state === "Current") ?? journey.levels.at(-1);
  const contextualAction = paymentAction ?? (journey.nextActionCode === "JoinProgramme"
    ? joinAction
    : journey.nextActionCode === "CompleteJoiningPayment"
      ? paymentAction
      : journey.nextActionCode === "InviteMembers" && canInvite
        ? <LinkButton className="w-full" href="/member/invitations">Invite someone</LinkButton>
        : journey.nextActionCode === "ResolveMonthlySubscription"
          ? <LinkButton className="w-full" href="/member/entry-commitments">View monthly payment</LinkButton>
          : undefined);

  return (
    <article aria-labelledby={`${journey.programmeCode}-heading`} className="flex flex-col gap-6">
      <Card className="overflow-hidden p-0">
        <div className="border-b border-border bg-gradient-to-r from-accent/10 via-card to-card p-5 sm:p-7">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-xs font-bold uppercase tracking-[0.22em] text-accent">My programme journey</p>
              <h2 className="mt-2 text-3xl font-bold" id={`${journey.programmeCode}-heading`}>{journey.programmeName}</h2>
              <p className="mt-2 text-muted-foreground">
                {journey.isActive
                  ? journey.qualifiedLevel > 0 ? `Structurally qualified through Level ${journey.qualifiedLevel}.` : "Active and building toward Level 1."
                  : journey.participationStatus}
              </p>
              {journey.decisionReason ? (
                <p className="mt-3 rounded-xl border border-error/30 bg-error/5 p-3 text-sm">
                  <span className="font-bold">Decision reason: </span>{journey.decisionReason}
                </p>
              ) : null}
            </div>
            <Badge tone={journey.isActive ? "success" : journey.hasParticipation ? "warning" : "neutral"}>{journey.participationStatus}</Badge>
          </div>
        </div>
        <div className="p-5 sm:p-7">
          <ActivationJourney headingId={`${journey.programmeCode}-activation-heading`} steps={journey.activationSteps} />
        </div>
      </Card>

      <LevelRail headingId={`${journey.programmeCode}-level-rail-heading`} levels={journey.levels} />
      {journey.isActive && target ? <CurrentTarget action={contextualAction} level={target} /> : null}

      <Card className="border-accent/30 bg-accent/5">
        <p className="text-xs font-bold uppercase tracking-[0.18em] text-accent">Next action</p>
        <div className="mt-2 grid gap-4 sm:grid-cols-[1fr_auto] sm:items-center">
          <div><h3 className="text-xl font-bold">{journey.nextActionTitle}</h3><p className="mt-1 text-sm text-muted-foreground">{journey.nextActionBody}</p></div>
          {!journey.isActive ? contextualAction : null}
        </div>
      </Card>

      <div className="grid gap-5 lg:grid-cols-2">
        <JoiningCard journey={journey} />
        <EarningsCard journey={journey} />
      </div>

      {journey.monthlySubscription ? (
        <Card className="border-warning/30">
          <div className="grid gap-5 sm:grid-cols-[1fr_auto] sm:items-center">
            <div>
              <div className="flex items-center gap-2"><WalletCards className="size-5 text-warning" /><p className="text-xs font-bold uppercase tracking-[0.18em] text-warning">Recurring monthly subscription</p></div>
              <h3 className="mt-2 text-xl font-bold">{formatCurrency(journey.monthlySubscription.monthlyAmount, journey.currency)} per month</h3>
              <p className="mt-1 font-semibold">{journey.monthlySubscription.status}</p>
              <p className="mt-2 text-sm text-muted-foreground">{journey.monthlySubscription.explanation}</p>
            </div>
            <LinkButton href="/member/entry-commitments" variant="outline">View monthly records</LinkButton>
          </div>
        </Card>
      ) : null}

      <CommissionGrowth currency={journey.currency} levels={journey.levels} />

      <Card>
        <div className="flex items-center gap-2"><ShieldCheck className="size-5 text-accent" /><h3 className="text-lg font-bold">Benefits and unlocks</h3></div>
        <div className="mt-4 grid gap-4 md:grid-cols-2">
          {journey.benefits.length > 0
            ? journey.benefits.map((benefit) => <BenefitCard benefit={benefit} key={benefit.code} />)
            : <p className="text-sm text-muted-foreground">No benefit records are available for this programme.</p>}
        </div>
      </Card>
    </article>
  );
};

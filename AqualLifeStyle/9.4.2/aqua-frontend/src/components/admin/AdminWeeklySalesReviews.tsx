"use client";

import { ClipboardCheck, RefreshCw } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import {
  Badge,
  Breadcrumb,
  Button,
  Card,
  DataTable,
  LinkButton,
  Skeleton,
  StatusMessage,
  TextAreaField,
  TextField,
} from "@/src/shared/ui";

type ReviewStatus = 1 | 2 | 3;
type ThresholdResult = 1 | 2;

type WeeklySalesReview = {
  areaId: string | null;
  areaName: string;
  clubMemberNumber: string;
  commissionWeekEndUtc: string;
  commissionWeekStartUtc: string;
  customerName: string;
  decisionId: string | null;
  email: string;
  evidenceReferences: string[];
  participantId: string;
  rejectionReason: string | null;
  reviewStatus: ReviewStatus | null;
  reviewedAt: string | null;
  reviewedByUserId: number | null;
  reviewedFiveLitreQuantity: number | null;
  reviewedOneLitreQuantity: number | null;
  reviewedSprayQuantity: number | null;
  salesEligibilityRulesVersion: string;
  tenantId: number;
  thresholdResult: ThresholdResult | null;
  timeZoneId: string;
};

type PagedReviews = {
  items: WeeklySalesReview[];
  totalCount: number;
};

type DecisionResult = {
  id: string;
};

const REVIEW_PERMISSION =
  "Aqua.Admin.Commissions.ReviewAQGreenWeeklySalesEligibility";

const formatDate = (value: string) =>
  new Intl.DateTimeFormat("en-ZA", {
    day: "numeric",
    month: "short",
    timeZone: "Africa/Johannesburg",
    year: "numeric",
  }).format(new Date(value));

const formatDateTime = (value: string) =>
  new Intl.DateTimeFormat("en-ZA", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "Africa/Johannesburg",
  }).format(new Date(value));

const statusLabel = (review: WeeklySalesReview) => {
  if (review.reviewStatus === null) return "Not started";
  if (review.reviewStatus === 1) return "Held for evidence";
  if (review.reviewStatus === 3) return "Rejected";
  return review.thresholdResult === 1
    ? "Confirmed · Met"
    : "Confirmed · Not met";
};

const statusTone = (review: WeeklySalesReview) => {
  if (review.reviewStatus === 2 && review.thresholdResult === 1) return "success";
  if (review.reviewStatus === 3) return "error";
  if (review.reviewStatus === 1) return "warning";
  return "neutral";
};

const evidenceLines = (value: string) =>
  [...new Set(value
    .split("\n")
    .map((item) => item.trim())
    .filter(Boolean))];

export const AdminWeeklySalesReviews = () => {
  const { session } = useAuthState();
  const canReview =
    session?.user?.permissions?.includes(REVIEW_PERMISSION) ?? false;
  const [reviews, setReviews] = useState<WeeklySalesReview[]>([]);
  const [selected, setSelected] = useState<WeeklySalesReview>();
  const [loading, setLoading] = useState(canReview);
  const [loadingTarget, setLoadingTarget] = useState(false);
  const [action, setAction] = useState<"begin" | "confirm" | "reject">();
  const [error, setError] = useState<string>();
  const [notice, setNotice] = useState<string>();
  const [spray, setSpray] = useState("0");
  const [oneLitre, setOneLitre] = useState("0");
  const [fiveLitre, setFiveLitre] = useState("0");
  const [evidence, setEvidence] = useState("");
  const [rejectionReason, setRejectionReason] = useState("");
  const initialTargetLoaded = useRef(false);

  const loadReviews = useCallback(async () => {
    if (!canReview) {
      setLoading(false);
      return;
    }
    setLoading(true);
    setError(undefined);
    try {
      const result = await httpClient.get<PagedReviews>(
        `${apiEndpoints.weeklySalesReviews.getAll}?MaxResultCount=100`,
      );
      setReviews(result.items);
    } catch (requestError) {
      setError(getRequestErrorMessage(
        requestError,
        "Weekly sales reviews could not be loaded.",
      ));
    } finally {
      setLoading(false);
    }
  }, [canReview]);

  const selectReview = useCallback((review: WeeklySalesReview) => {
    setSelected(review);
    setSpray(String(review.reviewedSprayQuantity ?? 0));
    setOneLitre(String(review.reviewedOneLitreQuantity ?? 0));
    setFiveLitre(String(review.reviewedFiveLitreQuantity ?? 0));
    setEvidence(review.evidenceReferences.join("\n"));
    setRejectionReason(review.rejectionReason ?? "");
    setError(undefined);
    setNotice(undefined);
  }, []);

  const openDecision = useCallback(async (decisionId: string) => {
    const review = await httpClient.get<WeeklySalesReview>(
      `${apiEndpoints.weeklySalesReviews.get}?Id=${encodeURIComponent(decisionId)}`,
    );
    selectReview(review);
    return review;
  }, [selectReview]);

  useEffect(() => {
    const task = window.setTimeout(() => void loadReviews(), 0);
    return () => window.clearTimeout(task);
  }, [loadReviews]);

  useEffect(() => {
    if (!canReview || initialTargetLoaded.current) return;
    initialTargetLoaded.current = true;
    const parameters = new URLSearchParams(window.location.search);
    const tenantId = Number(parameters.get("tenantId"));
    const participantId = parameters.get("participantId");
    if (!Number.isInteger(tenantId) || tenantId <= 0 || !participantId) return;

    let cancelled = false;
    const task = window.setTimeout(() => {
      setLoadingTarget(true);
      void httpClient.get<WeeklySalesReview>(
        `${apiEndpoints.weeklySalesReviews.getLatestClosedWeek}?TenantId=${tenantId}&ParticipantId=${encodeURIComponent(participantId)}`,
      ).then((review) => {
        if (!cancelled) selectReview(review);
      }).catch((requestError) => {
        if (!cancelled) {
          setError(getRequestErrorMessage(
            requestError,
            "The selected Club Member's latest closed review week could not be loaded.",
          ));
        }
      }).finally(() => {
        if (!cancelled) setLoadingTarget(false);
      });
    }, 0);
    return () => {
      cancelled = true;
      window.clearTimeout(task);
    };
  }, [canReview, selectReview]);

  const beginReview = async () => {
    if (!selected) return;
    setAction("begin");
    setError(undefined);
    setNotice(undefined);
    try {
      const result = await httpClient.post<DecisionResult, {
        commissionWeekStartUtc: string;
        participantId: string;
        tenantId: number;
      }>(
        apiEndpoints.weeklySalesReviews.begin,
        {
          commissionWeekStartUtc: selected.commissionWeekStartUtc,
          participantId: selected.participantId,
          tenantId: selected.tenantId,
        },
      );
      await openDecision(result.id);
      setNotice("The latest closed week is now held for evidence review.");
      await loadReviews();
    } catch (requestError) {
      setError(getRequestErrorMessage(
        requestError,
        "The weekly sales review could not be started. No decision was changed.",
      ));
    } finally {
      setAction(undefined);
    }
  };

  const quantities = () => ({
    fiveLitreQuantity: Number(fiveLitre),
    oneLitreQuantity: Number(oneLitre),
    sprayQuantity: Number(spray),
  });

  const inputsAreValid = () =>
    Object.values(quantities()).every((value) =>
      Number.isInteger(value) && value >= 0);

  const confirmSales = async () => {
    if (!selected || !selected.decisionId) return;
    const references = evidenceLines(evidence);
    if (!inputsAreValid()) {
      setError("Verified quantities must be whole numbers of zero or more.");
      return;
    }
    if (references.length === 0) {
      setError("Add at least one evidence reference before confirming sales.");
      return;
    }

    setAction("confirm");
    setError(undefined);
    setNotice(undefined);
    try {
      await httpClient.post(apiEndpoints.weeklySalesReviews.confirm, {
        commissionWeekStartUtc: selected.commissionWeekStartUtc,
        evidenceReferences: references,
        participantId: selected.participantId,
        tenantId: selected.tenantId,
        ...quantities(),
      });
      const finalized = await openDecision(selected.decisionId);
      setNotice(`Sales review finalized as ${statusLabel(finalized)}.`);
      await loadReviews();
    } catch (requestError) {
      setError(getRequestErrorMessage(
        requestError,
        "Sales could not be confirmed. No final decision was recorded.",
      ));
    } finally {
      setAction(undefined);
    }
  };

  const rejectEvidence = async () => {
    if (!selected || !selected.decisionId) return;
    const references = evidenceLines(evidence);
    if (references.length === 0) {
      setError("Add at least one evidence reference before rejecting evidence.");
      return;
    }
    if (rejectionReason.trim().length < 3) {
      setError("Enter a clear rejection reason of at least 3 characters.");
      return;
    }

    setAction("reject");
    setError(undefined);
    setNotice(undefined);
    try {
      await httpClient.post(apiEndpoints.weeklySalesReviews.reject, {
        commissionWeekStartUtc: selected.commissionWeekStartUtc,
        evidenceReferences: references,
        participantId: selected.participantId,
        rejectionReason: rejectionReason.trim(),
        tenantId: selected.tenantId,
      });
      await openDecision(selected.decisionId);
      setNotice("Evidence was rejected and the reason was recorded.");
      await loadReviews();
    } catch (requestError) {
      setError(getRequestErrorMessage(
        requestError,
        "Evidence could not be rejected. No final decision was recorded.",
      ));
    } finally {
      setAction(undefined);
    }
  };

  if (!canReview) {
    return (
      <main className="p-6">
        <StatusMessage tone="error">
          You do not have permission to review AQGreen weekly sales.
        </StatusMessage>
      </main>
    );
  }

  const columns = [
    {
      header: "Club Member",
      key: "customerName",
      render: (item: WeeklySalesReview) => (
        <div>
          <p className="font-semibold">{item.customerName}</p>
          <p className="font-mono text-xs text-muted-foreground">
            {item.clubMemberNumber}
          </p>
        </div>
      ),
      sortable: true,
    },
    {
      header: "Area",
      key: "areaName",
      render: (item: WeeklySalesReview) => item.areaName,
      sortable: true,
    },
    {
      header: "Week",
      key: "commissionWeekStartUtc",
      render: (item: WeeklySalesReview) => (
        <span>{formatDate(item.commissionWeekStartUtc)} – {formatDate(item.commissionWeekEndUtc)}</span>
      ),
      sortable: true,
    },
    {
      header: "Review result",
      key: "reviewStatus",
      render: (item: WeeklySalesReview) => (
        <Badge tone={statusTone(item)}>{statusLabel(item)}</Badge>
      ),
      sortable: true,
    },
    {
      header: "Action",
      key: "action",
      render: (item: WeeklySalesReview) => (
        <Button onClick={() => selectReview(item)} size="sm" variant="outline">
          {item.reviewStatus === 1 ? "Review" : "View"}
        </Button>
      ),
    },
  ];

  const selectedIsHeld = selected?.reviewStatus === 1;
  const selectedIsFinal = selected?.reviewStatus === 2 || selected?.reviewStatus === 3;

  return (
    <main className="min-h-dvh px-4 py-6 sm:px-6 lg:px-8">
      <div className="mx-auto flex max-w-7xl flex-col gap-6">
        <header className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <Breadcrumb items={[
              { href: "/admin/dashboard", label: "Administration" },
              { label: "AQGreen weekly sales reviews" },
            ]} />
            <h1 className="mt-2 text-3xl font-bold">AQGreen weekly sales reviews</h1>
            <p className="mt-2 max-w-3xl text-muted-foreground">
              Review evidence and verified quantities for a closed Friday-to-Thursday
              week. The system—not the administrator—computes whether the 5/5/5
              threshold was met.
            </p>
          </div>
          <Button onClick={() => void loadReviews()} variant="outline">
            <RefreshCw className="size-4" /> Refresh queue
          </Button>
        </header>

        {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
        {notice ? <StatusMessage tone="success">{notice}</StatusMessage> : null}

        {loadingTarget ? <Skeleton className="h-64" /> : selected ? (
          <Card className="flex flex-col gap-5 border-accent/30">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div>
                <p className="text-xs font-bold uppercase tracking-[0.18em] text-accent">
                  Selected review
                </p>
                <h2 className="mt-1 text-xl font-bold">{selected.customerName}</h2>
                <p className="text-sm text-muted-foreground">
                  {selected.clubMemberNumber} · {selected.areaName} · {selected.email}
                </p>
              </div>
              <Badge tone={statusTone(selected)}>{statusLabel(selected)}</Badge>
            </div>

            <dl className="grid gap-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
              <div><dt className="text-muted-foreground">Canonical week</dt><dd className="mt-1 font-semibold">{formatDate(selected.commissionWeekStartUtc)} – {formatDate(selected.commissionWeekEndUtc)}</dd></div>
              <div><dt className="text-muted-foreground">Time zone</dt><dd className="mt-1 font-semibold">{selected.timeZoneId}</dd></div>
              <div><dt className="text-muted-foreground">Rules</dt><dd className="mt-1 font-semibold">{selected.salesEligibilityRulesVersion}</dd></div>
              <div><dt className="text-muted-foreground">Reviewed</dt><dd className="mt-1 font-semibold">{selected.reviewedAt ? `${formatDateTime(selected.reviewedAt)} by user #${selected.reviewedByUserId}` : "Not finalized"}</dd></div>
            </dl>

            {selected.reviewStatus === null ? (
              <div className="rounded-xl border border-warning/30 bg-warning/5 p-4">
                <p className="font-semibold">No review decision exists for this latest closed week.</p>
                <p className="mt-1 text-sm text-muted-foreground">
                  Start review to create the existing Held for evidence state.
                  Production remains subject to the backend review-write gate.
                </p>
                <Button className="mt-4" isLoading={action === "begin"} onClick={() => void beginReview()}>
                  Start weekly sales review
                </Button>
              </div>
            ) : null}

            {selectedIsHeld ? (
              <>
                <div className="grid gap-4 sm:grid-cols-3">
                  <TextField label="Spray verified quantity" min={0} name="sprayQuantity" onChange={(event) => setSpray(event.target.value)} required type="number" value={spray} />
                  <TextField label="1L verified quantity" min={0} name="oneLitreQuantity" onChange={(event) => setOneLitre(event.target.value)} required type="number" value={oneLitre} />
                  <TextField label="5L verified quantity" min={0} name="fiveLitreQuantity" onChange={(event) => setFiveLitre(event.target.value)} required type="number" value={fiveLitre} />
                </div>
                <TextAreaField
                  label="Evidence references (one per line)"
                  maxLength={1028}
                  name="evidenceReferences"
                  onChange={(event) => setEvidence(event.target.value)}
                  placeholder="ticket:weekly-sales-2026-09-01"
                  required
                  rows={3}
                  value={evidence}
                />
                <div className="flex flex-wrap gap-3">
                  <Button isLoading={action === "confirm"} onClick={() => void confirmSales()}>
                    Confirm sales
                  </Button>
                </div>
                <div className="border-t border-border pt-5">
                  <TextAreaField
                    label="Rejection reason"
                    maxLength={1000}
                    name="rejectionReason"
                    onChange={(event) => setRejectionReason(event.target.value)}
                    rows={3}
                    value={rejectionReason}
                  />
                  <Button className="mt-3" isLoading={action === "reject"} onClick={() => void rejectEvidence()} variant="outline">
                    Reject evidence
                  </Button>
                </div>
              </>
            ) : null}

            {selectedIsFinal ? (
              <div className="grid gap-5 lg:grid-cols-2">
                <div>
                  <h3 className="font-semibold">Evidence references</h3>
                  <ul className="mt-2 list-disc space-y-1 pl-5 text-sm">
                    {selected.evidenceReferences.map((reference) => <li className="break-all" key={reference}>{reference}</li>)}
                  </ul>
                </div>
                <dl className="grid grid-cols-2 gap-4 text-sm">
                  <div><dt className="text-muted-foreground">Spray</dt><dd className="mt-1 font-bold">{selected.reviewedSprayQuantity ?? "—"}</dd></div>
                  <div><dt className="text-muted-foreground">1L</dt><dd className="mt-1 font-bold">{selected.reviewedOneLitreQuantity ?? "—"}</dd></div>
                  <div><dt className="text-muted-foreground">5L</dt><dd className="mt-1 font-bold">{selected.reviewedFiveLitreQuantity ?? "—"}</dd></div>
                  <div><dt className="text-muted-foreground">System result</dt><dd className="mt-1 font-bold">{selected.thresholdResult === 1 ? "Met" : selected.thresholdResult === 2 ? "Not met" : "Not applicable"}</dd></div>
                </dl>
                {selected.rejectionReason ? (
                  <div className="rounded-xl border border-error/30 bg-error/5 p-4 text-sm lg:col-span-2">
                    <p className="font-semibold">Rejection reason</p>
                    <p className="mt-1 text-muted-foreground">{selected.rejectionReason}</p>
                  </div>
                ) : null}
                <p className="text-xs text-muted-foreground lg:col-span-2">
                  Finalized weekly-sales decisions are immutable. Commission amounts and payout state are not editable here.
                </p>
              </div>
            ) : null}
          </Card>
        ) : (
          <Card className="flex flex-wrap items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <ClipboardCheck className="size-6 text-accent" />
              <div>
                <p className="font-semibold">Open an active AQGreen participation to start its latest closed week.</p>
                <p className="text-sm text-muted-foreground">Existing held and finalized decisions remain visible in the queue below.</p>
              </div>
            </div>
            <LinkButton href="/admin/programme-participations" variant="outline">Find an AQGreen participant</LinkButton>
          </Card>
        )}

        <section>
          <h2 className="text-xl font-bold">Review queue and history</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Held cases require action. Confirmed and rejected cases are read-only history.
          </p>
          <div className="mt-4">
            {loading ? <Skeleton className="h-72" /> : (
              <DataTable
                columns={columns}
                data={reviews}
                emptyState="No weekly sales review decisions have been recorded yet."
                keyExtractor={(item) => item.decisionId ?? `${item.tenantId}:${item.participantId}:${item.commissionWeekStartUtc}`}
                searchFn={(item, query) => `${item.customerName} ${item.clubMemberNumber} ${item.areaName} ${statusLabel(item)}`.toLowerCase().includes(query.toLowerCase())}
              />
            )}
          </div>
        </section>
      </div>
    </main>
  );
};

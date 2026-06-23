"use client";

import { ArrowRight, UserCheck } from "lucide-react";

import { Avatar, Button, Card, EmptyState, LinkButton } from "@/src/shared/ui";

export type PendingFacilitator = {
  customerName: string;
  directReferrals: number;
  id: number;
};

type FacilitatorApprovalProps = {
  facilitators: PendingFacilitator[];
  isApproving?: boolean;
  onApprove: (id: number) => void;
};

export const FacilitatorApproval = ({
  facilitators,
  isApproving,
  onApprove,
}: FacilitatorApprovalProps) => (
  <Card className="flex h-full flex-col">
    <div className="flex items-start justify-between gap-4">
      <div>
        <h2 className="text-lg font-semibold">Facilitator approvals</h2>
        <p className="text-sm text-muted-foreground">
          {facilitators.length} application{facilitators.length === 1 ? "" : "s"} awaiting review.
        </p>
      </div>
      <LinkButton href="/area-leader/facilitators" size="sm" variant="ghost">
        View all <ArrowRight className="size-4" />
      </LinkButton>
    </div>

    {facilitators.length === 0 ? (
      <EmptyState className="mt-4" description="All applications have been reviewed." icon={UserCheck} title="No pending approvals" />
    ) : (
      <ul className="mt-4 divide-y divide-border">
        {facilitators.slice(0, 5).map((facilitator) => (
          <li className="flex items-center justify-between gap-3 py-4 first:pt-0" key={facilitator.id}>
            <div className="flex min-w-0 items-center gap-3">
              <Avatar fallback={facilitator.customerName} size="sm" />
              <div className="min-w-0">
                <p className="truncate font-semibold">{facilitator.customerName}</p>
                <p className="text-xs text-muted-foreground">{facilitator.directReferrals} referrals</p>
              </div>
            </div>
            <Button disabled={isApproving} onClick={() => onApprove(facilitator.id)} size="sm">
              Approve
            </Button>
          </li>
        ))}
      </ul>
    )}
  </Card>
);

"use client";

import type { Enquiry, EnquiryStatus } from "@/src/providers";
import { formatDate, formatPercent } from "@/src/shared/format";
import { Badge, Card, LinkButton } from "@/src/shared/ui";

const enquiryStatusLabels: Record<EnquiryStatus, string> = {
  0: "Pending",
  1: "Responded",
  2: "Closed",
};

type EnquiryCardProps = {
  customerName: string;
  enquiry: Enquiry;
  productName: string;
};

export const EnquiryCard = ({
  customerName,
  enquiry,
  productName,
}: EnquiryCardProps) => {
  return (
    <Card>
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <h2 className="truncate text-lg font-semibold text-zinc-950">
            {customerName}
          </h2>
          <p className="mt-1 text-sm text-zinc-600">{productName}</p>
        </div>
        <Badge
          tone={
            enquiry.isConverted || !enquiry.isClosed ? "success" : "neutral"
          }
        >
          {enquiry.isConverted
            ? "Converted"
            : enquiryStatusLabels[enquiry.status]}
        </Badge>
      </div>

      <p className="mt-5 line-clamp-3 text-sm leading-6 text-zinc-700">
        {enquiry.message}
      </p>

      <dl className="mt-6 grid gap-3 text-sm">
        <div className="flex justify-between gap-4">
          <dt className="text-zinc-600">Created</dt>
          <dd className="font-medium text-zinc-950">
            {formatDate(enquiry.createdAt, { withTime: true })}
          </dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-zinc-600">Follow-ups</dt>
          <dd className="font-medium text-zinc-950">{enquiry.followUpCount}</dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-zinc-600">Probability</dt>
          <dd className="font-medium text-zinc-950">
            {formatPercent(enquiry.conversionProbability)}
          </dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-zinc-600">Sales ready</dt>
          <dd className="font-medium text-zinc-950">
            {enquiry.isSalesReady ? "Yes" : "No"}
          </dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-zinc-600">Converted</dt>
          <dd className="font-medium text-zinc-950">
            {enquiry.isConverted ? "Yes" : "No"}
          </dd>
        </div>
      </dl>

      <div className="mt-6">
        <LinkButton href={`/enquiries/${enquiry.id}`}>Open enquiry</LinkButton>
      </div>
    </Card>
  );
};

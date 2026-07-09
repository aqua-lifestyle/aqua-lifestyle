"use client";

import type { Enquiry, EnquiryStatus } from "@/src/providers";
import { Badge, Card, LinkButton } from "@/src/shared/ui";

const enquiryStatusLabels: Record<EnquiryStatus, string> = {
  0: "Pending",
  1: "Responded",
  2: "Closed",
};

const formatDate = (date: string) => {
  return new Intl.DateTimeFormat("en-ZA", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(date));
};

const formatPercent = (value: number) => {
  return new Intl.NumberFormat("en-ZA", {
    maximumFractionDigits: 0,
    style: "percent",
  }).format(value / 100);
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
          <h2 className="truncate text-lg font-semibold text-foreground">
            {customerName}
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">{productName}</p>
        </div>
        <Badge
          tone={enquiry.isConverted ? "success" : enquiry.isClosed ? "neutral" : "info"}
        >
          {enquiry.isConverted
            ? "Converted"
            : enquiryStatusLabels[enquiry.status]}
        </Badge>
      </div>

      <p className="mt-5 line-clamp-3 text-sm leading-6 text-foreground">
        {enquiry.message}
      </p>

      <dl className="mt-6 grid gap-3 text-sm">
        <div className="flex justify-between gap-4">
          <dt className="text-muted-foreground">Created</dt>
          <dd className="font-medium text-foreground">{formatDate(enquiry.createdAt)}</dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-muted-foreground">Follow-ups</dt>
          <dd className="font-medium text-foreground">{enquiry.followUpCount}</dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-muted-foreground">Probability</dt>
          <dd className="font-medium text-foreground">{formatPercent(enquiry.conversionProbability)}</dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-muted-foreground">Sales ready</dt>
          <dd className="font-medium text-foreground">{enquiry.isSalesReady ? "Yes" : "No"}</dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-muted-foreground">Converted</dt>
          <dd className="font-medium text-foreground">{enquiry.isConverted ? "Yes" : "No"}</dd>
        </div>
      </dl>

      <div className="mt-6">
        <LinkButton href={`/enquiries/${enquiry.id}`}>Open enquiry</LinkButton>
      </div>
    </Card>
  );
};

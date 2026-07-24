import { StatusMessage } from "@/src/shared/ui";

export const TruncatedResultsWarning = ({
  loadedCount,
  totalCount,
}: {
  loadedCount: number;
  totalCount: number;
}) =>
  totalCount > loadedCount ? (
    <StatusMessage tone="warning">
      Showing {loadedCount} of {totalCount} records. Refine your search to
      review records that are not currently shown.
    </StatusMessage>
  ) : null;

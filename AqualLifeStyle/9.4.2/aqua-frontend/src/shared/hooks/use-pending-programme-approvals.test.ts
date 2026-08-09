import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { apiEndpoints, httpClient } from "@/src/shared/api";
import {
  PROGRAMME_APPROVAL_QUEUE_CHANGED,
  usePendingProgrammeApprovals,
} from "./use-pending-programme-approvals";

vi.mock("@/src/shared/api", async () => {
  const actual = await vi.importActual<typeof import("@/src/shared/api")>(
    "@/src/shared/api",
  );
  return { ...actual, httpClient: { get: vi.fn() } };
});

describe("usePendingProgrammeApprovals", () => {
  beforeEach(() => vi.clearAllMocks());

  it("loads the Area-scoped pending approval summary and refreshes after a decision", async () => {
    vi.mocked(httpClient.get)
      .mockResolvedValueOnce({ aqGreenCount: 2, onyxCount: 1, totalCount: 3 })
      .mockResolvedValueOnce({ aqGreenCount: 1, onyxCount: 1, totalCount: 2 });

    const { result } = renderHook(() => usePendingProgrammeApprovals(true));

    await waitFor(() => expect(result.current.summary?.totalCount).toBe(3));
    expect(httpClient.get).toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.getPendingApprovalSummary,
    );

    act(() => window.dispatchEvent(new Event(PROGRAMME_APPROVAL_QUEUE_CHANGED)));

    await waitFor(() => expect(result.current.summary?.totalCount).toBe(2));
  });

  it("does not query or expose a count without view permission", async () => {
    const { result } = renderHook(() => usePendingProgrammeApprovals(false));

    await waitFor(() => expect(result.current.summary).toBeUndefined());
    expect(httpClient.get).not.toHaveBeenCalled();
  });
});

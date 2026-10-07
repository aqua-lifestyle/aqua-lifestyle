import { renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { httpClient } from "@/src/shared/api";
import { createProgrammeJourney } from "@/src/components/members/programme-journey-test-data";
import { useMyProgrammeJourney } from "./use-my-programme-journey";

vi.mock("@/src/shared/api", async () => ({
  ...await vi.importActual<typeof import("@/src/shared/api")>("@/src/shared/api"),
  httpClient: { get: vi.fn() },
}));

describe("pilot journey loading", () => {
  beforeEach(() => vi.clearAllMocks());

  it("loads the explicit AQGreen pilot response and finishes loading", async () => {
    vi.mocked(httpClient.get).mockResolvedValue({
      projectedAt: "2026-10-06T10:00:00Z",
      programmes: [createProgrammeJourney("AQGREEN", { benefits: [], monthlySubscription: null })],
    });
    const { result } = renderHook(() => useMyProgrammeJourney(true, true));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.data?.programmes).toHaveLength(1);
    expect(result.current.errorMessage).toBeUndefined();
  });

  it("exposes an incompatible response as an error rather than a loading or success state", async () => {
    vi.mocked(httpClient.get).mockResolvedValue({
      projectedAt: "2026-10-06T10:00:00Z",
      programmes: [createProgrammeJourney("ONYX")],
    });
    const { result } = renderHook(() => useMyProgrammeJourney(true, true));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.data).toBeUndefined();
    expect(result.current.errorMessage).toBeTruthy();
  });
});

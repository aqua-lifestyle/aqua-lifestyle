import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { httpClient } from "@/src/shared/api";
import type { MyProgrammeProgress } from "@/src/shared/domain/programme-progress";

import { useMyProgrammeProgress } from "./use-my-programme-progress";

vi.mock("@/src/shared/api", async () => {
  const actual = await vi.importActual<typeof import("@/src/shared/api")>(
    "@/src/shared/api",
  );
  return { ...actual, httpClient: { get: vi.fn() } };
});

const createDeferred = <T,>() => {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });
  return { promise, reject, resolve };
};

const createProgress = (qualifiedLevelLabel: string): MyProgrammeProgress => ({
  currency: "ZAR",
  directRecruits: 5,
  directRecruitsRequired: 5,
  earnedAwaitingRelease: 150,
  education: [],
  funeralCoverBenefitAmount: 30000,
  funeralCoverIncluded: true,
  hasEntryParticipation: true,
  monthlyObligationAmount: null,
  monthlyObligationDueAt: null,
  monthlyObligationOutstanding: null,
  monthlyObligationStatus: null,
  nextAction: null,
  nextActionAmount: null,
  nextLevelLabel: "Level 2",
  onHold: 0,
  paid: 0,
  qualifiedLevel: 1,
  qualifiedLevelLabel,
  recentEarnings: [],
  recruitsRemaining: 0,
  recruitmentProgressPercent: 100,
  releasedAwaitingPayment: 0,
  totalEarned: 150,
});

describe("useMyProgrammeProgress", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("keeps the newest response when requests complete out of order", async () => {
    const first = createDeferred<MyProgrammeProgress>();
    const second = createDeferred<MyProgrammeProgress>();
    vi.mocked(httpClient.get)
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise);
    const { result } = renderHook(() => useMyProgrammeProgress(true));
    await waitFor(() => expect(httpClient.get).toHaveBeenCalledOnce());

    act(() => {
      void result.current.reload();
    });
    await waitFor(() => expect(httpClient.get).toHaveBeenCalledTimes(2));

    await act(async () => {
      second.resolve(createProgress("Level 2"));
      await second.promise;
    });
    expect(result.current.data?.qualifiedLevelLabel).toBe("Level 2");

    await act(async () => {
      first.resolve(createProgress("Level 1"));
      await first.promise;
    });
    expect(result.current.data?.qualifiedLevelLabel).toBe("Level 2");
  });

  it("ignores an in-flight response after access is disabled", async () => {
    const request = createDeferred<MyProgrammeProgress>();
    vi.mocked(httpClient.get).mockReturnValueOnce(request.promise);
    const { rerender, result } = renderHook(
      ({ enabled }) => useMyProgrammeProgress(enabled),
      { initialProps: { enabled: true } },
    );
    await waitFor(() => expect(httpClient.get).toHaveBeenCalledOnce());

    rerender({ enabled: false });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.data).toBeUndefined();

    await act(async () => {
      request.resolve(createProgress("Level 1"));
      await request.promise;
    });
    expect(result.current.data).toBeUndefined();
    expect(result.current.errorMessage).toBeUndefined();
  });
});

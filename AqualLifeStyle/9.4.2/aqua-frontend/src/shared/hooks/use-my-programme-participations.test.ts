import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { httpClient } from "@/src/shared/api";
import type { MyProgrammeParticipations } from "@/src/shared/domain/programme-participations";

import { useMyProgrammeParticipations } from "./use-my-programme-participations";

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

const createParticipations = (clubMemberNumber: string): MyProgrammeParticipations => ({
  canJoinEntry: true,
  canJoinOnyxDirectly: true,
  clubMemberNumber,
  entry: null,
  onyx: null,
  pendingAQGreenCheckout: null,
  pendingDirectOnyxCheckout: null,
  travelBenefit: null,
});

describe("useMyProgrammeParticipations", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("keeps the newest response when requests complete out of order", async () => {
    const first = createDeferred<MyProgrammeParticipations>();
    const second = createDeferred<MyProgrammeParticipations>();
    vi.mocked(httpClient.get)
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise);
    const { result } = renderHook(() => useMyProgrammeParticipations(true));
    await waitFor(() => expect(httpClient.get).toHaveBeenCalledOnce());

    act(() => {
      void result.current.reload();
    });
    await waitFor(() => expect(httpClient.get).toHaveBeenCalledTimes(2));

    await act(async () => {
      second.resolve(createParticipations("CLB-NEWEST"));
      await second.promise;
    });
    expect(result.current.data?.clubMemberNumber).toBe("CLB-NEWEST");

    await act(async () => {
      first.resolve(createParticipations("CLB-STALE"));
      await first.promise;
    });
    expect(result.current.data?.clubMemberNumber).toBe("CLB-NEWEST");
  });

  it("ignores an in-flight response after access is disabled", async () => {
    const request = createDeferred<MyProgrammeParticipations>();
    vi.mocked(httpClient.get).mockReturnValueOnce(request.promise);
    const { rerender, result } = renderHook(
      ({ enabled }) => useMyProgrammeParticipations(enabled),
      { initialProps: { enabled: true } },
    );
    await waitFor(() => expect(httpClient.get).toHaveBeenCalledOnce());

    rerender({ enabled: false });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.data).toBeUndefined();

    await act(async () => {
      request.resolve(createParticipations("CLB-STALE"));
      await request.promise;
    });
    expect(result.current.data).toBeUndefined();
    expect(result.current.errorMessage).toBeUndefined();
  });
});

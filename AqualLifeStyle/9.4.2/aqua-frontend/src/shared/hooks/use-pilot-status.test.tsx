import { renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { httpClient } from "@/src/shared/api";
import { usePilotStatus } from "./use-pilot-status";
vi.mock("@/src/shared/api", () => ({ httpClient: { get: vi.fn() } }));
describe("pilot mode discovery", () => {
  beforeEach(() => vi.clearAllMocks());
  it.each([{ isPilot: true, testMoneyOnly: true }, { isPilot: false, testMoneyOnly: false }])(
    "requires an explicit consistent server result %j", async (value) => {
      vi.mocked(httpClient.get).mockResolvedValue(value);
      const { result } = renderHook(usePilotStatus);
      await waitFor(() => expect(result.current).toEqual({ isPilot: value.isPilot, modeKnown: true }));
    },
  );
  it.each([{}, { isPilot: true }, { isPilot: false, testMoneyOnly: true }, { isPilot: "true", testMoneyOnly: true }])(
    "does not enable money actions from ambiguous mode %j", async (value) => {
      vi.mocked(httpClient.get).mockResolvedValue(value);
      const { result } = renderHook(usePilotStatus);
      await waitFor(() => expect(httpClient.get).toHaveBeenCalled());
      expect(result.current.modeKnown).toBe(false);
    },
  );
  it("keeps mode unknown when the server cannot establish it", async () => {
    vi.mocked(httpClient.get).mockRejectedValue(new Error("Unavailable"));
    const { result } = renderHook(usePilotStatus);
    await waitFor(() => expect(httpClient.get).toHaveBeenCalled());
    expect(result.current.modeKnown).toBe(false);
  });
});

import { renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "@/src/shared/api";
import { usePilotStatus } from "./use-pilot-status";
vi.mock("@/src/shared/api", () => ({ apiClient: { get: vi.fn() } }));
describe("deployment mode discovery", () => {
  beforeEach(() => vi.clearAllMocks());
  it.each([{ isPilot: true, testMoneyOnly: true }, { isPilot: false, testMoneyOnly: false }])(
    "requires an explicit consistent server result %j", async (value) => {
      vi.mocked(apiClient.get).mockResolvedValue({ data: value, status: 200, headers: {} });
      const { result } = renderHook(usePilotStatus);
      await waitFor(() => expect(result.current).toEqual({ isPilot: value.isPilot, modeKnown: true, isRecovery: false }));
    },
  );
  it.each([{}, { isPilot: true }, { isPilot: false, testMoneyOnly: true }, { isPilot: "true", testMoneyOnly: true }])(
    "does not enable money actions from ambiguous mode %j", async (value) => {
      vi.mocked(apiClient.get).mockResolvedValue({ data: value, status: 200, headers: {} });
      const { result } = renderHook(usePilotStatus);
      await waitFor(() => expect(apiClient.get).toHaveBeenCalled());
      expect(result.current.modeKnown).toBe(false);
      expect(result.current.isRecovery).toBe(false);
    },
  );
  it("recognizes recovery only from the explicit backend suspension response", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ status: 503, headers: { "x-aqua-recovery-suspended": "true" }, data: { error: "APPLICATION_RECOVERY_SUSPENDED" } });
    const { result } = renderHook(usePilotStatus);
    await waitFor(() => expect(result.current.isRecovery).toBe(true));
    expect(result.current.modeKnown).toBe(false);
    expect(result.current.isPilot).toBe(false);
    const options = vi.mocked(apiClient.get).mock.calls[0][1];
    expect(options?.validateStatus?.(503)).toBe(true);
    expect(options?.validateStatus?.(401)).toBe(false);
  });
  it.each([{}, { "x-aqua-recovery-suspended": "false" }, { "x-aqua-recovery-suspended": "TRUE" }])(
    "does not treat an unverified 503 as recovery %j", async (headers) => {
      vi.mocked(apiClient.get).mockResolvedValue({ status: 503, headers, data: { error: "APPLICATION_RECOVERY_SUSPENDED" } });
      const { result } = renderHook(usePilotStatus);
      await waitFor(() => expect(apiClient.get).toHaveBeenCalled());
      expect(result.current.isRecovery).toBe(false);
      expect(result.current.modeKnown).toBe(false);
    },
  );
  it("keeps mode unknown when the server cannot establish it", async () => {
    vi.mocked(apiClient.get).mockRejectedValue(new Error("Unavailable"));
    const { result } = renderHook(usePilotStatus);
    await waitFor(() => expect(apiClient.get).toHaveBeenCalled());
    expect(result.current.modeKnown).toBe(false);
    expect(result.current.isRecovery).toBe(false);
  });
});

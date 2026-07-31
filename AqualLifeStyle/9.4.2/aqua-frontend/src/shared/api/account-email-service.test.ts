import axios from "axios";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  completePasswordReset,
  confirmEmail,
  requestPasswordReset,
} from "./account-email-service";

vi.mock("axios", () => ({
  default: { post: vi.fn() },
}));

describe("account-email-service", () => {
  beforeEach(() => vi.clearAllMocks());

  it.each([
    false,
    {
      __abp: true,
      error: null,
      result: false,
      success: true,
      targetUrl: null,
      unAuthorizedRequest: false,
    },
  ])("treats a false HTTP 200 result as a failed confirmation", async (data) => {
    vi.mocked(axios.post).mockResolvedValue({ data });

    const result = await confirmEmail(1, 42, "token");

    expect(result.ok).toBe(false);
  });

  it("accepts a true ABP result", async () => {
    vi.mocked(axios.post).mockResolvedValue({
      data: {
        __abp: true,
        error: null,
        result: true,
        success: true,
        targetUrl: null,
        unAuthorizedRequest: false,
      },
    });

    await expect(completePasswordReset(1, 42, "token", "CustomerChosen123!"))
      .resolves.toEqual({ ok: true });
  });

  it("accepts the generic password-reset request response", async () => {
    vi.mocked(axios.post).mockResolvedValue({
      data: {
        __abp: true,
        error: null,
        result: { message: "If an eligible account exists, an email will be sent." },
        success: true,
        targetUrl: null,
        unAuthorizedRequest: false,
      },
    });

    await expect(requestPasswordReset("Default", "member@example.test", "/profile"))
      .resolves.toEqual({ ok: true });
  });
});

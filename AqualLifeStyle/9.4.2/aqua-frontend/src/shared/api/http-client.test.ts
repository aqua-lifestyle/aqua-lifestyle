import { beforeEach, describe, expect, it, vi } from "vitest";

const { apiClient } = vi.hoisted(() => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock("./axios-instance", () => ({ apiClient }));

import { AbpHttpError } from "./abp-error";
import { httpClient } from "./http-client";

describe("httpClient", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("returns the raw response body for plain payloads", async () => {
    apiClient.get.mockResolvedValue({ data: { id: 1, name: "Ada" } });

    const result = await httpClient.get<{ id: number; name: string }>("/url");

    expect(apiClient.get).toHaveBeenCalledWith("/url");
    expect(result).toEqual({ id: 1, name: "Ada" });
  });

  it("unwraps successful ABP response envelopes", async () => {
    apiClient.post.mockResolvedValue({
      data: {
        __abp: true,
        success: true,
        result: { created: true },
        targetUrl: null,
        error: null,
        unAuthorizedRequest: false,
      },
    });

    const result = await httpClient.post<{ created: boolean }, { name: string }>(
      "/url",
      { name: "Ada" },
    );

    expect(apiClient.post).toHaveBeenCalledWith("/url", { name: "Ada" });
    expect(result).toEqual({ created: true });
  });

  it("throws an AbpHttpError for failed ABP envelopes", async () => {
    apiClient.put.mockResolvedValue({
      data: {
        __abp: true,
        success: false,
        result: null,
        targetUrl: null,
        error: { message: "Validation failed." },
        unAuthorizedRequest: false,
      },
    });

    await expect(httpClient.put("/url", {})).rejects.toBeInstanceOf(
      AbpHttpError,
    );
    await expect(httpClient.put("/url", {})).rejects.toThrow(
      "Validation failed.",
    );
  });

  it("forwards delete requests and unwraps the result", async () => {
    apiClient.delete.mockResolvedValue({ data: "ok" });

    const result = await httpClient.delete<string>("/url");

    expect(apiClient.delete).toHaveBeenCalledWith("/url");
    expect(result).toBe("ok");
  });
});

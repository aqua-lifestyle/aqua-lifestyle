import { beforeEach, describe, expect, it, vi } from "vitest";

import { apiClient } from "./axios-instance";
import { httpClient } from "./http-client";

vi.mock("./axios-instance", () => ({
  apiClient: {
    delete: vi.fn(),
  },
}));

describe("httpClient.delete", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("sends ABP delete inputs as query parameters", async () => {
    vi.mocked(apiClient.delete).mockResolvedValue({ data: null });
    const input = { id: 42, justification: "The customer requested account removal." };

    await httpClient.delete("/api/services/app/AdminCustomer/Delete", input);

    expect(apiClient.delete).toHaveBeenCalledWith(
      "/api/services/app/AdminCustomer/Delete",
      { params: input },
    );
  });
});

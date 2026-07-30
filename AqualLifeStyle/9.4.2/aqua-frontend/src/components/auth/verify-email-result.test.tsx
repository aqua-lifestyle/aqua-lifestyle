import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { confirmEmail } from "@/src/shared/api/account-email-service";

import { VerifyEmailResult } from "./verify-email-result";

vi.mock("@/src/shared/api/account-email-service", () => ({
  confirmEmail: vi.fn(),
}));

describe("VerifyEmailResult", () => {
  beforeEach(() => vi.clearAllMocks());

  it("confirms a valid verification link and offers sign in", async () => {
    vi.mocked(confirmEmail).mockResolvedValue({ ok: true });

    render(<VerifyEmailResult tenantId={1} token="token" userId={42} />);

    expect(confirmEmail).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "Verify email" }));
    expect(await screen.findByText("Your email is verified. You can now sign in.")).toBeInTheDocument();
    expect(confirmEmail).toHaveBeenCalledWith(1, 42, "token");
    expect(screen.getByRole("link", { name: "Continue to sign in" })).toHaveAttribute("href", "/login");
  });

  it("fails safely without calling the backend for a malformed link", () => {
    render(<VerifyEmailResult tenantId={0} token="" userId={0} />);

    expect(screen.getByText(/invalid or has expired/)).toBeInTheDocument();
    expect(confirmEmail).not.toHaveBeenCalled();
  });

  it("preserves a safe invitation return path after verification", async () => {
    vi.mocked(confirmEmail).mockResolvedValue({ ok: true });

    render(<VerifyEmailResult
      areaName="Johannesburg"
      redirectPath="/i/AQ7G2X9K"
      tenantId={1}
      token="token"
      userId={42}
    />);

    fireEvent.click(screen.getByRole("button", { name: "Verify email" }));
    expect(await screen.findByRole("link", { name: "Continue to sign in" })).toHaveAttribute(
      "href",
      "/login?area=Johannesburg&redirect=%2Fi%2FAQ7G2X9K",
    );
  });
});

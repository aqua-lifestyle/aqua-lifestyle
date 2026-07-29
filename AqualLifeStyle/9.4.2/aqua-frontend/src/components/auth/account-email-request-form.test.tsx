import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { requestPasswordReset, resendEmailVerification } from "@/src/shared/api/account-email-service";

import { AccountEmailRequestForm } from "./account-email-request-form";

vi.mock("@/src/shared/api/account-email-service", () => ({
  requestPasswordReset: vi.fn(),
  resendEmailVerification: vi.fn(),
}));

describe("AccountEmailRequestForm", () => {
  beforeEach(() => vi.clearAllMocks());

  it("requests another verification email without exposing account existence", async () => {
    vi.mocked(resendEmailVerification).mockResolvedValue({ ok: true });
    render(<AccountEmailRequestForm areaName="Default" initialEmail="member@example.test" purpose="verification" />);

    fireEvent.click(screen.getByRole("button", { name: "Send verification email" }));

    await waitFor(() => expect(resendEmailVerification).toHaveBeenCalledWith("Default", "member@example.test", undefined));
    expect(screen.getByText(/If the account is eligible/)).toBeInTheDocument();
    expect(requestPasswordReset).not.toHaveBeenCalled();
  });

  it("routes forgot-password requests to the password-reset endpoint", async () => {
    vi.mocked(requestPasswordReset).mockResolvedValue({ ok: true });
    render(<AccountEmailRequestForm areaName="Johannesburg" purpose="password-reset" redirectPath="/profile" />);

    fireEvent.change(screen.getByLabelText("Email address"), { target: { value: "member@example.test" } });
    fireEvent.click(screen.getByRole("button", { name: "Send reset instructions" }));

    await waitFor(() => expect(requestPasswordReset).toHaveBeenCalledWith("Johannesburg", "member@example.test", "/profile"));
    expect(screen.getByText(/If the account is eligible/)).toBeInTheDocument();
    expect(resendEmailVerification).not.toHaveBeenCalled();
  });

  it("validates the email before making a request", async () => {
    render(<AccountEmailRequestForm areaName="Default" purpose="password-reset" />);

    const email = screen.getByLabelText("Email address");
    fireEvent.change(email, { target: { value: "not-an-email" } });
    fireEvent.submit(email.closest("form")!);

    expect(await screen.findByText("Enter a valid email address.")).toBeInTheDocument();
    expect(requestPasswordReset).not.toHaveBeenCalled();
  });
});

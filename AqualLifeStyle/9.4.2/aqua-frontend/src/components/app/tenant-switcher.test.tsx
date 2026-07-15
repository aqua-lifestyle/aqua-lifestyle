import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { TenantProvider, ToastProvider } from "@/src/providers";

import { TenantSwitcher } from "./tenant-switcher";

describe("TenantSwitcher", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    window.localStorage.clear();
  });

  afterEach(() => {
    vi.useRealTimers();
    window.localStorage.clear();
  });

  const renderWithProviders = () =>
    render(
      <ToastProvider>
        <TenantProvider>
          <TenantSwitcher />
        </TenantProvider>
      </ToastProvider>,
    );

  const openSwitcher = () => {
    fireEvent.click(
      screen.getByRole("button", { name: /Host mode|tenant/i }),
    );
  };

  it("renders the current tenant label", () => {
    window.localStorage.setItem("aqua.currentTenant", "tenant-a");

    renderWithProviders();

    expect(screen.getByText("tenant-a")).toBeInTheDocument();
  });

  it("opens the switcher and validates tenant input", () => {
    renderWithProviders();

    openSwitcher();
    const input = screen.getByPlaceholderText("tenant-a");

    fireEvent.change(input, { target: { value: "bad tenant!" } });
    fireEvent.click(screen.getByRole("button", { name: "Switch" }));

    expect(
      screen.getByText(/Use letters, numbers, dots, underscores, or hyphens/i),
    ).toBeInTheDocument();
  });

  it("switches tenant after a short delay and shows a toast", () => {
    renderWithProviders();

    openSwitcher();
    const input = screen.getByPlaceholderText("tenant-a");

    fireEvent.change(input, { target: { value: "new-tenant" } });
    fireEvent.click(screen.getByRole("button", { name: "Switch" }));

    expect(screen.getAllByText("Switching tenant...").length).toBeGreaterThanOrEqual(2);

    act(() => {
      vi.advanceTimersByTime(600);
    });

    expect(screen.getByText("new-tenant")).toBeInTheDocument();
    expect(screen.getByText("Switched to tenant new-tenant")).toBeInTheDocument();
  });

  it("switches to host mode", () => {
    window.localStorage.setItem("aqua.currentTenant", "tenant-a");

    renderWithProviders();

    openSwitcher();
    fireEvent.click(screen.getByRole("button", { name: "Use host" }));

    expect(screen.getAllByText("Switching tenant...").length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText("Switching to host mode...")).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(600);
    });

    expect(screen.getByText("Host mode")).toBeInTheDocument();
    expect(screen.getByText("Switched to host mode")).toBeInTheDocument();
  });
});

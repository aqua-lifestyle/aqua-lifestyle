import { act, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { ToastProvider, useToast } from "./";

const ToastTrigger = () => {
  const { toast } = useToast();

  return (
    <button onClick={() => toast({ message: "Hello", title: "Success", type: "success" })}>
      Show
    </button>
  );
};

describe("ToastProvider", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("displays and auto-dismisses a toast", () => {
    render(
      <ToastProvider>
        <ToastTrigger />
      </ToastProvider>,
    );

    act(() => {
      screen.getByRole("button", { name: "Show" }).click();
    });

    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent("Hello");
    expect(alert).toHaveTextContent("Success");
    expect(alert).toHaveClass("w-full", "max-w-80");
    expect(screen.getByRole("button", { name: "Dismiss notification" }))
      .toHaveClass("min-h-11", "min-w-11");

    act(() => {
      vi.advanceTimersByTime(5000);
    });

    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});

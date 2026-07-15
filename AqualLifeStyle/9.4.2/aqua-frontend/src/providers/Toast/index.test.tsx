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

    expect(screen.getByRole("alert")).toHaveTextContent("Hello");
    expect(screen.getByRole("alert")).toHaveTextContent("Success");

    act(() => {
      vi.advanceTimersByTime(5000);
    });

    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});

import { act, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it } from "vitest";

import { TenantProvider, useTenantActions, useTenantState } from "./";

const TenantInspector = () => {
  const { currentTenant, isHost } = useTenantState();
  const { setTenant, clearTenant } = useTenantActions();

  return (
    <div>
      <span data-testid="tenant">{currentTenant ?? "null"}</span>
      <span data-testid="isHost">{isHost ? "host" : "tenant"}</span>
      <button onClick={() => setTenant("new-tenant")}>Set tenant</button>
      <button onClick={clearTenant}>Clear tenant</button>
    </div>
  );
};

describe("TenantProvider", () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  afterEach(() => {
    window.localStorage.clear();
  });

  it("loads the stored tenant on mount", () => {
    window.localStorage.setItem("aqua.currentTenant", "stored-tenant");

    render(
      <TenantProvider>
        <TenantInspector />
      </TenantProvider>,
    );

    expect(screen.getByTestId("tenant")).toHaveTextContent("stored-tenant");
    expect(screen.getByTestId("isHost")).toHaveTextContent("tenant");
  });

  it("updates tenant state", () => {
    render(
      <TenantProvider>
        <TenantInspector />
      </TenantProvider>,
    );

    act(() => {
      screen.getByText("Set tenant").click();
    });

    expect(screen.getByTestId("tenant")).toHaveTextContent("new-tenant");
    expect(screen.getByTestId("isHost")).toHaveTextContent("tenant");
  });

  it("clears tenant and returns to host mode", () => {
    window.localStorage.setItem("aqua.currentTenant", "stored-tenant");

    render(
      <TenantProvider>
        <TenantInspector />
      </TenantProvider>,
    );

    act(() => {
      screen.getByText("Clear tenant").click();
    });

    expect(screen.getByTestId("tenant")).toHaveTextContent("null");
    expect(screen.getByTestId("isHost")).toHaveTextContent("host");
  });
});

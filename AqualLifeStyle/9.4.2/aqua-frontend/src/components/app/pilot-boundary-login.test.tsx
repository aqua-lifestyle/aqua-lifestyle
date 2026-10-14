import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { PilotBoundary } from "./pilot-boundary";

let path = "/login";
let status = { isPilot: false, modeKnown: false, isRecovery: false };
vi.mock("next/navigation", () => ({ usePathname: () => path }));
vi.mock("@/src/shared/hooks/use-pilot-status", () => ({
  usePilotStatus: () => status,
}));

describe("authentication while capability status is unavailable", () => {
  beforeEach(() => {
    path = "/login";
    status = { isPilot: false, modeKnown: false, isRecovery: false };
  });

  it("mounts sign-in without presenting unknown mode as normal operation", () => {
    render(<PilotBoundary><form aria-label="Administrator sign-in" /></PilotBoundary>);
    expect(screen.getByRole("form", { name: "Administrator sign-in" })).toBeInTheDocument();
    expect(screen.queryByText(/Establishing the application/)).not.toBeInTheDocument();
    expect(screen.queryByRole("note")).not.toBeInTheDocument();
  });

  it.each([
    "/signup", "/login/other", "/admin/dashboard", "/admin/programme-participations",
    "/admin/weekly-earnings", "/member/programmes", "/products",
  ])("keeps %s closed when capability status is unknown", (value) => {
    path = value;
    render(<PilotBoundary><div>Business capability</div></PilotBoundary>);
    expect(screen.queryByText("Business capability")).not.toBeInTheDocument();
    expect(screen.getByText(/Establishing the application/)).toBeInTheDocument();
  });

  it("mounts the original dashboard only on verified recovery", () => {
    status.isRecovery = true;
    path = "/admin/dashboard";
    render(<PilotBoundary><div>Original dashboard shell</div></PilotBoundary>);
    expect(screen.getByText("Original dashboard shell")).toBeInTheDocument();
    expect(screen.queryByText(/Establishing the application/)).not.toBeInTheDocument();
  });

  it.each(["/admin/dashboard/other", "/admin/b6-discovery", "/admin/customers", "/admin/weekly-earnings", "/member/programmes", "/signup"])(
    "does not mount business content for %s in verified recovery", (value) => {
      status.isRecovery = true;
      path = value;
      render(<PilotBoundary><div>Business capability</div></PilotBoundary>);
      expect(screen.queryByText("Business capability")).not.toBeInTheDocument();
      expect(screen.getByText(/Restricted recovery mode is active/)).toBeInTheDocument();
    },
  );

  it("preserves the ordinary non-pilot route", () => {
    status.modeKnown = true;
    path = "/admin/dashboard";
    render(<PilotBoundary><div>Normal dashboard</div></PilotBoundary>);
    expect(screen.getByText("Normal dashboard")).toBeInTheDocument();
  });

  it("retains the explicit pilot label on a known pilot login", () => {
    status = { isPilot: true, modeKnown: true, isRecovery: false };
    render(<PilotBoundary><form aria-label="Administrator sign-in" /></PilotBoundary>);
    expect(screen.getByRole("form", { name: "Administrator sign-in" })).toBeInTheDocument();
    expect(screen.getByRole("note")).toHaveTextContent("TEST MONEY ONLY");
  });
});

import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { PilotBoundary } from "./pilot-boundary";

let path = "/login";
let status = { isPilot: false, modeKnown: false };
vi.mock("next/navigation", () => ({ usePathname: () => path }));
vi.mock("@/src/shared/hooks/use-pilot-status", () => ({
  usePilotStatus: () => status,
}));
vi.mock("@/src/features/recovery/recovery-admin-workspace", () => ({
  RecoveryAdminWorkspace: ({ discovery }: { discovery: boolean }) => <div>{discovery ? "Recovery discovery" : "Recovery workspace"}</div>,
}));

describe("authentication while capability status is unavailable", () => {
  beforeEach(() => {
    path = "/login";
    status = { isPilot: false, modeKnown: false };
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
    expect(screen.getByText(value === "/admin/dashboard" ? "Recovery workspace" : /Establishing the application/)).toBeInTheDocument();
  });

  it("replaces the discovery route with the recovery workspace without mounting business content", () => {
    path = "/admin/b6-discovery";
    render(<PilotBoundary><div>Business capability</div></PilotBoundary>);
    expect(screen.getByText("Recovery discovery")).toBeInTheDocument();
    expect(screen.queryByText("Business capability")).not.toBeInTheDocument();
  });

  it("does not extend the recovery exception to nested routes", () => {
    path = "/admin/b6-discovery/other";
    render(<PilotBoundary><div>Business capability</div></PilotBoundary>);
    expect(screen.queryByText("Business capability")).not.toBeInTheDocument();
    expect(screen.getByText(/Establishing the application/)).toBeInTheDocument();
  });

  it("retains the explicit pilot label on a known pilot login", () => {
    status = { isPilot: true, modeKnown: true };
    render(<PilotBoundary><form aria-label="Administrator sign-in" /></PilotBoundary>);
    expect(screen.getByRole("form", { name: "Administrator sign-in" })).toBeInTheDocument();
    expect(screen.getByRole("note")).toHaveTextContent("TEST MONEY ONLY");
  });
});

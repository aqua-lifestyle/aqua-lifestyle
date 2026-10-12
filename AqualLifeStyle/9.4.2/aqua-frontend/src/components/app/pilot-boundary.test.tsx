import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { PilotBoundary } from "./pilot-boundary";
import { isPilotRoute } from "@/src/shared/lib/pilot-routes";
let path = "/member/programmes";
vi.mock("next/navigation", () => ({ usePathname: () => path }));
vi.mock("@/src/shared/hooks/use-pilot-status", () => ({ usePilotStatus: () => ({ isPilot: true, modeKnown: true }) }));
describe("pilot commissioning boundary", () => {
  it("labels commissioned screens as test money", () => {
    path = "/member/programmes";
    render(<PilotBoundary><div>Joining</div></PilotBoundary>);
    expect(screen.getByText("Joining")).toBeInTheDocument();
    expect(screen.getByRole("note")).toHaveTextContent("TEST MONEY ONLY");
  });
  it.each(["/admin/loans", "/member/savings", "/admin/users", "/admin/entry-commitments", "/products", "/admin/tenants"])(
    "does not mount excluded capability %s", (value) => {
      path = value;
      render(<PilotBoundary><div>Excluded action</div></PilotBoundary>);
      expect(screen.queryByText("Excluded action")).not.toBeInTheDocument();
      expect(screen.getByText(/capability is closed/)).toBeInTheDocument();
      expect(isPilotRoute(value)).toBe(false);
    },
  );
});

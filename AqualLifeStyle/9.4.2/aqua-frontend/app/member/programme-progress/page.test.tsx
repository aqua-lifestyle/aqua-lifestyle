import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { usePilotStatus } from "@/src/shared/hooks/use-pilot-status";
import Page from "./page";

const { replace } = vi.hoisted(() => ({ replace: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ replace }) }));
vi.mock("@/src/shared/hooks/use-pilot-status", () => ({ usePilotStatus: vi.fn() }));
vi.mock("@/src/components/members/member-programme-progress", () => ({
  MemberProgrammeProgress: () => <div>Structural progress view</div>,
}));

describe("progress route commissioning", () => {
  beforeEach(() => vi.clearAllMocks());
  it("opens the existing structural view for the explicit pilot", () => {
    vi.mocked(usePilotStatus).mockReturnValue({ isPilot: true, modeKnown: true });
    render(<Page />);
    expect(screen.getByText("Structural progress view")).toBeInTheDocument();
    expect(replace).not.toHaveBeenCalled();
  });
  it("preserves the normal-mode journey redirect", async () => {
    vi.mocked(usePilotStatus).mockReturnValue({ isPilot: false, modeKnown: true });
    render(<Page />);
    await waitFor(() => expect(replace).toHaveBeenCalledWith("/member/programmes"));
    expect(screen.queryByText("Structural progress view")).not.toBeInTheDocument();
  });
  it("does not expose a view or choose a route when deployment mode is unknown", () => {
    vi.mocked(usePilotStatus).mockReturnValue({ isPilot: false, modeKnown: false });
    render(<Page />);
    expect(screen.queryByText("Structural progress view")).not.toBeInTheDocument();
    expect(replace).not.toHaveBeenCalled();
  });
});

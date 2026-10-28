import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { MembershipsProvider, useMembershipsActions, useMembershipsState } from "./index";

let permissions: string[] = [];
vi.mock("../Auth", () => ({ useAuthState: () => ({ session: { user: { permissions } } }) }));
vi.mock("@/src/shared/api", async () => ({
  ...await vi.importActual<typeof import("@/src/shared/api")>("@/src/shared/api"),
  httpClient: { get: vi.fn() },
}));
function Probe() {
  const { getMemberships } = useMembershipsActions();
  const { isError, isSuccess } = useMembershipsState();
  return <><button onClick={() => void getMemberships()}>Load</button><output>{isError ? "error" : isSuccess ? "loaded" : "waiting"}</output></>;
}
describe("membership catalogue permissions", () => {
  beforeEach(() => { vi.clearAllMocks(); permissions = ["Aqua.Memberships.ViewSelf"]; });
  it.each([false, true])("selects the authorized catalogue before querying (administrator=%s)", async (admin) => {
    if (admin) permissions.push("Aqua.Memberships.View");
    vi.mocked(httpClient.get).mockResolvedValue([]);
    render(<MembershipsProvider><Probe /></MembershipsProvider>);
    fireEvent.click(screen.getByText("Load"));
    await waitFor(() => expect(screen.getByText("loaded")).toBeVisible());
    expect(httpClient.get).toHaveBeenCalledExactlyOnceWith(admin ? apiEndpoints.memberships.getAll : apiEndpoints.memberships.getActiveTiers);
  });
  it("reports a denied catalogue without a fallback or fabricated empty result", async () => {
    vi.mocked(httpClient.get).mockRejectedValue(new Error("Forbidden"));
    render(<MembershipsProvider><Probe /></MembershipsProvider>);
    fireEvent.click(screen.getByText("Load"));
    await waitFor(() => expect(screen.getByText("error")).toBeVisible());
    expect(httpClient.get).toHaveBeenCalledExactlyOnceWith(apiEndpoints.memberships.getActiveTiers);
  });
});

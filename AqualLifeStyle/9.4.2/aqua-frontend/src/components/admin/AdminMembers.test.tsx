import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState, useToast } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { AdminMembers } from "./AdminMembers";

vi.mock("@/src/providers", () => ({
  useAuthState: vi.fn(),
  useMembershipsActions: vi.fn(),
  useMembershipsState: vi.fn(),
  useToast: vi.fn(),
}));
const editDialog = vi.hoisted(() => ({ onUpdated: undefined as undefined | (() => Promise<void>) }));
vi.mock("./EditMemberProfileDialog", () => ({
  EditMemberProfileDialog: ({ onUpdated }: { onUpdated: () => Promise<void> }) => {
    editDialog.onUpdated = onUpdated;
    return null;
  },
}));
vi.mock("@/src/shared/api", () => ({ httpClient: { get: vi.fn(), post: vi.fn() } }));

const authState = (permissions: string[]) => ({
  isAuthenticated: true,
  isReady: true,
  session: {
    accessToken: "token",
    expiresAt: null,
    user: { email: "admin@example.com", id: 7, name: "Admin", permissions, role: "SystemAdmin", tenantId: 1 },
  },
});

describe("AdminMembers", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    editDialog.onUpdated = undefined;
    vi.mocked(useToast).mockReturnValue({ toast: vi.fn() } as ReturnType<typeof useToast>);
  });

  it("does not request member data without the member view permission", () => {
    vi.mocked(useAuthState).mockReturnValue(authState([]));
    render(<AdminMembers />);

    expect(screen.getByText("You do not have permission to view members.")).toBeInTheDocument();
    expect(httpClient.get).not.toHaveBeenCalled();
  });

  it("loads members and hides mutation controls without their granular permissions", async () => {
    vi.mocked(useAuthState).mockReturnValue(authState(["Aqua.Admin.Members.View"]));
    vi.mocked(httpClient.get).mockResolvedValue({
      items: [{
        areaId: "11111111-1111-1111-1111-111111111111",
        areaName: "TEST business Area",
        creationTime: "2026-07-15T12:00:00Z",
        email: "ada@example.com",
        firstName: "Ada",
        id: 12,
        isActive: true,
        lastName: "Lovelace",
        membershipId: 2,
        membershipName: "Onyx",
        membershipType: 1,
        tenantId: 1,
        userId: 21,
      }],
      totalCount: 1,
    });
    render(<AdminMembers />);

    await waitFor(() => expect(screen.getByText("Ada Lovelace")).toBeInTheDocument());
    expect(httpClient.get).toHaveBeenCalledWith("/api/services/app/AdminMember/GetAll?MaxResultCount=20&SkipCount=0&Keyword=");
    expect(screen.getByText("TEST business Area")).toBeInTheDocument();
    expect(screen.queryByText("Area 1")).not.toBeInTheDocument();
    expect(screen.getByText("Onyx")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit profile" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Change plan" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Suspend" })).not.toBeInTheDocument();
  });

  const member = (id: number, areaName: string | null = "TEST business Area") => ({
    id, areaId: areaName ? "11111111-1111-1111-1111-111111111111" : null, areaName,
    creationTime: "2026-07-15T12:00:00Z", email: `test${id}@example.test`, firstName: "TEST", lastName: `Member ${id}`,
    isActive: true, membershipId: 2, membershipName: "TEST Plan", membershipType: 1, tenantId: 1, userId: id,
  });

  it("uses the authorized server total and reaches members beyond the first page", async () => {
    vi.mocked(useAuthState).mockReturnValue(authState(["Aqua.Admin.Members.View"]));
    vi.mocked(httpClient.get).mockImplementation(async (url) => String(url).includes("SkipCount=20")
      ? { items: [member(21)], totalCount: 21 }
      : { items: Array.from({ length: 20 }, (_, index) => member(index + 1)), totalCount: 21 });
    render(<AdminMembers />);
    await screen.findByText("Showing 1–20 of 21 accessible members");
    fireEvent.click(screen.getByRole("button", { name: "Next members" }));
    await screen.findByText("TEST Member 21");
    expect(httpClient.get).toHaveBeenLastCalledWith("/api/services/app/AdminMember/GetAll?MaxResultCount=20&SkipCount=20&Keyword=");
    expect(screen.getByText("Showing 21–21 of 21 accessible members")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Next members" })).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "Previous members" }));
    await screen.findByText("Showing 1–20 of 21 accessible members");
  });

  it("refreshes the current page when an earlier-page mutation finishes after navigation", async () => {
    vi.mocked(useAuthState).mockReturnValue(authState(["Aqua.Admin.Members.View", "Aqua.Admin.Members.Edit"]));
    vi.mocked(httpClient.get).mockImplementation(async (url) => String(url).includes("SkipCount=20")
      ? { items: [member(21)], totalCount: 21 }
      : { items: Array.from({ length: 20 }, (_, index) => member(index + 1)), totalCount: 21 });
    render(<AdminMembers />);
    await screen.findByText("Showing 1–20 of 21 accessible members");
    fireEvent.click(screen.getByRole("button", { name: "Manage member for TEST Member 1" }));
    const completePendingUpdate = editDialog.onUpdated!;
    fireEvent.click(screen.getByRole("button", { name: "Next members" }));
    await screen.findByText("TEST Member 21");
    await act(async () => { await completePendingUpdate(); });
    await waitFor(() => expect(httpClient.get).toHaveBeenCalledTimes(3));
    await screen.findByText("Showing 21–21 of 21 accessible members");
    expect(httpClient.get).toHaveBeenLastCalledWith("/api/services/app/AdminMember/GetAll?MaxResultCount=20&SkipCount=20&Keyword=");
    expect(screen.queryByText("TEST Member 1")).not.toBeInTheDocument();
    // A failed post-mutation refresh must not present a zero-member result.
    vi.mocked(httpClient.get).mockRejectedValueOnce(new Error("TEST refresh unavailable"));
    await act(async () => { await completePendingUpdate(); });
    await screen.findByText("Club members could not be loaded.");
    expect(screen.getByText("Unavailable")).toBeInTheDocument();
    expect(screen.queryByText("0 accessible members")).not.toBeInTheDocument();
  });

  it("searches all accessible pages on the server and resets the offset", async () => {
    vi.mocked(useAuthState).mockReturnValue(authState(["Aqua.Admin.Members.View"]));
    vi.mocked(httpClient.get).mockImplementation(async (url) => String(url).includes("Keyword=other")
      ? { items: [member(99)], totalCount: 1 }
      : { items: Array.from({ length: 20 }, (_, index) => member(index + 1)), totalCount: 41 });
    render(<AdminMembers />);
    await screen.findByText("Showing 1–20 of 41 accessible members");
    fireEvent.click(screen.getByRole("button", { name: "Next members" }));
    await screen.findByText("Showing 21–40 of 41 accessible members");
    fireEvent.change(screen.getByLabelText("Search club members"), { target: { value: "other" } });
    await screen.findByText("TEST Member 99");
    expect(httpClient.get).toHaveBeenLastCalledWith("/api/services/app/AdminMember/GetAll?MaxResultCount=20&SkipCount=0&Keyword=other");
    expect(screen.getByText("Showing 1–1 of 1 accessible members")).toBeInTheDocument();
  });

  it("keeps unavailable Area evidence and failed reads distinct from zero members", async () => {
    vi.mocked(useAuthState).mockReturnValue(authState(["Aqua.Admin.Members.View"]));
    vi.mocked(httpClient.get).mockResolvedValueOnce({ items: [member(1, null)], totalCount: 1 });
    render(<AdminMembers />);
    await screen.findByText("Area unavailable");
    vi.mocked(httpClient.get).mockRejectedValueOnce(new Error("TEST unavailable"));
    fireEvent.change(screen.getByLabelText("Search club members"), { target: { value: "unavailable" } });
    await screen.findByText("Club members could not be loaded.");
    expect(screen.getByText("Unavailable")).toBeInTheDocument();
    expect(screen.queryByText("0 accessible members")).not.toBeInTheDocument();
    expect(screen.queryByText("No club members found.")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Member pages")).not.toBeInTheDocument();
  });

  it("ignores a stale page response after the operator changes search", async () => {
    vi.mocked(useAuthState).mockReturnValue(authState(["Aqua.Admin.Members.View"]));
    let resolveOld: (value: unknown) => void = () => {};
    vi.mocked(httpClient.get).mockReturnValueOnce(new Promise((resolve) => { resolveOld = resolve; }))
      .mockResolvedValue({ items: [member(99)], totalCount: 1 });
    render(<AdminMembers />);
    await waitFor(() => expect(httpClient.get).toHaveBeenCalledTimes(1));
    fireEvent.change(screen.getByLabelText("Search club members"), { target: { value: "new" } });
    await screen.findByText("TEST Member 99");
    resolveOld({ items: [member(1)], totalCount: 400 });
    await waitFor(() => expect(screen.queryByText("TEST Member 1")).not.toBeInTheDocument());
    expect(screen.getByRole("status")).toHaveTextContent("of 1 accessible members");
  });
});

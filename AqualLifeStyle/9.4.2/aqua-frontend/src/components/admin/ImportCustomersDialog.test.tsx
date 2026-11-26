import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { httpClient } from "@/src/shared/api";

import { ImportCustomersDialog } from "./ImportCustomersDialog";

vi.mock("@/src/providers", () => ({ useAuthState: vi.fn() }));
vi.mock("@/src/shared/api", () => ({ httpClient: { post: vi.fn() } }));
const dropzone = vi.hoisted(() => ({ onDropAccepted: null as null | ((files: File[]) => void) }));
vi.mock("react-dropzone", () => ({
  useDropzone: (options: { onDropAccepted: (files: File[]) => void }) => {
    dropzone.onDropAccepted = options.onDropAccepted;
    return { getInputProps: () => ({}), getRootProps: () => ({}), isDragActive: false };
  },
}));

const authState = (permissions: string[]) => ({
  isAuthenticated: true,
  isReady: true,
  session: {
    accessToken: "token",
    expiresAt: null,
    user: { email: "admin@example.com", id: 7, name: "Admin", permissions, role: "SystemAdmin" },
  },
});

describe("ImportCustomersDialog", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    HTMLDialogElement.prototype.showModal = vi.fn(function (this: HTMLDialogElement) {
      this.setAttribute("open", "");
    });
    HTMLDialogElement.prototype.close = vi.fn(function (this: HTMLDialogElement) {
      this.removeAttribute("open");
    });
  });

  it("requires the dedicated customer import permission", () => {
    vi.mocked(useAuthState).mockReturnValue(authState([]));
    render(<ImportCustomersDialog />);
    expect(screen.queryByRole("button", { name: /import customers/i })).not.toBeInTheDocument();
  });

  it("previews before confirmation and reports the import result", async () => {
    vi.mocked(useAuthState).mockReturnValue(authState(["Aqua.Admin.Customers.Import"]));
    vi.mocked(httpClient.post)
      .mockResolvedValueOnce({
        canImport: true,
        errors: [],
        fileName: "customers.csv",
        previewId: "preview-1",
        rows: [{ contactNumber: "+27 82 123 4567", email: "ada@example.com", firstName: "Ada", homeAddress: "10 Aqua Street", isActive: true, lastName: "Lovelace", membershipId: 1, rowNumber: 2 }],
        totalRows: 1,
        validRows: 1,
      })
      .mockResolvedValueOnce({ errors: [], failedRows: 0, importedRows: 1, totalRows: 1 });

    render(<ImportCustomersDialog />);
    fireEvent.click(screen.getByRole("button", { name: /import customers/i }));
    act(() => {
      dropzone.onDropAccepted?.([
        new File(["FirstName,LastName,Email,ContactNumber,HomeAddress\nAda,Lovelace,ada@example.com,+27 82 123 4567,10 Aqua Street"], "customers.csv", { type: "text/csv" }),
      ]);
    });
    fireEvent.click(screen.getByRole("button", { name: /preview file/i }));

    expect(await screen.findByText("Ada Lovelace")).toBeInTheDocument();
    expect(httpClient.post).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole("button", { name: /confirm import of 1 customers/i }));

    await waitFor(() => expect(httpClient.post).toHaveBeenCalledTimes(2));
    expect(httpClient.post).toHaveBeenLastCalledWith("/api/services/app/CustomerImport/Import", { previewId: "preview-1" });
    expect(await screen.findByText(/Imported 1 of 1 customers/i)).toBeInTheDocument();
  });

  it("does not offer confirmation when a previous file's preview arrives after replacement", async () => {
    vi.mocked(useAuthState).mockReturnValue(authState(["Aqua.Admin.Customers.Import"]));
    let finishPreview: (value: unknown) => void = () => {};
    vi.mocked(httpClient.post).mockReturnValueOnce(new Promise((resolve) => { finishPreview = resolve; }));
    render(<ImportCustomersDialog />);
    fireEvent.click(screen.getByRole("button", { name: /import customers/i }));
    const select = (name: string) => act(() => { dropzone.onDropAccepted?.([new File(["synthetic"], name, { type: "text/csv" })]); });
    select("first.csv");
    fireEvent.click(screen.getByRole("button", { name: /preview file/i }));
    await waitFor(() => expect(httpClient.post).toHaveBeenCalledTimes(1));

    select("replacement.csv");
    await act(async () => {
      finishPreview({ canImport: true, errors: [], previewId: "stale", rows: [], totalRows: 1, validRows: 1 });
    });
    expect(screen.getByText("replacement.csv")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /confirm import/i })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /preview file/i })).toBeEnabled();
    expect(httpClient.post).toHaveBeenCalledExactlyOnceWith("/api/services/app/CustomerImport/Preview", expect.objectContaining({ fileName: "first.csv" }));
  });
});

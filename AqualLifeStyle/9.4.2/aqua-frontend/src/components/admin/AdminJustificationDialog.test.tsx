import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { AdminJustificationDialog } from "./AdminJustificationDialog";

describe("AdminJustificationDialog", () => {
  beforeEach(() => {
    HTMLDialogElement.prototype.showModal = vi.fn(function (this: HTMLDialogElement) { this.setAttribute("open", ""); });
    HTMLDialogElement.prototype.close = vi.fn(function (this: HTMLDialogElement) { this.removeAttribute("open"); });
  });

  it("requires and trims a descriptive audit justification", async () => {
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    render(<AdminJustificationDialog confirmLabel="Approve" description="Approve the application." onConfirm={onConfirm} title="Approve area leader" triggerLabel="Approve" />);

    fireEvent.click(screen.getByRole("button", { name: "Approve" }));
    fireEvent.click(screen.getAllByRole("button", { name: "Approve" })[1]);
    expect(await screen.findByText("Explain why this action is required.")).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Audit justification"), { target: { value: "  Checks completed  " } });
    fireEvent.click(screen.getAllByRole("button", { name: "Approve" })[1]);

    await waitFor(() => expect(onConfirm).toHaveBeenCalledWith("Checks completed"));
  });
});

import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Dialog } from "./dialog";
import { DialogActions } from "./dialog-actions";

describe("Dialog", () => {
  beforeEach(() => {
    HTMLDialogElement.prototype.showModal = vi.fn(function (this: HTMLDialogElement) {
      this.setAttribute("open", "");
    });
    HTMLDialogElement.prototype.close = vi.fn(function (this: HTMLDialogElement) {
      this.removeAttribute("open");
      this.dispatchEvent(new Event("close"));
    });
  });

  it("is viewport-bounded and gives the close control a comfortable target", () => {
    render(<Dialog onClose={vi.fn()} open title="Edit a long administration record"><p>Content</p></Dialog>);

    expect(screen.getByRole("dialog")).toHaveClass(
      "max-h-[calc(100dvh-2rem)]",
      "overflow-y-auto",
      "w-[min(calc(100vw-2rem),32rem)]",
    );
    expect(screen.getByRole("button", { name: "Close dialog" }))
      .toHaveClass("min-h-11", "min-w-11");
  });

  it("routes Escape cancellation through the controlled close callback", () => {
    const onClose = vi.fn();
    render(<Dialog onClose={onClose} open title="Confirm"><p>Content</p></Dialog>);

    const event = new Event("cancel", { bubbles: false, cancelable: true });
    fireEvent(screen.getByRole("dialog"), event);

    expect(event.defaultPrevented).toBe(true);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("retains unsaved input when a pointer gesture starts in a field and ends outside", () => {
    const onClose = vi.fn();
    render(<Dialog onClose={onClose} open title="Edit"><input aria-label="Draft" defaultValue="Unsaved draft" /></Dialog>);
    const dialog = screen.getByRole("dialog");
    vi.spyOn(dialog, "getBoundingClientRect").mockReturnValue(new DOMRect(20, 20, 400, 200));
    const field = screen.getByLabelText("Draft");
    fireEvent(field, new MouseEvent("pointerdown", { bubbles: true, clientX: 100, clientY: 100 }));
    fireEvent.click(dialog, { clientX: 1, clientY: 1 });
    expect(onClose).not.toHaveBeenCalled();
    expect(field).toHaveValue("Unsaved draft");
  });

  it("dismisses a gesture that begins and ends on the backdrop", () => {
    const onClose = vi.fn();
    render(<Dialog onClose={onClose} open title="Edit"><p>Content</p></Dialog>);
    const dialog = screen.getByRole("dialog");
    vi.spyOn(dialog, "getBoundingClientRect").mockReturnValue(new DOMRect(20, 20, 400, 200));
    fireEvent(dialog, new MouseEvent("pointerdown", { bubbles: true, clientX: 1, clientY: 1 }));
    fireEvent.click(dialog, { clientX: 1, clientY: 1 });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("does not dismiss scrollbar or backdrop-to-interior gestures", () => {
    const onClose = vi.fn();
    render(<Dialog onClose={onClose} open title="Edit"><p>Content</p></Dialog>);
    const dialog = screen.getByRole("dialog");
    vi.spyOn(dialog, "getBoundingClientRect").mockReturnValue(new DOMRect(20, 20, 400, 200));
    fireEvent(dialog, new MouseEvent("pointerdown", { bubbles: true, clientX: 419, clientY: 100 }));
    fireEvent.click(dialog, { clientX: 419, clientY: 100 });
    fireEvent(dialog, new MouseEvent("pointerdown", { bubbles: true, clientX: 1, clientY: 1 }));
    fireEvent.click(dialog, { clientX: 100, clientY: 100 });
    expect(onClose).not.toHaveBeenCalled();
  });
});

describe("DialogActions", () => {
  it("stacks full-width actions on narrow screens and restores row actions at sm", () => {
    render(<DialogActions data-testid="actions"><button>Cancel</button><button>Save</button></DialogActions>);

    expect(screen.getByTestId("actions")).toHaveClass(
      "flex-col",
      "sm:flex-row",
      "[&>*]:w-full",
      "sm:[&>*]:w-auto",
    );
  });
});

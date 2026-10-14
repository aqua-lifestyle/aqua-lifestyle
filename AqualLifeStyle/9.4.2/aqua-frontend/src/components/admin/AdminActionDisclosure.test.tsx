import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { AdminActionDisclosure } from "./AdminActionDisclosure";

describe("AdminActionDisclosure", () => {
  it("keeps secondary row actions reachable without displaying them all at once", () => {
    render(
      <AdminActionDisclosure accessibleLabel="Manage account for Test User" label="Manage account">
        <button>Edit details</button>
        <button>Remove account</button>
      </AdminActionDisclosure>,
    );

    const trigger = screen.getByRole("button", { name: "Manage account for Test User" });
    expect(trigger).toHaveTextContent("Manage account");
    expect(trigger).toHaveAccessibleName("Manage account for Test User");
    expect(trigger).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByRole("button", { name: "Edit details" })).not.toBeInTheDocument();

    fireEvent.click(trigger);
    expect(trigger).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByRole("group", { name: "Manage account for Test User" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Remove account" })).toBeInTheDocument();

    screen.getByRole("button", { name: "Edit details" }).focus();
    fireEvent.keyDown(document, { key: "Escape" });
    expect(trigger).toHaveAttribute("aria-expanded", "false");
    expect(trigger).toHaveFocus();
  });

  it("closes only the focused disclosure when several rows are expanded", () => {
    render(<>
      <AdminActionDisclosure accessibleLabel="Manage first" label="Manage"><button>First action</button></AdminActionDisclosure>
      <AdminActionDisclosure accessibleLabel="Manage second" label="Manage"><button>Second action</button></AdminActionDisclosure>
    </>);
    const first = screen.getByRole("button", { name: "Manage first" });
    const second = screen.getByRole("button", { name: "Manage second" });
    fireEvent.click(first);
    fireEvent.click(second);
    screen.getByRole("button", { name: "First action" }).focus();
    fireEvent.keyDown(document, { key: "Escape" });
    expect(first).toHaveAttribute("aria-expanded", "false");
    expect(second).toHaveAttribute("aria-expanded", "true");
    expect(first).toHaveFocus();
  });
});

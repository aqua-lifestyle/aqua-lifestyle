import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { SelectField } from "./select-field";

describe("SelectField", () => {
  it("renders label and options", () => {
    render(
      <SelectField label="Role" name="role">
        <option value="admin">Admin</option>
        <option value="user">User</option>
      </SelectField>,
    );
    expect(screen.getByLabelText("Role")).toBeInTheDocument();
    expect(screen.getByRole("combobox")).toHaveValue("admin");
  });

  it("forwards change events", () => {
    const handleChange = vi.fn();
    render(
      <SelectField label="Role" name="role" onChange={handleChange}>
        <option value="admin">Admin</option>
        <option value="user">User</option>
      </SelectField>,
    );
    fireEvent.change(screen.getByLabelText("Role"), { target: { value: "user" } });
    expect(handleChange).toHaveBeenCalledTimes(1);
  });

  it("shows error message", () => {
    render(
      <SelectField errorMessage="Required" label="Role" name="role">
        <option value="">Select</option>
      </SelectField>,
    );
    expect(screen.getByText("Required")).toBeInTheDocument();
    expect(screen.getByRole("combobox")).toHaveAttribute("aria-invalid", "true");
  });
});

import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { TextField } from "./text-field";

describe("TextField", () => {
  it("renders label and input", () => {
    render(<TextField label="Name" name="name" />);
    expect(screen.getByLabelText("Name")).toBeInTheDocument();
  });

  it("forwards value changes", () => {
    const handleChange = vi.fn();
    render(<TextField label="Name" name="name" onChange={handleChange} />);
    fireEvent.change(screen.getByLabelText("Name"), {
      target: { value: "John" },
    });
    expect(handleChange).toHaveBeenCalledTimes(1);
  });

  it("shows error message", () => {
    render(<TextField errorMessage="Required" label="Name" name="name" />);
    expect(screen.getByText("Required")).toBeInTheDocument();
    expect(screen.getByLabelText("Name")).toHaveAttribute("aria-invalid", "true");
  });
});

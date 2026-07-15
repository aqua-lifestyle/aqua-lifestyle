import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { StatusMessage } from "./status-message";

describe("StatusMessage", () => {
  it("renders children", () => {
    render(<StatusMessage>Saved</StatusMessage>);
    expect(screen.getByText("Saved")).toBeInTheDocument();
  });

  it("uses tone title by default", () => {
    render(<StatusMessage tone="success">Saved</StatusMessage>);
    expect(screen.getByText("Success")).toBeInTheDocument();
  });

  it("allows custom title", () => {
    render(<StatusMessage title="Done">Saved</StatusMessage>);
    expect(screen.getByText("Done")).toBeInTheDocument();
  });
});

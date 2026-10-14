import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { Tabs } from "./tabs";

describe("Tabs", () => {
  it("links tabs to panels and supports arrow-key selection", () => {
    const onChange = vi.fn();
    render(
      <Tabs
        onChange={onChange}
        tabs={[
          { content: "AQGreen content", id: "entry", label: "AQGreen" },
          { content: "Onyx content", id: "onyx", label: "Onyx" },
        ]}
        value="entry"
      />,
    );

    const aqGreen = screen.getByRole("tab", { name: "AQGreen" });
    const onyx = screen.getByRole("tab", { name: "Onyx" });
    const panel = screen.getByRole("tabpanel");
    expect(aqGreen).toHaveAttribute("aria-controls", panel.id);
    expect(panel).toHaveAttribute("aria-labelledby", aqGreen.id);
    expect(aqGreen).toHaveAttribute("tabindex", "0");
    expect(onyx).toHaveAttribute("tabindex", "-1");

    fireEvent.keyDown(aqGreen, { key: "ArrowRight" });
    expect(onChange).toHaveBeenCalledWith("onyx");
    expect(onyx).toHaveFocus();
  });
});

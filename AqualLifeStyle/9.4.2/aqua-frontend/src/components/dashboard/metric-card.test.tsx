import { Users } from "lucide-react";
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { MetricCard } from "./metric-card";

describe("MetricCard", () => {
  it("renders the label, value, and icon", () => {
    render(<MetricCard icon={Users} label="Total customers" value={42} />);

    expect(screen.getByText("Total customers")).toBeInTheDocument();
    expect(screen.getByText("42")).toBeInTheDocument();
  });

  it("renders a positive trend with the correct styling", () => {
    const { container } = render(
      <MetricCard icon={Users} label="Revenue" trend={12} value="R1,200" />,
    );

    expect(screen.getByText("+12%")).toBeInTheDocument();
    expect(container.querySelector(".bg-success\\/10")).toBeInTheDocument();
  });

  it("renders a negative trend with the correct styling", () => {
    const { container } = render(
      <MetricCard icon={Users} label="Churn" trend={-5} value={3} />,
    );

    expect(screen.getByText("-5%")).toBeInTheDocument();
    expect(container.querySelector(".bg-error\\/10")).toBeInTheDocument();
  });

  it("renders a skeleton while loading", () => {
    render(<MetricCard icon={Users} isLoading label="Loading" value={0} />);

    expect(screen.queryByText("0")).not.toBeInTheDocument();
    expect(document.querySelector(".skeleton-shimmer")).toBeInTheDocument();
  });
});

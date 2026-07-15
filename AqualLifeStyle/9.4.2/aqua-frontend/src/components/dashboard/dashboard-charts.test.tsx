import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { DashboardCharts } from "./dashboard-charts";

vi.mock("recharts", () => ({
  Bar: () => null,
  BarChart: ({ data }: { data: unknown[] }) => (
    <div data-testid="bar-chart">{JSON.stringify(data)}</div>
  ),
  CartesianGrid: () => null,
  Cell: () => null,
  Legend: () => null,
  Pie: ({ data }: { data: unknown[] }) => (
    <div data-testid="pie-chart">{JSON.stringify(data)}</div>
  ),
  PieChart: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="pie-chart-wrapper">{children}</div>
  ),
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="responsive-container">{children}</div>
  ),
  Tooltip: () => null,
  XAxis: () => null,
  YAxis: () => null,
}));

describe("DashboardCharts", () => {
  const membershipData = [
    { count: 10, name: "Gold" },
    { count: 5, name: "Silver" },
  ];

  const enquiryData = [
    { name: "Pending", value: 3 },
    { name: "Responded", value: 2 },
    { name: "Closed", value: 1 },
  ];

  it("renders chart titles and passes data to charts", () => {
    render(
      <DashboardCharts
        enquiryData={enquiryData}
        membershipData={membershipData}
      />,
    );

    expect(screen.getByText("Membership tier mix")).toBeInTheDocument();
    expect(screen.getByText("Enquiry pipeline")).toBeInTheDocument();
    expect(screen.getByTestId("bar-chart")).toHaveTextContent(
      JSON.stringify(membershipData),
    );
    expect(screen.getByTestId("pie-chart")).toHaveTextContent(
      JSON.stringify(enquiryData),
    );
  });
});

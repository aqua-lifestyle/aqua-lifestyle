import { MessageSquare, Wallet } from "lucide-react";
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { ActivityFeed } from "./activity-feed";

describe("ActivityFeed", () => {
  const items = [
    {
      description: "Customer enquired about Product A.",
      icon: MessageSquare,
      id: "enquiry-1",
      meta: "Pending",
      timestamp: "2026-07-08T10:00:00Z",
      title: "New enquiry #1",
    },
    {
      description: "Customer reserved Product B.",
      icon: Wallet,
      id: "order-1",
      meta: "Reserved",
      timestamp: "2026-07-08T09:00:00Z",
      title: "Order intent #1",
    },
  ];

  it("renders an empty state when there are no items", () => {
    render(<ActivityFeed items={[]} />);

    expect(screen.getByText("No recent activity.")).toBeInTheDocument();
  });

  it("renders activity items with title, description, meta, and timestamp", () => {
    render(<ActivityFeed items={items} />);

    expect(screen.getByText("New enquiry #1")).toBeInTheDocument();
    expect(screen.getByText("Customer enquired about Product A.")).toBeInTheDocument();
    expect(screen.getByText("Pending")).toBeInTheDocument();

    expect(screen.getByText("Order intent #1")).toBeInTheDocument();
    expect(screen.getByText("Reserved")).toBeInTheDocument();
  });
});

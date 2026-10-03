import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { DataTable } from "./data-table";

type Row = { id: number; name: string; score: number };

const rows: Row[] = [
  { id: 1, name: "Zulu", score: 2 },
  { id: 2, name: "Alpha", score: 10 },
  ...Array.from({ length: 9 }, (_, index) => ({
    id: index + 3,
    name: `Member ${index}`,
    score: index + 20,
  })),
];

const columns = [
  {
    header: "Name",
    key: "name",
    render: (row: Row) => <strong>{row.name}</strong>,
    sortable: true,
  },
  { header: "Score", key: "score", sortable: true },
];

describe("DataTable", () => {
  it("distinguishes failed loading from an empty result and does not expose cached row actions", () => {
    const retry = vi.fn();
    render(<DataTable columns={[...columns, { header: "Actions", key: "actions", render: () => <button>Change record</button> }]} data={rows} keyExtractor={row => row.id} label="Users" loadError onRetry={retry} />);
    expect(screen.getByRole("region", { name: "Users unavailable" })).toBeInTheDocument();
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
    expect(screen.queryByText("No records found.")).not.toBeInTheDocument();
    expect(screen.queryByText("Zulu")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Change record" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Go to next page" })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Retry loading Users" }));
    expect(retry).toHaveBeenCalledTimes(1);
  });

  it("retains the ordinary empty state after a successful empty read", () => {
    render(<DataTable columns={columns} data={[]} keyExtractor={row => row.id} label="Users" />);
    expect(screen.getByRole("table", { name: "Users" })).toBeInTheDocument();
    expect(screen.getByText("No records found.")).toBeInTheDocument();
    expect(screen.queryByRole("region", { name: "Users unavailable" })).not.toBeInTheDocument();
  });

  it("provides a named, contained scroll region and named search control", () => {
    render(
      <DataTable
        columns={columns}
        data={rows}
        keyExtractor={(row) => row.id}
        label="User accounts"
        searchFn={(row, query) => row.name.toLowerCase().includes(query)}
      />,
    );

    const region = screen.getByRole("region", { name: "User accounts table" });
    expect(region).toHaveClass("overflow-x-auto", "overscroll-x-contain");
    expect(within(region).getByRole("table", { name: "User accounts" }))
      .toHaveClass("w-max", "min-w-full");
    expect(screen.getByRole("searchbox", { name: "Search User accounts" }))
      .toBeInTheDocument();
  });

  it("sorts rendered columns from their data and exposes the sort state", () => {
    render(
      <DataTable
        columns={columns}
        data={rows.slice(0, 2)}
        keyExtractor={(row) => row.id}
        label="Users"
      />,
    );

    const sortButton = screen.getByRole("button", { name: "Name" });
    const header = sortButton.closest("th");
    expect(header).toHaveAttribute("aria-sort", "none");

    fireEvent.click(sortButton);
    expect(header).toHaveAttribute("aria-sort", "ascending");
    expect(screen.getAllByRole("row")[1]).toHaveTextContent("Alpha");

    fireEvent.click(sortButton);
    expect(header).toHaveAttribute("aria-sort", "descending");
    expect(screen.getAllByRole("row")[1]).toHaveTextContent("Zulu");

    fireEvent.click(screen.getByRole("button", { name: "Score" }));
    expect(screen.getAllByRole("row")[1]).toHaveTextContent("Zulu");
  });

  it("names pagination controls and exposes touch-sized control classes", () => {
    render(
      <DataTable
        columns={columns}
        data={rows}
        keyExtractor={(row) => row.id}
        label="Users"
        pageSize={10}
      />,
    );

    expect(screen.getByRole("button", { name: "Go to first page" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Go to previous page" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Go to next page" })).toHaveClass("size-11");
    expect(screen.getByRole("button", { name: "Go to last page" })).toHaveClass("size-11");
  });

  it("sorts nullable numeric data deliberately without treating absent values as zero", () => {
    const data = [
      { id: 1, name: "Ten", score: 10 },
      { id: 2, name: "Null", score: null },
      { id: 3, name: "Two", score: 2 },
      { id: 4, name: "Undefined", score: undefined },
      { id: 5, name: "Zero", score: 0 },
      { id: 6, name: "Negative", score: -3 },
    ];
    render(<DataTable columns={[{ header: "Name", key: "name" }, { header: "Score", key: "score", sortable: true }]} data={data} keyExtractor={row => row.id} />);
    const names = () => screen.getAllByRole("row").slice(1).map(row => within(row).getAllByRole("cell")[0].textContent);
    fireEvent.click(screen.getByRole("button", { name: "Score" }));
    expect(names()).toEqual(["Null", "Undefined", "Negative", "Zero", "Two", "Ten"]);
    fireEvent.click(screen.getByRole("button", { name: "Score" }));
    expect(names()).toEqual(["Ten", "Two", "Zero", "Negative", "Null", "Undefined"]);
  });

  it("honours an explicit null sort value instead of falling back to the raw field", () => {
    render(<DataTable columns={[{ header: "Name", key: "name" }, { header: "Score", key: "score", sortable: true, sortValue: (row: Row) => row.id === 2 ? null : row.score }]} data={rows.slice(0, 2)} keyExtractor={row => row.id} />);
    fireEvent.click(screen.getByRole("button", { name: "Score" }));
    expect(screen.getAllByRole("row")[1]).toHaveTextContent("Alpha");
  });
});

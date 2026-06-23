"use client";

import {
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Search,
} from "lucide-react";
import { useMemo, useState } from "react";

import { cn } from "@/src/shared/lib/utils";
import { Button } from "./button";

type DataTableColumn<T> = {
  header: string;
  key: string;
  render?: (row: T) => React.ReactNode;
  sortable?: boolean;
};

type DataTableProps<T> = {
  className?: string;
  columns: DataTableColumn<T>[];
  data: T[];
  emptyState?: React.ReactNode;
  keyExtractor: (row: T) => string | number;
  pageSize?: number;
  searchFn?: (row: T, query: string) => boolean;
};

export function DataTable<T>({
  className,
  columns,
  data,
  emptyState,
  keyExtractor,
  pageSize = 10,
  searchFn,
}: DataTableProps<T>) {
  const [query, setQuery] = useState("");
  const [sortKey, setSortKey] = useState<string | null>(null);
  const [sortDirection, setSortDirection] = useState<"asc" | "desc">("asc");
  const [page, setPage] = useState(1);

  const filteredData = useMemo(() => {
    const searched = query.trim()
      ? data.filter((row) => searchFn?.(row, query.trim().toLowerCase()) ?? true)
      : data;

    if (!sortKey) return searched;

    const column = columns.find((c) => c.key === sortKey);
    if (!column) return searched;

    return [...searched].sort((a, b) => {
      const aValue = column.render ? column.render(a) : String((a as never)[sortKey]);
      const bValue = column.render ? column.render(b) : String((b as never)[sortKey]);
      const aStr = String(aValue).toLowerCase();
      const bStr = String(bValue).toLowerCase();

      if (aStr < bStr) return sortDirection === "asc" ? -1 : 1;
      if (aStr > bStr) return sortDirection === "asc" ? 1 : -1;
      return 0;
    });
  }, [data, query, searchFn, sortKey, sortDirection, columns]);

  const totalPages = Math.max(1, Math.ceil(filteredData.length / pageSize));
  const currentPage = Math.min(page, totalPages);
  const paginatedData = filteredData.slice(
    (currentPage - 1) * pageSize,
    currentPage * pageSize,
  );

  const handleSort = (key: string) => {
    if (sortKey === key) {
      setSortDirection((current) => (current === "asc" ? "desc" : "asc"));
    } else {
      setSortKey(key);
      setSortDirection("asc");
    }
    setPage(1);
  };

  return (
    <div className={cn("flex flex-col gap-4", className)}>
      {searchFn ? (
        <div className="relative">
          <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <input
            className="w-full rounded-lg border border-border bg-card py-2 pl-9 pr-3 text-sm text-foreground outline-none transition focus:border-accent focus:ring-2 focus:ring-accent/20"
            onChange={(event) => {
              setQuery(event.target.value);
              setPage(1);
            }}
            placeholder="Search..."
            type="search"
            value={query}
          />
        </div>
      ) : null}

      <div className="overflow-hidden rounded-xl border border-border">
        <table className="w-full text-sm">
          <thead className="bg-muted/50">
            <tr>
              {columns.map((column) => (
                <th
                  key={column.key}
                  className={cn(
                    "px-4 py-3 text-left font-semibold text-foreground",
                    column.sortable && "cursor-pointer select-none hover:bg-muted",
                  )}
                  onClick={() => column.sortable && handleSort(column.key)}
                >
                  <div className="flex items-center gap-1">
                    {column.header}
                    {column.sortable && sortKey === column.key ? (
                      <span className="text-accent">
                        {sortDirection === "asc" ? "↑" : "↓"}
                      </span>
                    ) : null}
                  </div>
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {paginatedData.length === 0 ? (
              <tr>
                <td
                  className="px-4 py-8 text-center text-muted-foreground"
                  colSpan={columns.length}
                >
                  {emptyState ?? "No records found."}
                </td>
              </tr>
            ) : (
              paginatedData.map((row) => (
                <tr
                  key={keyExtractor(row)}
                  className="transition hover:bg-muted/50"
                >
                  {columns.map((column) => (
                    <td key={column.key} className="px-4 py-3">
                      {column.render ? column.render(row) : String((row as never)[column.key])}
                    </td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {filteredData.length > pageSize ? (
        <div className="flex items-center justify-between gap-4">
          <p className="text-sm text-muted-foreground">
            Showing {(currentPage - 1) * pageSize + 1} -
            {Math.min(currentPage * pageSize, filteredData.length)} of {filteredData.length}
          </p>
          <div className="flex items-center gap-2">
            <Button
              disabled={currentPage === 1}
              onClick={() => setPage(1)}
              size="sm"
              variant="outline"
            >
              <ChevronsLeft className="size-4" />
            </Button>
            <Button
              disabled={currentPage === 1}
              onClick={() => setPage((p) => p - 1)}
              size="sm"
              variant="outline"
            >
              <ChevronLeft className="size-4" />
            </Button>
            <span className="text-sm font-semibold text-foreground">
              {currentPage} / {totalPages}
            </span>
            <Button
              disabled={currentPage === totalPages}
              onClick={() => setPage((p) => p + 1)}
              size="sm"
              variant="outline"
            >
              <ChevronRight className="size-4" />
            </Button>
            <Button
              disabled={currentPage === totalPages}
              onClick={() => setPage(totalPages)}
              size="sm"
              variant="outline"
            >
              <ChevronsRight className="size-4" />
            </Button>
          </div>
        </div>
      ) : null}
    </div>
  );
}

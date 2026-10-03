"use client";

import {
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Search,
} from "lucide-react";
import { useId, useMemo, useState } from "react";

import { cn } from "@/src/shared/lib/utils";
import { Button } from "./button";

type DataTableColumn<T> = {
  header: string;
  key: string;
  render?: (row: T) => React.ReactNode;
  sortValue?: (row: T) => boolean | number | string | null | undefined;
  sortable?: boolean;
};

type DataTableProps<T> = {
  className?: string;
  columns: DataTableColumn<T>[];
  data: T[];
  emptyState?: React.ReactNode;
  keyExtractor: (row: T) => string | number;
  label?: string;
  loadError?: boolean;
  onRetry?: () => void;
  pageSize?: number;
  searchFn?: (row: T, query: string) => boolean;
};

export function DataTable<T>({
  className,
  columns,
  data,
  emptyState,
  keyExtractor,
  label = "Records",
  loadError = false,
  onRetry,
  pageSize = 10,
  searchFn,
}: DataTableProps<T>) {
  const [query, setQuery] = useState("");
  const [sortKey, setSortKey] = useState<string | null>(null);
  const [sortDirection, setSortDirection] = useState<"asc" | "desc">("asc");
  const [page, setPage] = useState(1);
  const searchId = useId();

  const filteredData = useMemo(() => {
    const searched = query.trim()
      ? data.filter((row) => searchFn?.(row, query.trim().toLowerCase()) ?? true)
      : data;

    if (!sortKey) return searched;

    const column = columns.find((c) => c.key === sortKey);
    if (!column) return searched;

    return [...searched].sort((a, b) => {
      const aValue = column.sortValue ? column.sortValue(a) : (a as unknown as Record<string, unknown>)[sortKey];
      const bValue = column.sortValue ? column.sortValue(b) : (b as unknown as Record<string, unknown>)[sortKey];
      if (typeof aValue === "number" && typeof bValue === "number") {
        return sortDirection === "asc" ? aValue - bValue : bValue - aValue;
      }

      const aStr = String(aValue ?? "").toLowerCase();
      const bStr = String(bValue ?? "").toLowerCase();

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

  if (loadError) {
    return (
      <section aria-label={`${label} unavailable`} className={cn("flex min-w-0 flex-col items-start gap-3 rounded-xl border border-border bg-card p-5", className)}>
        <p className="wrap-anywhere text-sm text-muted-foreground">{label} could not be loaded.</p>
        {onRetry ? <Button aria-label={`Retry loading ${label}`} className="w-full sm:w-auto" onClick={onRetry} variant="outline">Retry loading</Button> : null}
      </section>
    );
  }

  return (
    <div className={cn("flex flex-col gap-4", className)}>
      {searchFn ? (
        <div className="relative">
          <label className="sr-only" htmlFor={searchId}>Search {label}</label>
          <Search aria-hidden="true" className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <input
            className="min-h-11 w-full rounded-lg border border-control-border bg-card py-2 pl-9 pr-3 text-sm text-foreground outline-none transition focus:border-accent focus:ring-2 focus:ring-accent/20 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            id={searchId}
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

      <div className="rounded-xl border border-border">
        <div
          aria-label={`${label} table`}
          className="overflow-x-auto overscroll-x-contain rounded-xl focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          role="region"
          tabIndex={0}
        >
        <table className="w-max min-w-full text-sm">
          <caption className="sr-only">{label}</caption>
          <thead className="bg-muted/50">
            <tr>
              {columns.map((column) => {
                const activeSort = sortKey === column.key;
                return (
                <th
                  key={column.key}
                  aria-sort={column.sortable ? activeSort ? (sortDirection === "asc" ? "ascending" : "descending") : "none" : undefined}
                  className={cn(
                    "whitespace-nowrap text-left font-semibold text-foreground",
                    !column.sortable && "px-4 py-3",
                  )}
                >
                  {column.sortable ? (
                  <button
                    className="flex min-h-11 w-full items-center gap-1 px-4 py-2 text-left transition hover:bg-muted focus-visible:outline focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-accent"
                    onClick={() => handleSort(column.key)}
                    type="button"
                  >
                    {column.header}
                    {activeSort ? (
                      <span aria-hidden="true" className="text-accent">
                        {sortDirection === "asc" ? "↑" : "↓"}
                      </span>
                    ) : null}
                  </button>
                  ) : column.header}
                </th>
              )})}
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
                    <td key={column.key} className="px-4 py-3 align-top">
                      <div className="max-w-md wrap-anywhere">
                        {column.render ? column.render(row) : String((row as unknown as Record<string, unknown>)[column.key])}
                      </div>
                    </td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
        </div>
      </div>

      {filteredData.length > pageSize ? (
        <div className="flex flex-col items-start gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
          <p aria-live="polite" className="text-sm text-muted-foreground">
            Showing {(currentPage - 1) * pageSize + 1} -
            {Math.min(currentPage * pageSize, filteredData.length)} of {filteredData.length}
          </p>
          <div className="flex w-full items-center justify-between gap-2 sm:w-auto sm:justify-start">
            <Button
              aria-label="Go to first page"
              className="size-11 p-0 lg:size-10"
              disabled={currentPage === 1}
              onClick={() => setPage(1)}
              size="sm"
              variant="outline"
            >
              <ChevronsLeft aria-hidden="true" className="size-4" />
            </Button>
            <Button
              aria-label="Go to previous page"
              className="size-11 p-0 lg:size-10"
              disabled={currentPage === 1}
              onClick={() => setPage((p) => p - 1)}
              size="sm"
              variant="outline"
            >
              <ChevronLeft aria-hidden="true" className="size-4" />
            </Button>
            <span className="text-sm font-semibold text-foreground">
              {currentPage} / {totalPages}
            </span>
            <Button
              aria-label="Go to next page"
              className="size-11 p-0 lg:size-10"
              disabled={currentPage === totalPages}
              onClick={() => setPage((p) => p + 1)}
              size="sm"
              variant="outline"
            >
              <ChevronRight aria-hidden="true" className="size-4" />
            </Button>
            <Button
              aria-label="Go to last page"
              className="size-11 p-0 lg:size-10"
              disabled={currentPage === totalPages}
              onClick={() => setPage(totalPages)}
              size="sm"
              variant="outline"
            >
              <ChevronsRight aria-hidden="true" className="size-4" />
            </Button>
          </div>
        </div>
      ) : null}
    </div>
  );
}

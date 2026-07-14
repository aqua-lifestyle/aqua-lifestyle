import { Skeleton } from "@/src/shared/ui";

export default function AdminDashboardLoading() {
  return (
    <main className="min-h-[calc(100dvh-4rem)] bg-muted/30 px-4 py-8 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-7xl">
        <Skeleton className="h-10 w-72" />
        <Skeleton className="mt-3 h-5 w-full max-w-xl" />
        <div className="mt-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
          {[0, 1, 2, 3, 4].map((item) => <Skeleton className="h-32" key={item} />)}
        </div>
      </div>
    </main>
  );
}

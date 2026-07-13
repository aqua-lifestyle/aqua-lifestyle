"use client";

import { BarChart3, Users } from "lucide-react";
import { useEffect, useMemo } from "react";

import {
  useAreaLeadersActions,
  useAreaLeadersState,
  useAreaSpacesActions,
  useAreaSpacesState,
  useAuthState,
  useFacilitatorsActions,
  useFacilitatorsState,
  useOrderIntentsActions,
  useOrderIntentsState,
} from "@/src/providers";
import {
  Breadcrumb,
  Card,
  EmptyState,
  LinkButton,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";

export const AreaLeaderDashboard = () => {
  const { getAreaLeaders } = useAreaLeadersActions();
  const { areaLeaders } = useAreaLeadersState();

  const { getAreaSpaces } = useAreaSpacesActions();
  const { areaSpaces } = useAreaSpacesState();

  const { getFacilitators } = useFacilitatorsActions();
  const { facilitators } = useFacilitatorsState();

  const { getOrderIntents } = useOrderIntentsActions();
  const { orderIntents, isLoadError: isOrdersError, isLoadPending: isOrdersPending, loadErrorMessage: ordersErrorMessage } = useOrderIntentsState();
  const { isLoadError: isAreaLeadersError, isLoadPending: isAreaLeadersPending, loadErrorMessage: areaLeadersErrorMessage } = useAreaLeadersState();
  const { isLoadError: isAreaSpacesError, isLoadPending: isAreaSpacesPending, loadErrorMessage: areaSpacesErrorMessage } = useAreaSpacesState();
  const { isLoadError: isFacilitatorsError, isLoadPending: isFacilitatorsPending, loadErrorMessage: facilitatorsErrorMessage } = useFacilitatorsState();

  const { session } = useAuthState();
  const hasPermission = session?.user?.permissions?.includes("Pages.AreaLeaders") ?? false;

  // ALL hooks before early returns
  useEffect(() => {
    void getAreaLeaders();
    void getAreaSpaces();
    void getFacilitators();
    void getOrderIntents();
  }, [getAreaLeaders, getAreaSpaces, getFacilitators, getOrderIntents]);

  const isLoading = isAreaLeadersPending || isAreaSpacesPending || isFacilitatorsPending || isOrdersPending;
  const hasError = isAreaLeadersError || isAreaSpacesError || isFacilitatorsError || isOrdersError;

  const recentOrders = useMemo(() => {
    return [...orderIntents]
      .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
      .slice(0, 5);
  }, [orderIntents]);

  if (!hasPermission) {
    return (
      <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
          <StatusMessage tone="error">
            You do not have permission to view the area leader dashboard.
          </StatusMessage>
        </div>
      </main>
    );
  }
    return [...orderIntents]
      .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
      .slice(0, 5);
  }, [orderIntents]);

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
        <header>
          <Breadcrumb
            items={[
              { href: "/", label: "Dashboard" },
              { label: "Area Leader dashboard" },
            ]}
          />
          <h1 className="mt-2 text-3xl font-bold tracking-tight">Area Leader dashboard</h1>
          <p className="mt-2 max-w-2xl text-base text-muted-foreground">
            Overview of your performance, team, and activity.
          </p>
        </header>

        {isLoading ? (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Skeleton className="h-28" />
            <Skeleton className="h-28" />
            <Skeleton className="h-28" />
            <Skeleton className="h-28" />
          </div>
        ) : hasError ? (
          <StatusMessage tone="error">
            {areaLeadersErrorMessage ?? areaSpacesErrorMessage ?? facilitatorsErrorMessage ?? ordersErrorMessage ?? "Unable to load dashboard data."}
          </StatusMessage>
        ) : (
          <>
            <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <Card className="flex items-center gap-4">
                <div className="rounded-full bg-accent/10 p-3 text-accent">
                  <Users className="size-6" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Area Leaders</p>
                  <p className="text-2xl font-bold">{areaLeaders.length}</p>
                </div>
              </Card>
              <Card className="flex items-center gap-4">
                <div className="rounded-full bg-success/10 p-3 text-success">
                  <BarChart3 className="size-6" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Area Spaces</p>
                  <p className="text-2xl font-bold">{areaSpaces.length}</p>
                </div>
              </Card>
              <Card className="flex items-center gap-4">
                <div className="rounded-full bg-info/10 p-3 text-info">
                  <Users className="size-6" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Facilitators</p>
                  <p className="text-2xl font-bold">{facilitators.length}</p>
                </div>
              </Card>
              <Card className="flex items-center gap-4">
                <div className="rounded-full bg-warning/10 p-3 text-warning">
                  <BarChart3 className="size-6" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Orders</p>
                  <p className="text-2xl font-bold">{orderIntents.length}</p>
                </div>
              </Card>
            </section>

            <section className="grid gap-6 lg:grid-cols-2">
              <Card>
                <h2 className="text-lg font-semibold">Recent orders</h2>
                <div className="mt-4">
                  {recentOrders.length === 0 ? (
                    <EmptyState
                      description="No orders yet."
                      icon={BarChart3}
                      title="No orders"
                    />
                  ) : (
                    <div className="flex flex-col gap-3">
                      {recentOrders.map((order) => (
                        <LinkButton
                          key={order.id}
                          href={`/order-intents/${order.id}`}
                          variant="outline"
                        >
                          <div className="flex items-center justify-between gap-4">
                            <div className="flex items-center gap-3">
                              <span className="font-semibold">Order #{order.id}</span>
                            </div>
                            <span className="text-sm text-muted-foreground">
                              Customer #{order.customerId}
                            </span>
                          </div>
                        </LinkButton>
                      ))}
                    </div>
                  )}
                </div>
              </Card>

              <Card>
                <h2 className="text-lg font-semibold">Quick actions</h2>
                <div className="mt-4 flex flex-col gap-3">
                  <LinkButton href="/area-leader/area-spaces" variant="outline">
                    Manage area spaces
                  </LinkButton>
                  <LinkButton href="/area-leader/facilitators" variant="outline">
                    View facilitators
                  </LinkButton>
                  <LinkButton href="/area-leader/orders" variant="outline">
                    Review orders
                  </LinkButton>
                </div>
              </Card>
            </section>
          </>
        )}
      </div>
    </main>
  );
};


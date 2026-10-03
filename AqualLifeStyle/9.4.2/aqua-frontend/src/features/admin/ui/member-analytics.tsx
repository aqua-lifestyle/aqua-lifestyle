"use client";

import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from "recharts";

import { Badge, Card } from "@/src/shared/ui";
import type { AdminDashboardData } from "../model/dashboard";

type MemberAnalyticsProps = {
  members: AdminDashboardData["members"];
};

const colors = ["#2563eb", "#7c3aed", "#0891b2", "#64748b"];

const formatDate = (value: string | null) =>
  value
    ? new Intl.DateTimeFormat("en-ZA", { dateStyle: "medium" }).format(new Date(value))
    : "Date unavailable";

export const MemberAnalytics = ({ members }: MemberAnalyticsProps) => (
  <Card className="overflow-hidden p-0">
    <div className="border-b border-border p-5">
      <h2 className="text-lg font-semibold">Customer account analytics</h2>
      <p className="text-sm text-muted-foreground">Membership-plan distribution and newest customer accounts.</p>
    </div>
    <div className="grid gap-6 p-5 md:grid-cols-[minmax(0,1fr)_minmax(0,1.2fr)]">
      <div className="min-w-0">
        <div className="grid grid-cols-2 gap-3">
          <div className="flex-1 rounded-lg bg-success/10 p-3">
            <p className="text-xs font-medium uppercase tracking-wide text-success">Active</p>
            <p className="mt-1 break-words text-2xl font-bold tabular-nums">{members.active}</p>
          </div>
          <div className="flex-1 rounded-lg bg-muted p-3">
            <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Inactive</p>
            <p className="mt-1 break-words text-2xl font-bold tabular-nums">{members.inactive}</p>
          </div>
        </div>
        <div aria-label="Customer accounts by membership plan" className="mt-3 h-48 min-w-0">
          {members.byTier.length > 0 ? (
            <ResponsiveContainer height="100%" width="100%">
              <PieChart>
                <Pie data={members.byTier} dataKey="value" innerRadius={48} nameKey="name" outerRadius={76} paddingAngle={3}>
                  {members.byTier.map((tier, index) => (
                    <Cell fill={colors[index % colors.length]} key={tier.name} stroke="var(--card)" strokeWidth={2} />
                  ))}
                </Pie>
                <Tooltip contentStyle={{ background: "var(--card)", borderColor: "var(--control-border)", borderRadius: "0.625rem", color: "var(--foreground)" }} itemStyle={{ color: "var(--foreground)" }} />
              </PieChart>
            </ResponsiveContainer>
          ) : (
            <div className="flex h-full items-center justify-center text-sm text-muted-foreground">No tier data available.</div>
          )}
        </div>
        {members.byTier.length > 0 ? <dl className="mt-2 space-y-2 text-sm">
          {members.byTier.map((tier, index) => <div className="flex items-start justify-between gap-2" key={tier.name}>
            <dt className="flex min-w-0 items-start gap-2 wrap-anywhere"><span aria-hidden="true" className="mt-1.5 size-2.5 shrink-0 rounded-sm" style={{ background: colors[index % colors.length] }} />{tier.name}</dt>
            <dd className="shrink-0 font-semibold tabular-nums">{tier.value}</dd>
          </div>)}
        </dl> : null}
      </div>
      <div className="min-w-0">
        <h3 className="text-sm font-semibold">Recent sign-ups</h3>
        {members.recent.length > 0 ? (
          <ul className="mt-3 divide-y divide-border">
            {members.recent.map((member) => (
              <li className="flex flex-wrap items-center justify-between gap-3 py-3 first:pt-0" key={member.id}>
                <div className="min-w-0">
                  <p className="break-words text-sm font-semibold">{member.name}</p>
                  <p className="text-xs text-muted-foreground">{formatDate(member.joinedAt)}</p>
                </div>
                <Badge tone="accent">{member.tier}</Badge>
              </li>
            ))}
          </ul>
        ) : (
          <p className="mt-4 text-sm text-muted-foreground">No customer accounts available.</p>
        )}
      </div>
    </div>
  </Card>
);

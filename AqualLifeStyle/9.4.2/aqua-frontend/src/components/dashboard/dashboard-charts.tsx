"use client";

import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";

import { Card } from "@/src/shared/ui";

type MembershipChartData = {
  count: number;
  name: string;
};

type EnquiryChartData = {
  name: string;
  value: number;
};

type DashboardChartsProps = {
  enquiryData: EnquiryChartData[];
  membershipData: MembershipChartData[];
};

const membershipColors = ["#3b82f6", "#22c55e", "#eab308", "#ef4444"];
const enquiryColors = ["#3b82f6", "#f59e0b", "#22c55e"];

export const DashboardCharts = ({
  enquiryData,
  membershipData,
}: DashboardChartsProps) => {
  return (
    <div className="grid gap-4 lg:grid-cols-2">
      <Card className="flex flex-col">
        <h3 className="text-lg font-semibold">Membership tier mix</h3>
        <p className="text-sm text-muted-foreground">
          Distribution of active members across club tiers.
        </p>
        <div className="mt-4 h-64 w-full">
          <ResponsiveContainer height="100%" width="100%">
            <BarChart data={membershipData}>
              <CartesianGrid strokeDasharray="3 3" opacity={0.3} />
              <XAxis dataKey="name" tick={{ fontSize: 12 }} />
              <YAxis allowDecimals={false} tick={{ fontSize: 12 }} />
              <Tooltip
                contentStyle={{
                  borderRadius: "0.5rem",
                }}
              />
              <Bar dataKey="count" radius={[4, 4, 0, 0]}>
                {membershipData.map((entry, index) => (
                  <Cell
                    key={`cell-${entry.name}`}
                    fill={membershipColors[index % membershipColors.length]}
                  />
                ))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </div>
      </Card>

      <Card className="flex flex-col">
        <h3 className="text-lg font-semibold">Enquiry pipeline</h3>
        <p className="text-sm text-muted-foreground">
          Current enquiries by status.
        </p>
        <div className="mt-4 h-64 w-full">
          <ResponsiveContainer height="100%" width="100%">
            <PieChart>
              <Pie
                cx="50%"
                cy="50%"
                data={enquiryData}
                dataKey="value"
                innerRadius={50}
                nameKey="name"
                outerRadius={80}
                paddingAngle={2}
              >
                {enquiryData.map((entry, index) => (
                  <Cell
                    key={`slice-${entry.name}`}
                    fill={enquiryColors[index % enquiryColors.length]}
                  />
                ))}
              </Pie>
              <Tooltip
                contentStyle={{
                  borderRadius: "0.5rem",
                }}
              />
              <Legend verticalAlign="bottom" />
            </PieChart>
          </ResponsiveContainer>
        </div>
      </Card>
    </div>
  );
};

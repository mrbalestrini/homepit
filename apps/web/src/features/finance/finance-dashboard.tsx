"use client";

import { HomePitAuth } from "@/features/workspace/homepit-auth";
import { FinanceDashboardWorkspace } from "./finance-dashboard-workspace";
import { useFinanceDashboard } from "./use-finance-dashboard";

export function FinanceDashboard() {
  const dashboard = useFinanceDashboard();

  if (!dashboard.session) {
    return <HomePitAuth onAuthenticated={dashboard.handleAuthenticated} />;
  }

  return <FinanceDashboardWorkspace dashboard={dashboard} />;
}

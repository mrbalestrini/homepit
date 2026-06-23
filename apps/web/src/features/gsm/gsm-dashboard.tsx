"use client";

import { HomePitAuth } from "@/features/workspace/homepit-auth";
import { GsmDashboardWorkspace } from "./gsm-dashboard-workspace";
import { useGsmDashboard } from "./use-gsm-dashboard";

export function GsmDashboard() {
  const dashboard = useGsmDashboard();

  if (!dashboard.session) {
    return <HomePitAuth onAuthenticated={dashboard.handleAuthenticated} />;
  }

  return <GsmDashboardWorkspace dashboard={dashboard} />;
}

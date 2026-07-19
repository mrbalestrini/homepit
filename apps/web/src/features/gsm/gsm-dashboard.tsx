"use client";

import { AccountStateGate } from "@/features/workspace/account-state-gate";
import { OrganizaClubAuth } from "@/features/workspace/organiza-club-auth";
import { GsmDashboardWorkspace } from "./gsm-dashboard-workspace";
import { useGsmDashboard } from "./use-gsm-dashboard";

export function GsmDashboard() {
  const dashboard = useGsmDashboard();

  if (!dashboard.session) {
    return <OrganizaClubAuth onAuthenticated={dashboard.handleAuthenticated} />;
  }

  return (
    <AccountStateGate session={dashboard.session}>
      <GsmDashboardWorkspace dashboard={dashboard} />
    </AccountStateGate>
  );
}

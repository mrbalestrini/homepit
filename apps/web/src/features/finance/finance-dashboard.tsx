"use client";

import { AccountStateGate } from "@/features/workspace/account-state-gate";
import { OrganizaClubAuth } from "@/features/workspace/organiza-club-auth";
import { FinanceDashboardWorkspace } from "./finance-dashboard-workspace";
import { useFinanceDashboard } from "./use-finance-dashboard";

export function FinanceDashboard() {
  const dashboard = useFinanceDashboard();

  if (!dashboard.session) {
    return <OrganizaClubAuth onAuthenticated={dashboard.handleAuthenticated} />;
  }

  return (
    <AccountStateGate session={dashboard.session}>
      <FinanceDashboardWorkspace dashboard={dashboard} />
    </AccountStateGate>
  );
}

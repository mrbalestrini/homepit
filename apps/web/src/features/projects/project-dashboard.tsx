"use client";

import { AccountStateGate } from "@/features/workspace/account-state-gate";
import { ProjectDashboardAuth } from "./project-dashboard-auth";
import { ProjectDashboardWorkspace } from "./project-dashboard-workspace";
import { useProjectDashboard } from "./use-project-dashboard";

export function ProjectDashboard() {
  const dashboard = useProjectDashboard();

  if (!dashboard.session) {
    return <ProjectDashboardAuth onAuthenticated={dashboard.handleAuthenticated} />;
  }

  return (
    <AccountStateGate session={dashboard.session}>
      <ProjectDashboardWorkspace dashboard={dashboard} />
    </AccountStateGate>
  );
}

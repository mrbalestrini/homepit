import type { ActivityStatus, Priority } from "@/lib/api";

export type ActiveModal = "household" | "universe" | "project" | "activity" | "share" | null;

export type AppTheme = "cozy" | "earthy" | "dark";

export type ProjectViewMode = "list" | "kanban";

export type ActivitySortState = "priority" | "size" | "project" | "responsible" | "title";

export type ActivityStatusFilter = ActivityStatus | "all";

export type ActivityPriorityFilter = Priority | "all";

export type ActivityFilterState = {
  search: string;
  status: ActivityStatusFilter;
  priority: ActivityPriorityFilter;
  responsibleMemberId: "all" | string;
  sort: ActivitySortState;
};

export type ActivityFormInput = {
  projectId: string;
  title: string;
  description?: string;
  status: ActivityStatus;
  priority: Priority;
  size?: number;
  responsibleMemberId?: string;
};

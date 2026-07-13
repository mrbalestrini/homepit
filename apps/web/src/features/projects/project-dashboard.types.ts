import type { ActivityStatus, Priority } from "@/lib/api";

export type ActiveModal = "household" | "universe" | "project" | "activity" | "share" | "effort" | null;

export type AppTheme = "cozy" | "earthy" | "dark";

export type ProjectViewMode = "list" | "kanban";

export type ActivitySortState = "priority" | "size" | "project" | "responsible" | "title" | "relevance";

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
  dueDate: string;
  status: ActivityStatus;
  priority: Priority;
  size?: number;
  responsibleMemberId?: string;
  imageFile?: File | null;
  removeImage?: boolean;
};

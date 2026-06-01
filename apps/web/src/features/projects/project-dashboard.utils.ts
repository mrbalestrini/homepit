import type { Activity, Priority } from "@/lib/api";
import { priorityRank } from "./project-dashboard.constants";
import type { ActivitySortState } from "./project-dashboard.types";

export function getInitials(name: string) {
  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("");
}

export function formatDateTime(value: string) {
  return new Intl.DateTimeFormat("pt-BR", {
    day: "2-digit",
    month: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date(value));
}

export function getPriorityVariant(priority: Priority) {
  if (priority === "Urgente" || priority === "Alta") {
    return "danger" as const;
  }

  if (priority === "Media") {
    return "warning" as const;
  }

  return "success" as const;
}

export function getErrorMessage(exception: unknown, fallback: string) {
  return exception instanceof Error ? exception.message : fallback;
}

export function getStatusIndex(status: Activity["status"]) {
  switch (status) {
    case "NaoIniciada":
      return 0;
    case "EmAndamento":
      return 1;
    case "Concluido":
      return 2;
    default:
      return 0;
  }
}

export function sortActivities(items: Activity[], sort: ActivitySortState) {
  return [...items].sort((left, right) => {
    const priorityDifference = priorityRank[left.priority] - priorityRank[right.priority];
    const statusDifference = getStatusIndex(left.status) - getStatusIndex(right.status);

    switch (sort) {
      case "project":
        return (
          left.universeName.localeCompare(right.universeName) ||
          left.projectName.localeCompare(right.projectName) ||
          priorityDifference ||
          left.title.localeCompare(right.title)
        );
      case "responsible":
        return (
          (left.responsibleName ?? "zzz").localeCompare(right.responsibleName ?? "zzz") ||
          priorityDifference ||
          left.title.localeCompare(right.title)
        );
      case "title":
        return left.title.localeCompare(right.title) || priorityDifference || statusDifference;
      case "priority":
      default:
        return priorityDifference || statusDifference || left.title.localeCompare(right.title);
    }
  });
}

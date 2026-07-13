import { describe, expect, it } from "vitest";
import type { Activity } from "@/lib/api";
import { activitySortOptions } from "./project-dashboard.constants";
import { formatDateOnly, sortActivities } from "./project-dashboard.utils";

function buildActivity(overrides: Partial<Activity> & Pick<Activity, "id" | "title">): Activity {
  return {
    id: overrides.id,
    projectId: "project-1",
    projectName: "Projeto Alfa",
    universeId: "universe-1",
    universeName: "Universo Alfa",
    universeImageUrl: null,
    universeHasImage: false,
    universeImageUpdatedAt: null,
    createdByMemberId: null,
    createdAt: "2026-06-20T12:00:00.000Z",
    title: overrides.title,
    description: null,
    hasImage: false,
    imageUpdatedAt: null,
    dueDate: null,
    status: "NaoIniciada",
    priority: "Media",
    size: null,
    responsibleMemberId: null,
    responsibleName: null,
    pendingCount: 0,
    commentCount: 0,
    canEdit: true,
    canDelete: true,
    ...overrides,
  };
}

describe("project dashboard activity sorting", () => {
  it("exposes size as a sort option", () => {
    expect(activitySortOptions).toEqual(
      expect.arrayContaining([
        { value: "size", label: "Esforço" },
        { value: "relevance", label: "Mais relevantes" },
      ]),
    );
  });

  it("sorts activities by size from smallest to largest and keeps missing values last", () => {
    const sorted = sortActivities(
      [
        buildActivity({ id: "a", title: "Maior", size: 8 }),
        buildActivity({ id: "b", title: "Sem tamanho", size: null }),
        buildActivity({ id: "c", title: "Zero", size: 0 }),
        buildActivity({ id: "d", title: "Menor", size: 3 }),
      ],
      "size",
    );

    expect(sorted.map((activity) => [activity.id, activity.size])).toEqual([
      ["c", 0],
      ["d", 3],
      ["a", 8],
      ["b", null],
    ]);
  });

  it("formats date-only values in UTC without timezone drift", () => {
    expect(formatDateOnly("2026-06-30")).toBe("30/06/2026");
    expect(formatDateOnly(null)).toBe("Sem prazo");
  });
});

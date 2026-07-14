import { act, renderHook, waitFor } from "@testing-library/react";
import { toast } from "sonner";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { Activity, AuthResponse, HouseholdMember, Project } from "@/lib/api";
import * as api from "@/lib/api";
import { readStoredActiveHouseholdId, storeActiveHouseholdId } from "@/lib/household-selection";
import { uiStorageKeys } from "./project-dashboard.constants";
import { useProjectDashboard } from "./use-project-dashboard";

vi.mock("@/lib/api", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api")>("@/lib/api");

  return {
    ...actual,
    apiFetch: vi.fn(),
    readSession: vi.fn(),
    subscribeToSessionChanges: vi.fn(() => () => undefined),
    storeSession: vi.fn(),
    clearSession: vi.fn(),
    updateStoredSession: vi.fn(),
  };
});

vi.mock("sonner", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

const mockedApiFetch = vi.mocked(api.apiFetch);
const mockedReadSession = vi.mocked(api.readSession);
const mockedSubscribeToSessionChanges = vi.mocked(api.subscribeToSessionChanges);
const mockedToast = vi.mocked(toast);

function buildSession(): AuthResponse {
  return {
    accessToken: "token-1",
    refreshToken: "refresh-1",
    expiresAt: "2026-06-10T12:00:00.000Z",
    user: {
      id: "user-1",
      email: "user@example.com",
      displayName: "Usuário Exemplo",
      systemRole: "User",
      hasProfilePhoto: false,
      profilePhotoUpdatedAt: null,
    },
    households: [
      {
        id: "household-1",
        name: "Casa principal",
        role: "Owner",
      },
    ],
  };
}

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
    description: "Descrição de apoio.",
    hasImage: false,
    imageUpdatedAt: null,
    dueDate: null,
    completedAt: null,
    status: "NaoIniciada",
    priority: "Media",
    size: 3,
    responsibleMemberId: null,
    responsibleName: null,
    pendingCount: 2,
    commentCount: 4,
    canEdit: true,
    canDelete: true,
    ...overrides,
  };
}

function buildMember(overrides: Partial<HouseholdMember> & Pick<HouseholdMember, "id" | "userId" | "displayName">): HouseholdMember {
  return {
    id: overrides.id,
    userId: overrides.userId,
    displayName: overrides.displayName,
    email: "member@homepit.dev",
    phoneNumber: null,
    hasProfilePhoto: false,
    profilePhotoUpdatedAt: null,
    role: "Member",
    isCurrentUser: false,
    ...overrides,
  };
}

function buildProject(overrides: Partial<Project> & Pick<Project, "id" | "name">): Project {
  return {
    id: overrides.id,
    universeId: "universe-1",
    universeName: "Universo Alfa",
    universeImageUrl: null,
    universeHasImage: false,
    universeImageUpdatedAt: null,
    name: overrides.name,
    createdByMemberId: null,
    activityCount: 1,
    isOutOfPlan: false,
    canEdit: true,
    canDelete: true,
    ...overrides,
  };
}

describe("useProjectDashboard activity status optimism", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.localStorage.clear();
  });

  it("moves a kanban card immediately and keeps the server response as the source of truth on success", async () => {
    const session = buildSession();
    const activity = buildActivity({ id: "activity-1", title: "Mover card" });
    let resolveStatusUpdate!: (value: Activity) => void;
    const statusUpdatePromise = new Promise<Activity>((resolve) => {
      resolveStatusUpdate = resolve;
    });

    mockedReadSession.mockReturnValue(session);
    mockedSubscribeToSessionChanges.mockReturnValue(() => undefined);
    mockedApiFetch.mockImplementation(async (path: string) => {
      if (path === "/api/universes" || path === "/api/projects" || path === "/api/households/members") {
        return [];
      }

      if (path === "/api/activities") {
        return [activity];
      }

      if (path === `/api/activities/${activity.id}/status`) {
        return statusUpdatePromise;
      }

      throw new Error(`Unexpected API path: ${path}`);
    });

    const { result } = renderHook(() => useProjectDashboard());

    await waitFor(() => expect(result.current.activities).toHaveLength(1));

    let mutationPromise: Promise<void> | undefined;
    act(() => {
      mutationPromise = result.current.updateActivityStatusOptimistic(activity, "EmAndamento");
    });

    expect(result.current.activities.find((item) => item.id === activity.id)?.status).toBe("EmAndamento");
    expect(result.current.groupedActivities.find((group) => group.status === "EmAndamento")?.items).toHaveLength(1);
    expect(result.current.groupedActivities.find((group) => group.status === "NaoIniciada")?.items).toHaveLength(0);
    expect(mockedToast.success).not.toHaveBeenCalled();
    expect(mockedToast.error).not.toHaveBeenCalled();

    resolveStatusUpdate({
      ...activity,
      status: "EmAndamento",
      commentCount: 5,
    });

    await act(async () => {
      await mutationPromise;
    });

    expect(result.current.activities.find((item) => item.id === activity.id)?.status).toBe("EmAndamento");
    expect(result.current.activities.find((item) => item.id === activity.id)?.commentCount).toBe(5);
    expect(mockedToast.success).not.toHaveBeenCalled();
    expect(mockedToast.error).not.toHaveBeenCalled();
  });

  it("restores the previous card position and notifies the user when the kanban update fails", async () => {
    const session = buildSession();
    const activity = buildActivity({ id: "activity-1", title: "Mover card" });
    let rejectStatusUpdate!: (reason?: unknown) => void;
    const statusUpdatePromise = new Promise<Activity>((_resolve, reject) => {
      rejectStatusUpdate = reject;
    });

    mockedReadSession.mockReturnValue(session);
    mockedSubscribeToSessionChanges.mockReturnValue(() => undefined);
    mockedApiFetch.mockImplementation(async (path: string) => {
      if (path === "/api/universes" || path === "/api/projects" || path === "/api/households/members") {
        return [];
      }

      if (path === "/api/activities") {
        return [activity];
      }

      if (path === `/api/activities/${activity.id}/status`) {
        return statusUpdatePromise;
      }

      throw new Error(`Unexpected API path: ${path}`);
    });

    const { result } = renderHook(() => useProjectDashboard());

    await waitFor(() => expect(result.current.activities).toHaveLength(1));

    let mutationPromise: Promise<void> | undefined;
    act(() => {
      mutationPromise = result.current.updateActivityStatusOptimistic(activity, "EmAndamento");
    });

    expect(result.current.activities.find((item) => item.id === activity.id)?.status).toBe("EmAndamento");
    expect(result.current.groupedActivities.find((group) => group.status === "EmAndamento")?.items).toHaveLength(1);

    rejectStatusUpdate(new Error("Falha simulada"));

    await act(async () => {
      await mutationPromise;
    });

    expect(result.current.activities.find((item) => item.id === activity.id)?.status).toBe("NaoIniciada");
    expect(result.current.groupedActivities.find((group) => group.status === "NaoIniciada")?.items).toHaveLength(1);
    expect(result.current.groupedActivities.find((group) => group.status === "EmAndamento")?.items).toHaveLength(0);
    expect(mockedToast.error).toHaveBeenCalledWith("Falha simulada");
    expect(mockedToast.success).not.toHaveBeenCalled();
  });

  it("keeps project counts limited to open activities when an activity is completed", async () => {
    const session = buildSession();
    const activity = buildActivity({ id: "activity-1", title: "Fechar card" });
    const project = buildProject({ id: "project-1", name: "Projeto Alfa" });
    let resolveStatusUpdate!: (value: Activity) => void;
    const statusUpdatePromise = new Promise<Activity>((resolve) => {
      resolveStatusUpdate = resolve;
    });

    mockedReadSession.mockReturnValue(session);
    mockedSubscribeToSessionChanges.mockReturnValue(() => undefined);
    mockedApiFetch.mockImplementation(async (path: string) => {
      if (path === "/api/universes" || path === "/api/households/members") {
        return [];
      }

      if (path === "/api/projects") {
        return [project];
      }

      if (path === "/api/activities") {
        return [activity];
      }

      if (path === `/api/activities/${activity.id}/status`) {
        return statusUpdatePromise;
      }

      throw new Error(`Unexpected API path: ${path}`);
    });

    const { result } = renderHook(() => useProjectDashboard());

    await waitFor(() => expect(result.current.projects).toHaveLength(1));

    let mutationPromise: Promise<void> | undefined;
    act(() => {
      mutationPromise = result.current.updateActivityStatusOptimistic(activity, "Concluido");
    });

    expect(result.current.activities.find((item) => item.id === activity.id)?.status).toBe("Concluido");
    expect(result.current.projects.find((item) => item.id === project.id)?.activityCount).toBe(0);

    resolveStatusUpdate({
      ...activity,
      status: "Concluido",
    });

    await act(async () => {
      await mutationPromise;
    });

    expect(result.current.projects.find((item) => item.id === project.id)?.activityCount).toBe(0);
  });

  it("assigns an activity to the logged-in member with the quick action", async () => {
    const session = buildSession();
    const activity = buildActivity({ id: "activity-assign", title: "Atribuir rápido" });
    const currentMember = buildMember({
      id: "member-1",
      userId: session.user.id,
      displayName: session.user.displayName,
      isCurrentUser: true,
    });

    mockedReadSession.mockReturnValue(session);
    mockedSubscribeToSessionChanges.mockReturnValue(() => undefined);
    mockedApiFetch.mockImplementation(async (path: string) => {
      if (path === "/api/universes" || path === "/api/projects") {
        return [];
      }

      if (path === "/api/activities") {
        return [activity];
      }

      if (path === "/api/households/members") {
        return [currentMember];
      }

      if (path === `/api/activities/${activity.id}`) {
        return {
          ...activity,
          responsibleMemberId: currentMember.id,
          responsibleName: currentMember.displayName,
        };
      }

      throw new Error(`Unexpected API path: ${path}`);
    });

    const { result } = renderHook(() => useProjectDashboard());

    await waitFor(() => expect(result.current.activities).toHaveLength(1));
    await waitFor(() => expect(result.current.canAssignActivityToMe(activity)).toBe(true));

    await act(async () => {
      await result.current.assignActivityToMe(activity);
    });

    const updateCall = mockedApiFetch.mock.calls.find(([path]) => path === `/api/activities/${activity.id}`);
    expect(updateCall).toBeTruthy();
    expect(updateCall?.[1]).toEqual(
      expect.objectContaining({
        method: "PUT",
        token: session.accessToken,
        householdId: "household-1",
      }),
    );
    expect(JSON.parse(String((updateCall?.[1] as { body?: BodyInit } | undefined)?.body))).toMatchObject({
      projectId: activity.projectId,
      title: activity.title,
      responsibleMemberId: currentMember.id,
    });
    expect(result.current.activities.find((item) => item.id === activity.id)?.responsibleMemberId).toBe(currentMember.id);
    expect(mockedToast.success).toHaveBeenCalledWith("Atividade atribuída a você.");
  });
});

describe("useProjectDashboard household selection persistence", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.localStorage.clear();
  });

  it("restores the stored household and persists later changes for the same user", async () => {
    const session = {
      ...buildSession(),
      households: [
        {
          id: "household-1",
          name: "Casa A",
          role: "Owner" as const,
        },
        {
          id: "household-2",
          name: "Casa B",
          role: "Admin" as const,
        },
      ],
    };

    storeActiveHouseholdId(session.user.id, "household-2");
    mockedReadSession.mockReturnValue(session);
    mockedSubscribeToSessionChanges.mockReturnValue(() => undefined);
    mockedApiFetch.mockImplementation(async (path: string) => {
      if (path === "/api/universes" || path === "/api/projects" || path === "/api/activities" || path === "/api/households/members") {
        return [];
      }

      throw new Error(`Unexpected API path: ${path}`);
    });

    const { result } = renderHook(() => useProjectDashboard());

    await waitFor(() => expect(result.current.activeHouseholdId).toBe("household-2"));

    act(() => {
      result.current.handleHouseholdChange("household-1");
    });

    await waitFor(() => expect(readStoredActiveHouseholdId(session.user.id)).toBe("household-1"));
  });

  it("clears an invalid stored household and falls back to the last available one", async () => {
    const session = {
      ...buildSession(),
      households: [
        {
          id: "household-1",
          name: "Casa A",
          role: "Owner" as const,
        },
        {
          id: "household-2",
          name: "Casa B",
          role: "Member" as const,
        },
      ],
    };

    storeActiveHouseholdId(session.user.id, "household-missing");
    mockedReadSession.mockReturnValue(session);
    mockedSubscribeToSessionChanges.mockReturnValue(() => undefined);
    mockedApiFetch.mockImplementation(async (path: string) => {
      if (path === "/api/universes" || path === "/api/projects" || path === "/api/activities" || path === "/api/households/members") {
        return [];
      }

      throw new Error(`Unexpected API path: ${path}`);
    });

    const { result } = renderHook(() => useProjectDashboard());

    await waitFor(() => expect(result.current.activeHouseholdId).toBe("household-2"));
    await waitFor(() => expect(readStoredActiveHouseholdId(session.user.id)).toBe("household-2"));
  });
});

describe("useProjectDashboard sort persistence", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.localStorage.clear();
  });

  it("restores the stored sort and keeps the localStorage value in sync with changes", async () => {
    const session = buildSession();

    window.localStorage.setItem(uiStorageKeys.projectActivitySort, "size");
    mockedReadSession.mockReturnValue(session);
    mockedSubscribeToSessionChanges.mockReturnValue(() => undefined);
    mockedApiFetch.mockImplementation(async (path: string) => {
      if (
        path === "/api/universes" ||
        path === "/api/projects" ||
        path === "/api/activities" ||
        path === "/api/households/members"
      ) {
        return [];
      }

      throw new Error(`Unexpected API path: ${path}`);
    });

    const { result } = renderHook(() => useProjectDashboard());

    await waitFor(() => expect(result.current.filters.sort).toBe("size"));

    act(() => {
      result.current.updateFilter("sort", "title");
    });

    await waitFor(() => expect(window.localStorage.getItem(uiStorageKeys.projectActivitySort)).toBe("title"));

    act(() => {
      result.current.resetFilters();
    });

    await waitFor(() => expect(result.current.filters.sort).toBe("priority"));
    await waitFor(() => expect(window.localStorage.getItem(uiStorageKeys.projectActivitySort)).toBe("priority"));
  });
});

describe("useProjectDashboard old completed activities", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.localStorage.clear();
  });

  it("hides old completed activities, then reveals them and signals a matching search", async () => {
    const session = buildSession();
    const oldActivity = buildActivity({
      id: "activity-old",
      title: "Revisar atividade antiga",
      status: "Concluido",
      completedAt: new Date(Date.now() - 31 * 24 * 60 * 60 * 1000).toISOString(),
    });

    mockedReadSession.mockReturnValue(session);
    mockedSubscribeToSessionChanges.mockReturnValue(() => undefined);
    mockedApiFetch.mockImplementation(async (path: string) => {
      if (
        path === "/api/universes" ||
        path === "/api/projects" ||
        path === "/api/households/members"
      ) {
        return [];
      }

      if (path === "/api/activities") {
        return [oldActivity];
      }

      throw new Error(`Unexpected API path: ${path}`);
    });

    const { result } = renderHook(() => useProjectDashboard());

    await waitFor(() => expect(result.current.activities).toHaveLength(1));
    expect(result.current.visibleActivities).toHaveLength(0);
    expect(result.current.hasOldCompletedActivities).toBe(true);

    act(() => {
      result.current.updateFilter("search", oldActivity.title);
    });

    await waitFor(() => expect(result.current.hasHiddenOldCompletedSearchMatch).toBe(true));
    expect(result.current.visibleActivities).toHaveLength(0);

    act(() => {
      result.current.setShowOldCompleted(true);
    });

    await waitFor(() => expect(result.current.visibleActivities).toHaveLength(1));
    expect(result.current.visibleActivities[0]?.id).toBe(oldActivity.id);
  });
});

describe("useProjectDashboard profile redirect", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.localStorage.clear();
  });

  it("redirects a regular user without households to /profile", async () => {
    const session = {
      ...buildSession(),
      households: [],
    };

    mockedReadSession.mockReturnValue(session);
    mockedSubscribeToSessionChanges.mockReturnValue(() => undefined);
    mockedApiFetch.mockImplementation(async (path: string) => {
      if (
        path === "/api/universes" ||
        path === "/api/projects" ||
        path === "/api/activities" ||
        path === "/api/households/members"
      ) {
        return [];
      }

      throw new Error(`Unexpected API path: ${path}`);
    });

    renderHook(() => useProjectDashboard());

    await waitFor(() =>
      expect((globalThis as any).__nextNavigationMock.replace).toHaveBeenCalledWith("/profile"),
    );
  });
});

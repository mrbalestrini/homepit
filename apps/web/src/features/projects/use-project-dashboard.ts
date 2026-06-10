"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { toast } from "sonner";
import {
  Activity,
  ActivityComment,
  AuthResponse,
  Household,
  HouseholdMember,
  Project,
  Universe,
  apiFetch,
  clearSession,
  readSession,
  storeSession,
  subscribeToSessionChanges,
  updateStoredSession,
} from "@/lib/api";
import { activityColumns, defaultActivityFilters, defaultAppTheme, uiStorageKeys } from "./project-dashboard.constants";
import type { ActiveModal, ActivityFilterState, ActivityFormInput, AppTheme, ProjectViewMode } from "./project-dashboard.types";
import { getErrorMessage, sortActivities } from "./project-dashboard.utils";

function isAppTheme(value: string | null): value is AppTheme {
  return value === "cozy" || value === "earthy" || value === "dark";
}

function applyDocumentTheme(theme: AppTheme) {
  document.documentElement.dataset.theme = theme;
}

function isOpenActivity(status: Activity["status"]) {
  return status !== "Concluido";
}

function updateProjectActivityCounts(
  projects: Project[],
  previousActivity: Pick<Activity, "projectId" | "status"> | null,
  nextActivity: Pick<Activity, "projectId" | "status"> | null,
) {
  return projects.map((project) => {
    let activityCount = project.activityCount;

    if (previousActivity?.projectId === project.id && isOpenActivity(previousActivity.status)) {
      activityCount -= 1;
    }

    if (nextActivity?.projectId === project.id && isOpenActivity(nextActivity.status)) {
      activityCount += 1;
    }

    return activityCount === project.activityCount
      ? project
      : { ...project, activityCount: Math.max(0, activityCount) };
  });
}

export function useProjectDashboard() {
  const [session, setSession] = useState<AuthResponse | null>(null);
  const [activeHouseholdId, setActiveHouseholdId] = useState("");
  const [universes, setUniverses] = useState<Universe[]>([]);
  const [projects, setProjects] = useState<Project[]>([]);
  const [activities, setActivities] = useState<Activity[]>([]);
  const [members, setMembers] = useState<HouseholdMember[]>([]);
  const [selectedUniverseId, setSelectedUniverseId] = useState("");
  const [selectedProjectId, setSelectedProjectId] = useState("");
  const [filters, setFilters] = useState<ActivityFilterState>(defaultActivityFilters);
  const [viewMode, setViewModeState] = useState<ProjectViewMode>("kanban");
  const [sidebarCollapsed, setSidebarCollapsedState] = useState(false);
  const [theme, setThemeState] = useState<AppTheme>(defaultAppTheme);
  const [activeModal, setActiveModal] = useState<ActiveModal>(null);
  const [editingHousehold, setEditingHousehold] = useState<Household | null>(null);
  const [editingUniverse, setEditingUniverse] = useState<Universe | null>(null);
  const [editingProject, setEditingProject] = useState<Project | null>(null);
  const [editingActivity, setEditingActivity] = useState<Activity | null>(null);
  const [activityDraftProjectId, setActivityDraftProjectId] = useState("");
  const [selectedActivity, setSelectedActivity] = useState<Activity | null>(null);
  const [activityComments, setActivityComments] = useState<ActivityComment[]>([]);
  const [commentsLoading, setCommentsLoading] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const sessionUserIdRef = useRef<string | null>(null);
  const activityStatusMutationVersionRef = useRef<Record<string, number>>({});

  const resetWorkspaceState = useCallback(() => {
    setUniverses([]);
    setProjects([]);
    setActivities([]);
    setMembers([]);
    setSelectedUniverseId("");
    setSelectedProjectId("");
    setSelectedActivity(null);
    setActivityComments([]);
    setEditingHousehold(null);
    setEditingUniverse(null);
    setEditingProject(null);
    setEditingActivity(null);
    setActivityDraftProjectId("");
    setActiveModal(null);
    setCommentsLoading(false);
    setLoading(false);
    setError(null);
    setFilters(defaultActivityFilters);
  }, []);

  const syncSession = useCallback(
    (nextSession: AuthResponse | null) => {
      const nextUserId = nextSession?.user.id ?? null;
      const userChanged = sessionUserIdRef.current !== nextUserId;
      sessionUserIdRef.current = nextUserId;

      if (!nextSession || userChanged) {
        resetWorkspaceState();
      }

      setSession(nextSession);
      setActiveHouseholdId((current) => {
        if (!nextSession) {
          return "";
        }

        return current && nextSession.households.some((household) => household.id === current)
          ? current
          : nextSession.households[0]?.id ?? "";
      });
      setLoading(Boolean(nextSession && nextSession.households.length > 0));
    },
    [resetWorkspaceState],
  );

  useEffect(() => {
    let cancelled = false;
    const savedViewMode = window.localStorage.getItem(uiStorageKeys.projectViewMode);
    const savedSidebarState = window.localStorage.getItem(uiStorageKeys.sidebarCollapsed);
    const savedTheme = window.localStorage.getItem(uiStorageKeys.theme);

    void Promise.resolve().then(() => {
      if (cancelled) {
        return;
      }

      syncSession(readSession());

      if (savedViewMode === "list" || savedViewMode === "kanban") {
        setViewModeState(savedViewMode);
      }

      if (savedSidebarState === "true" || savedSidebarState === "false") {
        setSidebarCollapsedState(savedSidebarState === "true");
      }

      if (isAppTheme(savedTheme)) {
        setThemeState(savedTheme);
        applyDocumentTheme(savedTheme);
      } else {
        applyDocumentTheme(defaultAppTheme);
      }
    });

    const unsubscribe = subscribeToSessionChanges(syncSession);

    return () => {
      cancelled = true;
      unsubscribe();
    };
  }, [syncSession]);

  useEffect(() => {
    applyDocumentTheme(theme);
  }, [theme]);

  const activeHousehold = useMemo(() => {
    return session?.households.find((household) => household.id === activeHouseholdId) ?? null;
  }, [activeHouseholdId, session?.households]);

  const canShareHousehold = activeHousehold?.role === "Owner" || activeHousehold?.role === "Admin";
  const canManageHousehold = activeHousehold?.role === "Owner";

  const filteredProjects = useMemo(() => {
    return selectedUniverseId
      ? projects.filter((project) => project.universeId === selectedUniverseId)
      : projects;
  }, [projects, selectedUniverseId]);

  const activityDialogProjects = useMemo(() => {
    if (!activityDraftProjectId) {
      return filteredProjects.length > 0 ? filteredProjects : projects;
    }

    return filteredProjects.some((project) => project.id === activityDraftProjectId)
      ? filteredProjects
      : projects;
  }, [activityDraftProjectId, filteredProjects, projects]);

  const visibleActivities = useMemo(() => {
    const normalizedSearch = filters.search.trim().toLowerCase();
    const scopedActivities = activities.filter((activity) => {
      const matchesUniverse = !selectedUniverseId || activity.universeId === selectedUniverseId;
      const matchesProject = !selectedProjectId || activity.projectId === selectedProjectId;

      return matchesUniverse && matchesProject;
    });

    const filteredActivities = scopedActivities.filter((activity) => {
      const matchesSearch =
        !normalizedSearch ||
        activity.title.toLowerCase().includes(normalizedSearch) ||
        activity.projectName.toLowerCase().includes(normalizedSearch) ||
        activity.universeName.toLowerCase().includes(normalizedSearch) ||
        (activity.description ?? "").toLowerCase().includes(normalizedSearch);

      const matchesStatus = filters.status === "all" || activity.status === filters.status;
      const matchesPriority = filters.priority === "all" || activity.priority === filters.priority;
      const matchesResponsible =
        filters.responsibleMemberId === "all" || activity.responsibleMemberId === filters.responsibleMemberId;

      return matchesSearch && matchesStatus && matchesPriority && matchesResponsible;
    });

    return sortActivities(filteredActivities, filters.sort);
  }, [activities, filters, selectedProjectId, selectedUniverseId]);

  const selectedScopeLabel = useMemo(() => {
    const project = projects.find((item) => item.id === selectedProjectId);
    if (project) {
      return project.name;
    }

    const universe = universes.find((item) => item.id === selectedUniverseId);
    return universe?.name ?? "Todos os projetos";
  }, [projects, selectedProjectId, selectedUniverseId, universes]);

  const selectedActivitySnapshot = selectedActivity
    ? activities.find((activity) => activity.id === selectedActivity.id) ?? selectedActivity
    : null;

  const groupedActivities = useMemo(() => {
    return activityColumns.map((column) => ({
      ...column,
      items: visibleActivities.filter((activity) => activity.status === column.status),
    }));
  }, [visibleActivities]);

  const setViewMode = useCallback((nextViewMode: ProjectViewMode) => {
    setViewModeState(nextViewMode);
    window.localStorage.setItem(uiStorageKeys.projectViewMode, nextViewMode);
  }, []);

  const setSidebarCollapsed = useCallback((collapsed: boolean) => {
    setSidebarCollapsedState(collapsed);
    window.localStorage.setItem(uiStorageKeys.sidebarCollapsed, String(collapsed));
  }, []);

  const setTheme = useCallback((nextTheme: AppTheme) => {
    setThemeState(nextTheme);
    applyDocumentTheme(nextTheme);
    window.localStorage.setItem(uiStorageKeys.theme, nextTheme);
  }, []);

  const updateFilter = useCallback(<T extends keyof ActivityFilterState>(key: T, value: ActivityFilterState[T]) => {
    setFilters((current) => ({ ...current, [key]: value }));
  }, []);

  const resetFilters = useCallback(() => {
    setFilters(defaultActivityFilters);
  }, []);

  const reportError = useCallback((exception: unknown, fallback: string) => {
    const message = getErrorMessage(exception, fallback);
    setError(message);
    toast.error(message);

    if (exception instanceof Error) {
      throw exception;
    }

    throw new Error(message);
  }, []);

  const loadWorkspace = useCallback(
    async (token = session?.accessToken, householdId = activeHouseholdId) => {
      if (!token || !householdId) {
        return;
      }

      setLoading(true);
      setError(null);
      try {
        const [nextUniverses, nextProjects, nextActivities, nextMembers] = await Promise.all([
          apiFetch<Universe[]>("/api/universes", { token, householdId }),
          apiFetch<Project[]>("/api/projects", { token, householdId }),
          apiFetch<Activity[]>("/api/activities", { token, householdId }),
          apiFetch<HouseholdMember[]>("/api/households/members", { token, householdId }),
        ]);

        setUniverses(nextUniverses);
        setProjects(nextProjects);
        setActivities(nextActivities);
        setMembers(nextMembers);
      } catch (exception) {
        setError(getErrorMessage(exception, "Falha ao carregar dados."));
      } finally {
        setLoading(false);
      }
    },
    [activeHouseholdId, session?.accessToken],
  );

  const loadComments = useCallback(
    async (activityId: string) => {
      if (!session || !activeHouseholdId) {
        return;
      }

      setCommentsLoading(true);
      try {
        const nextComments = await apiFetch<ActivityComment[]>(`/api/activities/${activityId}/comments`, {
          token: session.accessToken,
          householdId: activeHouseholdId,
        });
        setActivityComments(nextComments);
      } catch (exception) {
        setError(getErrorMessage(exception, "Falha ao carregar comentários."));
      } finally {
        setCommentsLoading(false);
      }
    },
    [activeHouseholdId, session],
  );

  const replaceActivityInState = useCallback((nextActivity: Activity) => {
    setActivities((current) => current.map((activity) => (activity.id === nextActivity.id ? nextActivity : activity)));
    setSelectedActivity((current) => (current?.id === nextActivity.id ? nextActivity : current));
  }, []);

  const restoreActivityInState = useCallback((previousActivity: Activity) => {
    setActivities((current) =>
      current.map((activity) => (activity.id === previousActivity.id ? previousActivity : activity)),
    );
    setSelectedActivity((current) => (current?.id === previousActivity.id ? previousActivity : current));
  }, []);

  useEffect(() => {
    if (!session || !activeHouseholdId) {
      return;
    }

    const timer = window.setTimeout(() => {
      void loadWorkspace(session.accessToken, activeHouseholdId);
    }, 0);

    return () => window.clearTimeout(timer);
  }, [session, activeHouseholdId, loadWorkspace]);

  const handleAuthenticated = useCallback((auth: AuthResponse) => {
    storeSession(auth);
    toast.success("Sessão iniciada com sucesso.");
  }, []);

  const handleHouseholdChange = useCallback((householdId: string) => {
    setLoading(true);
    setUniverses([]);
    setProjects([]);
    setActivities([]);
    setMembers([]);
    setActiveHouseholdId(householdId);
    setSelectedUniverseId("");
    setSelectedProjectId("");
    setActivityDraftProjectId("");
    setSelectedActivity(null);
    setActivityComments([]);
    setCommentsLoading(false);
    setError(null);
  }, []);

  const handleLogout = useCallback(() => {
    clearSession();
    toast.success("Sessão encerrada.");
  }, []);

  const updateSessionHouseholds = useCallback(
    (nextHouseholds: Household[], preferredHouseholdId?: string) => {
      const nextSession = updateStoredSession((currentSession) => ({
        ...currentSession,
        households: nextHouseholds,
      }));

      if (!nextSession) {
        return;
      }

      setSession(nextSession);

      const nextHouseholdId =
        preferredHouseholdId ??
        (activeHouseholdId && nextHouseholds.some((household) => household.id === activeHouseholdId)
          ? activeHouseholdId
          : nextHouseholds[0]?.id ?? "");

      setActiveHouseholdId(nextHouseholdId);
    },
    [activeHouseholdId],
  );

  const applyUpdatedUser = useCallback(
    (updatedUser: AuthResponse["user"]) => {
      const nextSession = updateStoredSession((currentSession) => ({
        ...currentSession,
        user: updatedUser,
      }));

      if (!nextSession) {
        return;
      }

      setSession(nextSession);
      setMembers((current) =>
        current.map((member) =>
          member.isCurrentUser
            ? {
                ...member,
                displayName: updatedUser.displayName,
                phoneNumber: updatedUser.phoneNumber ?? null,
                email: updatedUser.email,
              }
            : member,
        ),
      );
    },
    [],
  );

  const refreshHouseholds = useCallback(async () => {
    if (!session) {
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const nextHouseholds = await apiFetch<Household[]>("/api/households", {
        token: session.accessToken,
      });
      updateSessionHouseholds(nextHouseholds);
      toast.success("Casas atualizadas.");
    } catch (exception) {
      setError(getErrorMessage(exception, "Falha ao carregar casas."));
      toast.error(getErrorMessage(exception, "Falha ao carregar casas."));
    } finally {
      setLoading(false);
    }
  }, [session, updateSessionHouseholds]);

  async function createHousehold(name: string) {
    if (!session) {
      return;
    }

    try {
      const created = await apiFetch<Household>("/api/households", {
        method: "POST",
        token: session.accessToken,
        body: JSON.stringify({ name }),
      });
      updateSessionHouseholds(
        [...session.households, created].sort((a, b) => a.name.localeCompare(b.name)),
        created.id,
      );
      toast.success("Casa criada.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar a casa.");
    }
  }

  async function updateHousehold(householdId: string, name: string) {
    if (!session) {
      return;
    }

    try {
      const updated = await apiFetch<Household>(`/api/households/${householdId}`, {
        method: "PUT",
        token: session.accessToken,
        householdId,
        body: JSON.stringify({ name }),
      });
      updateSessionHouseholds(
        session.households
          .map((household) => (household.id === updated.id ? updated : household))
          .sort((a, b) => a.name.localeCompare(b.name)),
        updated.id,
      );
      toast.success("Casa atualizada.");
    } catch (exception) {
      reportError(exception, "Não foi possível salvar a casa.");
    }
  }

  async function deleteHousehold(household: Household) {
    if (!session || !window.confirm(`Excluir a casa "${household.name}" e todos os dados dela?`)) {
      return;
    }

    try {
      await apiFetch<void>(`/api/households/${household.id}`, {
        method: "DELETE",
        token: session.accessToken,
        householdId: household.id,
      });

      const nextHouseholds = session.households.filter((item) => item.id !== household.id);
      setUniverses([]);
      setProjects([]);
      setActivities([]);
      setMembers([]);
      setSelectedUniverseId("");
      setSelectedProjectId("");
      setSelectedActivity(null);
      setActivityComments([]);
      updateSessionHouseholds(nextHouseholds);
      toast.success("Casa excluída.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir a casa.");
    }
  }

  function openCreateHousehold() {
    setEditingHousehold(null);
    setActiveModal("household");
  }

  function openEditHousehold() {
    if (!activeHousehold) {
      return;
    }

    setEditingHousehold(activeHousehold);
    setActiveModal("household");
  }

  function openCreateUniverse() {
    setEditingUniverse(null);
    setActiveModal("universe");
  }

  function openEditUniverse(universe: Universe) {
    if (!universe.canEdit) {
      return;
    }

    setEditingUniverse(universe);
    setActiveModal("universe");
  }

  function openCreateProject(universeId?: string) {
    setEditingProject(null);
    if (universeId) {
      setSelectedUniverseId(universeId);
      setSelectedProjectId("");
    }
    setActiveModal("project");
  }

  function openEditProject(project: Project) {
    if (!project.canEdit) {
      return;
    }

    setEditingProject(project);
    setActiveModal("project");
  }

  function openCreateActivity(projectId?: string) {
    setEditingActivity(null);
    setActivityDraftProjectId(projectId ?? selectedProjectId);
    setActiveModal("activity");
  }

  function openShareHousehold() {
    setActiveModal("share");
  }

  function openEditActivity(activity: Activity) {
    if (!activity.canEdit) {
      return;
    }

    setActivityDraftProjectId("");
    setEditingActivity(activity);
    setActiveModal("activity");
  }

  function closeModal() {
    setActiveModal(null);
    setEditingHousehold(null);
    setEditingUniverse(null);
    setEditingProject(null);
    setEditingActivity(null);
    setActivityDraftProjectId("");
  }

  async function createUniverse(input: { name: string; imageFile?: File | null; removeImage?: boolean }) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      let created = await apiFetch<Universe>("/api/universes", {
        method: "POST",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify({ name: input.name, imageUrl: null }),
      });

      if (input.imageFile) {
        created = await uploadUniverseImage(created.id, input.imageFile);
      }

      setUniverses((current) => [...current, created].sort((a, b) => a.name.localeCompare(b.name)));
      setSelectedUniverseId(created.id);
      setSelectedProjectId("");
      toast.success("Universo criado.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar o universo.");
    }
  }

  async function updateUniverse(universeId: string, input: { name: string; imageFile?: File | null; removeImage?: boolean }) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      let updated = await apiFetch<Universe>(`/api/universes/${universeId}`, {
        method: "PUT",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify({ name: input.name, imageUrl: null }),
      });

      if (input.imageFile) {
        updated = await uploadUniverseImage(universeId, input.imageFile);
      } else if (input.removeImage) {
        updated = await deleteUniverseImage(universeId);
      }

      setUniverses((current) =>
        current
          .map((universe) => (universe.id === updated.id ? updated : universe))
          .sort((a, b) => a.name.localeCompare(b.name)),
      );
      setProjects((current) =>
        current.map((project) =>
          project.universeId === updated.id
            ? {
                ...project,
                universeName: updated.name,
                universeImageUrl: updated.imageUrl ?? null,
                universeHasImage: updated.hasImage,
                universeImageUpdatedAt: updated.imageUpdatedAt ?? null,
              }
            : project,
        ),
      );
      setActivities((current) =>
        current.map((activity) =>
          activity.universeId === updated.id
            ? {
                ...activity,
                universeName: updated.name,
                universeImageUrl: updated.imageUrl ?? null,
                universeHasImage: updated.hasImage,
                universeImageUpdatedAt: updated.imageUpdatedAt ?? null,
              }
            : activity,
        ),
      );
      toast.success("Universo atualizado.");
    } catch (exception) {
      reportError(exception, "Não foi possível salvar o universo.");
    }
  }

  async function deleteUniverse(universe: Universe) {
    if (
      !session ||
      !activeHouseholdId ||
      !universe.canDelete ||
      !window.confirm(`Excluir o universo "${universe.name}" e tudo dentro dele?`)
    ) {
      return;
    }

    try {
      await apiFetch<void>(`/api/universes/${universe.id}`, {
        method: "DELETE",
        token: session.accessToken,
        householdId: activeHouseholdId,
      });
      setUniverses((current) => current.filter((item) => item.id !== universe.id));
      setProjects((current) => current.filter((project) => project.universeId !== universe.id));
      setActivities((current) => current.filter((activity) => activity.universeId !== universe.id));
      setSelectedUniverseId((current) => (current === universe.id ? "" : current));
      setSelectedProjectId((current) => {
        const selectedProject = projects.find((project) => project.id === current);
        return selectedProject?.universeId === universe.id ? "" : current;
      });
      if (selectedActivity?.universeId === universe.id) {
        setSelectedActivity(null);
        setActivityComments([]);
      }
      toast.success("Universo excluído.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir o universo.");
    }
  }

  async function uploadUniverseImage(universeId: string, imageFile: File) {
    if (!session || !activeHouseholdId) {
      throw new Error("Sessão inválida para upload da imagem do universo.");
    }

    const formData = new FormData();
    formData.append("file", imageFile);

    return await apiFetch<Universe>(`/api/universes/${universeId}/image`, {
      method: "POST",
      token: session.accessToken,
      householdId: activeHouseholdId,
      body: formData,
    });
  }

  async function deleteUniverseImage(universeId: string) {
    if (!session || !activeHouseholdId) {
      throw new Error("Sessão inválida para remoção da imagem do universo.");
    }

    return await apiFetch<Universe>(`/api/universes/${universeId}/image`, {
      method: "DELETE",
      token: session.accessToken,
      householdId: activeHouseholdId,
    });
  }

  async function createProject(universeId: string, name: string) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      const created = await apiFetch<Project>("/api/projects", {
        method: "POST",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify({ universeId, name }),
      });
      setProjects((current) => [...current, created].sort((a, b) => a.name.localeCompare(b.name)));
      setUniverses((current) =>
        current.map((universe) =>
          universe.id === created.universeId
            ? { ...universe, projectCount: universe.projectCount + 1 }
            : universe,
        ),
      );
      setSelectedUniverseId(created.universeId);
      setSelectedProjectId(created.id);
      toast.success("Projeto criado.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar o projeto.");
    }
  }

  async function updateProject(projectId: string, universeId: string, name: string) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      const previousProject = projects.find((project) => project.id === projectId) ?? null;
      const updated = await apiFetch<Project>(`/api/projects/${projectId}`, {
        method: "PUT",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify({ universeId, name }),
      });

      setProjects((current) =>
        current
          .map((project) => (project.id === updated.id ? updated : project))
          .sort((a, b) => a.name.localeCompare(b.name)),
      );
      setUniverses((current) =>
        current.map((universe) => {
          if (!previousProject || previousProject.universeId === updated.universeId) {
            return universe;
          }

          if (universe.id === previousProject.universeId) {
            return { ...universe, projectCount: Math.max(0, universe.projectCount - 1) };
          }

          if (universe.id === updated.universeId) {
            return { ...universe, projectCount: universe.projectCount + 1 };
          }

          return universe;
        }),
      );
      setActivities((current) =>
        current.map((activity) =>
          activity.projectId === updated.id
            ? {
                ...activity,
                projectName: updated.name,
                universeId: updated.universeId,
                universeName: updated.universeName,
              }
            : activity,
        ),
      );
      setSelectedUniverseId(updated.universeId);
      setSelectedProjectId(updated.id);
      toast.success("Projeto atualizado.");
    } catch (exception) {
      reportError(exception, "Não foi possível salvar o projeto.");
    }
  }

  async function deleteProject(project: Project) {
    if (
      !session ||
      !activeHouseholdId ||
      !project.canDelete ||
      !window.confirm(`Excluir o projeto "${project.name}" e suas atividades?`)
    ) {
      return;
    }

    try {
      await apiFetch<void>(`/api/projects/${project.id}`, {
        method: "DELETE",
        token: session.accessToken,
        householdId: activeHouseholdId,
      });
      setProjects((current) => current.filter((item) => item.id !== project.id));
      setUniverses((current) =>
        current.map((universe) =>
          universe.id === project.universeId
            ? { ...universe, projectCount: Math.max(0, universe.projectCount - 1) }
            : universe,
        ),
      );
      setActivities((current) => current.filter((activity) => activity.projectId !== project.id));
      setSelectedProjectId((current) => (current === project.id ? "" : current));
      if (selectedActivity?.projectId === project.id) {
        setSelectedActivity(null);
        setActivityComments([]);
      }
      toast.success("Projeto excluído.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir o projeto.");
    }
  }

  async function createActivity(input: ActivityFormInput) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      const created = await apiFetch<Activity>("/api/activities", {
        method: "POST",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify({
          ...input,
          description: input.description || null,
          size: input.size ?? null,
          responsibleMemberId: input.responsibleMemberId || null,
        }),
      });
      setActivities((current) => [...current, created]);
      setProjects((current) => updateProjectActivityCounts(current, null, created));
      setSelectedUniverseId(created.universeId);
      setSelectedProjectId(created.projectId);
      toast.success("Atividade criada.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar a atividade.");
    }
  }

  async function updateActivity(activityId: string, input: ActivityFormInput) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      const previousActivity = activities.find((activity) => activity.id === activityId) ?? null;
      const updated = await apiFetch<Activity>(`/api/activities/${activityId}`, {
        method: "PUT",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify({
          ...input,
          description: input.description || null,
          size: input.size ?? null,
          responsibleMemberId: input.responsibleMemberId || null,
        }),
      });

      setActivities((current) => current.map((activity) => (activity.id === updated.id ? updated : activity)));
      setProjects((current) => updateProjectActivityCounts(current, previousActivity, updated));
      setSelectedUniverseId(updated.universeId);
      setSelectedProjectId(updated.projectId);
      setSelectedActivity((current) => (current?.id === updated.id ? updated : current));
      toast.success("Atividade atualizada.");
    } catch (exception) {
      reportError(exception, "Não foi possível salvar a atividade.");
    }
  }

  async function deleteActivity(activity: Activity) {
    if (
      !session ||
      !activeHouseholdId ||
      !activity.canDelete ||
      !window.confirm(`Excluir a atividade "${activity.title}"?`)
    ) {
      return;
    }

    try {
      await apiFetch<void>(`/api/activities/${activity.id}`, {
        method: "DELETE",
        token: session.accessToken,
        householdId: activeHouseholdId,
      });
      setActivities((current) => current.filter((item) => item.id !== activity.id));
      setProjects((current) => updateProjectActivityCounts(current, activity, null));
      if (selectedActivity?.id === activity.id) {
        setSelectedActivity(null);
        setActivityComments([]);
      }
      toast.success("Atividade excluída.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir a atividade.");
    }
  }

  async function shareHousehold(input: { email: string; role: "Admin" | "Member" }) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      const created = await apiFetch<HouseholdMember>("/api/households/share", {
        method: "POST",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify(input),
      });
      setMembers((current) => [...current, created].sort((a, b) => a.displayName.localeCompare(b.displayName)));
      toast.success("Pessoa adicionada à casa.");
    } catch (exception) {
      reportError(exception, "Não foi possível compartilhar a casa.");
    }
  }

  async function updateHouseholdMember(memberId: string, role: HouseholdMember["role"]) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      const updated = await apiFetch<HouseholdMember>(`/api/households/members/${memberId}`, {
        method: "PUT",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify({ role }),
      });
      setMembers((current) =>
        current
          .map((member) => (member.id === updated.id ? updated : member))
          .sort((a, b) => a.displayName.localeCompare(b.displayName)),
      );

      if (updated.isCurrentUser) {
        updateSessionHouseholds(
          session.households.map((household) =>
            household.id === activeHouseholdId ? { ...household, role: updated.role } : household,
          ),
          activeHouseholdId,
        );
      }

      toast.success("Pessoa atualizada.");
    } catch (exception) {
      reportError(exception, "Não foi possível atualizar a pessoa.");
    }
  }

  async function removeHouseholdMember(member: HouseholdMember) {
    if (
      !session ||
      !activeHouseholdId ||
      !window.confirm(
        `Remover ${member.displayName} da casa? O histórico de ações e comentários será preservado.`,
      )
    ) {
      return;
    }

    try {
      await apiFetch<void>(`/api/households/members/${member.id}`, {
        method: "DELETE",
        token: session.accessToken,
        householdId: activeHouseholdId,
      });
      setMembers((current) => current.filter((item) => item.id !== member.id));

      if (member.isCurrentUser) {
        updateSessionHouseholds(session.households.filter((household) => household.id !== activeHouseholdId));
      }

      toast.success("Pessoa removida da casa.");
    } catch (exception) {
      reportError(exception, "Não foi possível remover a pessoa.");
    }
  }

  async function updateProfile(input: { displayName: string; phoneNumber?: string; profilePhoto?: File | null }) {
    if (!session) {
      return;
    }

    let profileSaved = false;

    try {
      let updatedUser = await apiFetch<AuthResponse["user"]>("/api/users/me", {
        method: "PUT",
        token: session.accessToken,
        body: JSON.stringify({
          displayName: input.displayName,
          phoneNumber: input.phoneNumber || null,
        }),
      });
      profileSaved = true;
      applyUpdatedUser(updatedUser);

      if (input.profilePhoto) {
        const formData = new FormData();
        formData.append("file", input.profilePhoto);
        updatedUser = await apiFetch<AuthResponse["user"]>("/api/users/me/profile-photo", {
          method: "POST",
          token: session.accessToken,
          body: formData,
        });
        applyUpdatedUser(updatedUser);
      }

      toast.success(input.profilePhoto ? "Perfil e foto atualizados." : "Perfil atualizado.");
    } catch (exception) {
      reportError(
        exception,
        profileSaved
          ? "Os dados do perfil foram salvos, mas não foi possível concluir o envio da foto."
          : "Não foi possível atualizar o perfil.",
      );
    }
  }

  async function updateActivityStatus(activity: Activity, nextStatus: Activity["status"]) {
    if (!session || !activeHouseholdId || !activity.canEdit || activity.status === nextStatus) {
      return;
    }

    try {
      const updated = await apiFetch<Activity>(`/api/activities/${activity.id}/status`, {
        method: "PATCH",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify({ status: nextStatus }),
      });
      setActivities((current) => current.map((item) => (item.id === updated.id ? updated : item)));
      setProjects((current) => updateProjectActivityCounts(current, activity, updated));
      setSelectedActivity((current) => (current?.id === updated.id ? updated : current));
      toast.success("Status da atividade atualizado.");
    } catch (exception) {
      reportError(exception, "Não foi possível atualizar o status.");
    }
  }

  async function updateActivityStatusOptimistic(activity: Activity, nextStatus: Activity["status"]) {
    if (!session || !activeHouseholdId || !activity.canEdit || activity.status === nextStatus) {
      return;
    }

    const mutationVersion = (activityStatusMutationVersionRef.current[activity.id] ?? 0) + 1;
    activityStatusMutationVersionRef.current[activity.id] = mutationVersion;

    const previousActivity = activities.find((item) => item.id === activity.id) ?? activity;
    const optimisticActivity = { ...previousActivity, status: nextStatus };

    replaceActivityInState(optimisticActivity);
    setProjects((current) => updateProjectActivityCounts(current, previousActivity, optimisticActivity));

    try {
      const updated = await apiFetch<Activity>(`/api/activities/${activity.id}/status`, {
        method: "PATCH",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify({ status: nextStatus }),
      });

      if (activityStatusMutationVersionRef.current[activity.id] !== mutationVersion) {
        return;
      }

      replaceActivityInState(updated);
    } catch (exception) {
      if (activityStatusMutationVersionRef.current[activity.id] !== mutationVersion) {
        return;
      }

      restoreActivityInState(previousActivity);
      setProjects((current) => updateProjectActivityCounts(current, optimisticActivity, previousActivity));
      toast.error(getErrorMessage(exception, "Não foi possível atualizar o status."));
    }
  }

  async function moveActivity(activity: Activity, direction: -1 | 1) {
    const currentIndex = activityColumns.findIndex((column) => column.status === activity.status);
    const nextStatus = activityColumns[currentIndex + direction]?.status;
    if (!nextStatus) {
      return;
    }

    await updateActivityStatus(activity, nextStatus);
  }

  async function createComment(activityId: string, body: string) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      const created = await apiFetch<ActivityComment>(`/api/activities/${activityId}/comments`, {
        method: "POST",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify({ body }),
      });
      setActivityComments((current) => [...current, created]);
      setActivities((current) =>
        current.map((activity) =>
          activity.id === activityId
            ? { ...activity, commentCount: activity.commentCount + 1 }
            : activity,
        ),
      );
      toast.success("Comentário publicado.");
    } catch (exception) {
      reportError(exception, "Não foi possível comentar.");
    }
  }

  async function updateComment(activityId: string, commentId: string, body: string) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      const updated = await apiFetch<ActivityComment>(`/api/activities/${activityId}/comments/${commentId}`, {
        method: "PUT",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify({ body }),
      });
      setActivityComments((current) => current.map((comment) => (comment.id === updated.id ? updated : comment)));
      toast.success("Comentário atualizado.");
    } catch (exception) {
      reportError(exception, "Não foi possível salvar o comentário.");
    }
  }

  async function deleteComment(activityId: string, comment: ActivityComment) {
    if (
      !session ||
      !activeHouseholdId ||
      !comment.canDelete ||
      !window.confirm("Excluir este comentário?")
    ) {
      return;
    }

    try {
      await apiFetch<void>(`/api/activities/${activityId}/comments/${comment.id}`, {
        method: "DELETE",
        token: session.accessToken,
        householdId: activeHouseholdId,
      });
      setActivityComments((current) => current.filter((item) => item.id !== comment.id));
      setActivities((current) =>
        current.map((activity) =>
          activity.id === activityId
            ? { ...activity, commentCount: Math.max(0, activity.commentCount - 1) }
            : activity,
        ),
      );
      toast.success("Comentário excluído.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir o comentário.");
    }
  }

  function openActivity(activity: Activity) {
    setSelectedActivity(activity);
    setActivityComments([]);
    void loadComments(activity.id);
  }

  function closeActivity() {
    setSelectedActivity(null);
    setActivityComments([]);
  }

  function selectAllScopes() {
    setSelectedUniverseId("");
    setSelectedProjectId("");
  }

  function selectUniverseScope(universeId: string) {
    setSelectedUniverseId(universeId);
    setSelectedProjectId("");
  }

  function selectProjectScope(project: Project) {
    setSelectedUniverseId(project.universeId);
    setSelectedProjectId(project.id);
  }

  return {
    session,
    activeHouseholdId,
    activeHousehold,
    universes,
    projects,
    activities,
    members,
    selectedUniverseId,
    selectedProjectId,
    filters,
    viewMode,
    sidebarCollapsed,
    theme,
    activeModal,
    editingHousehold,
    editingUniverse,
    editingProject,
    editingActivity,
    activityDraftProjectId,
    selectedActivity: selectedActivitySnapshot,
    activityComments,
    commentsLoading,
    loading,
    error,
    canShareHousehold,
    canManageHousehold,
    filteredProjects,
    activityDialogProjects,
    visibleActivities,
    groupedActivities,
    selectedScopeLabel,
    setError,
    setViewMode,
    setSidebarCollapsed,
    setTheme,
    updateFilter,
    resetFilters,
    handleAuthenticated,
    handleHouseholdChange,
    handleLogout,
    loadWorkspace,
    refreshHouseholds,
    createHousehold,
    updateHousehold,
    deleteHousehold,
    openCreateHousehold,
    openEditHousehold,
    openCreateUniverse,
    openEditUniverse,
    openCreateProject,
    openEditProject,
    openCreateActivity,
    openShareHousehold,
    openEditActivity,
    closeModal,
    createUniverse,
    updateUniverse,
    deleteUniverse,
    createProject,
    updateProject,
    deleteProject,
    createActivity,
    updateActivity,
    deleteActivity,
    shareHousehold,
    updateHouseholdMember,
    removeHouseholdMember,
    updateProfile,
    updateActivityStatus,
    updateActivityStatusOptimistic,
    moveActivity,
    createComment,
    updateComment,
    deleteComment,
    openActivity,
    closeActivity,
    selectAllScopes,
    selectUniverseScope,
    selectProjectScope,
  };
}

export type ProjectDashboardController = ReturnType<typeof useProjectDashboard>;

"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { toast } from "sonner";
import {
  Activity,
  ActivityComment,
  ActivityRelevance,
  ActivityRelevanceResponse,
  AuthResponse,
  EffortPlan,
  EffortScopeType,
  EffortWeekday,
  Space,
  SpaceMember,
  Project,
  Core,
  apiFetch,
  clearSession,
  readSession,
  storeSession,
  subscribeToSessionChanges,
  updateStoredSession,
} from "@/lib/api";
import {
  clearStoredActiveSpaceId,
  readStoredActiveSpaceId,
  resolveActiveSpaceSelection,
  storeActiveSpaceId,
} from "@/lib/space-selection";
import { activityColumns, defaultActivityFilters, defaultAppTheme, uiStorageKeys } from "./project-dashboard.constants";
import type {
  ActiveModal,
  ActivityFilterState,
  ActivityFormInput,
  ActivitySortState,
  AppTheme,
  ProjectViewMode,
} from "./project-dashboard.types";
import { activityMatchesSearch, getErrorMessage, isCompletedActivityOlderThanDays, sortActivities } from "./project-dashboard.utils";

const COMPLETED_ACTIVITY_HIDE_DAYS = 30;

function isAppTheme(value: string | null): value is AppTheme {
  return value === "light" || value === "system" || value === "dark";
}

function applyDocumentTheme(theme: AppTheme) {
  const resolved = theme === "system"
    ? window.matchMedia?.("(prefers-color-scheme: dark)").matches ? "dark" : "light"
    : theme;
  document.documentElement.dataset.themePreference = theme;
  document.documentElement.dataset.theme = resolved;
}

function isActivitySortState(value: string | null): value is ActivitySortState {
  return (
    value === "priority" ||
    value === "relevance" ||
    value === "size" ||
    value === "project" ||
    value === "responsible" ||
    value === "title"
  );
}

function readStoredActivitySort() {
  if (typeof window === "undefined") {
    return null;
  }

  try {
    const value = window.localStorage.getItem(uiStorageKeys.projectActivitySort);
    return isActivitySortState(value) ? value : null;
  } catch {
    return null;
  }
}

function storeActivitySort(sort: ActivitySortState) {
  if (typeof window === "undefined") {
    return;
  }

  try {
    window.localStorage.setItem(uiStorageKeys.projectActivitySort, sort);
  } catch {
    // Ignore storage failures so a restricted browser does not block the UI.
  }
}

function buildDefaultActivityFilters(): ActivityFilterState {
  return {
    ...defaultActivityFilters,
    sort: readStoredActivitySort() ?? defaultActivityFilters.sort,
  };
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
  const pathname = usePathname();
  const router = useRouter();
  const [session, setSession] = useState<AuthResponse | null>(null);
  const [activeSpaceId, setActiveSpaceId] = useState("");
  const [cores, setCores] = useState<Core[]>([]);
  const [projects, setProjects] = useState<Project[]>([]);
  const [activities, setActivities] = useState<Activity[]>([]);
  const [effortPlan, setEffortPlan] = useState<EffortPlan | null>(null);
  const [relevance, setRelevance] = useState<ActivityRelevanceResponse | null>(null);
  const [members, setMembers] = useState<SpaceMember[]>([]);
  const [selectedCoreId, setSelectedCoreId] = useState("");
  const [selectedProjectId, setSelectedProjectId] = useState("");
  const [filters, setFilters] = useState<ActivityFilterState>(defaultActivityFilters);
  const [showOldCompleted, setShowOldCompleted] = useState(false);
  const [viewMode, setViewModeState] = useState<ProjectViewMode>("kanban");
  const [sidebarCollapsed, setSidebarCollapsedState] = useState(false);
  const [theme, setThemeState] = useState<AppTheme>(defaultAppTheme);
  const [activeModal, setActiveModal] = useState<ActiveModal>(null);
  const [editingSpace, setEditingSpace] = useState<Space | null>(null);
  const [editingCore, setEditingCore] = useState<Core | null>(null);
  const [editingProject, setEditingProject] = useState<Project | null>(null);
  const [editingActivity, setEditingActivity] = useState<Activity | null>(null);
  const [activityDraftProjectId, setActivityDraftProjectId] = useState("");
  const [selectedActivity, setSelectedActivity] = useState<Activity | null>(null);
  const [activityComments, setActivityComments] = useState<ActivityComment[]>([]);
  const [commentsLoading, setCommentsLoading] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const sessionUserIdRef = useRef<string | null>(null);
  const activeSpaceIdRef = useRef("");
  const activityStatusMutationVersionRef = useRef<Record<string, number>>({});

  const resetWorkspaceState = useCallback(() => {
    setCores([]);
    setProjects([]);
    setActivities([]);
    setEffortPlan(null);
    setRelevance(null);
    setMembers([]);
    setSelectedCoreId("");
    setSelectedProjectId("");
    setSelectedActivity(null);
    setActivityComments([]);
    setEditingSpace(null);
    setEditingCore(null);
    setEditingProject(null);
    setEditingActivity(null);
    setActivityDraftProjectId("");
    setActiveModal(null);
    setCommentsLoading(false);
    setLoading(false);
    setError(null);
    setFilters(buildDefaultActivityFilters());
    setShowOldCompleted(false);
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
      if (!nextSession) {
        setActiveSpaceId("");
        return;
      }

      const storedSpaceId = readStoredActiveSpaceId(nextSession.user.id);
      const { spaceId, shouldClearStoredSpaceId } = resolveActiveSpaceSelection(
        nextSession.spaces,
        activeSpaceIdRef.current,
        storedSpaceId,
      );

      if (shouldClearStoredSpaceId) {
        clearStoredActiveSpaceId(nextSession.user.id);
      }

      setActiveSpaceId(spaceId);
      setLoading(Boolean(nextSession && nextSession.spaces.length > 0));
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

  useEffect(() => {
    if (!session) {
      return;
    }

    if (session.user.systemRole === "SuperAdmin") {
      return;
    }

    if (session.spaces.length > 0) {
      return;
    }

    if (pathname === "/profile" || pathname === "/spaces") {
      return;
    }

    router.replace("/profile");
  }, [pathname, router, session]);

  useEffect(() => {
    activeSpaceIdRef.current = activeSpaceId;
  }, [activeSpaceId]);

  useEffect(() => {
    const userId = session?.user.id;

    if (!userId) {
      return;
    }

    if (activeSpaceId) {
      storeActiveSpaceId(userId, activeSpaceId);
      return;
    }

    clearStoredActiveSpaceId(userId);
  }, [activeSpaceId, session?.user.id]);

  const activeSpace = useMemo(() => {
    return session?.spaces.find((space) => space.id === activeSpaceId) ?? null;
  }, [activeSpaceId, session?.spaces]);
  const isAccountActive = (session?.user.accountState ?? "Active") === "Active";

  const canShareSpace = activeSpace?.role === "Owner" || activeSpace?.role === "Admin";
  const canManageSpace = activeSpace?.role === "Owner";

  const filteredProjects = useMemo(() => {
    return selectedCoreId
      ? projects.filter((project) => project.coreId === selectedCoreId)
      : projects;
  }, [projects, selectedCoreId]);

  const activityDialogProjects = useMemo(() => {
    if (!activityDraftProjectId) {
      return filteredProjects.length > 0 ? filteredProjects : projects;
    }

    return filteredProjects.some((project) => project.id === activityDraftProjectId)
      ? filteredProjects
      : projects;
  }, [activityDraftProjectId, filteredProjects, projects]);

  const visibleActivities = useMemo(() => {
    const relevanceByActivityId = new Map((relevance?.items ?? []).map((item) => [item.activityId, item]));
    const scopedActivities = activities.filter((activity) => {
      const matchesCore = !selectedCoreId || activity.coreId === selectedCoreId;
      const matchesProject = !selectedProjectId || activity.projectId === selectedProjectId;

      return matchesCore && matchesProject;
    });

    const filteredActivities = scopedActivities.filter((activity) => {
      const matchesSearch = activityMatchesSearch(activity, filters.search);

      const matchesRelevance = filters.sort !== "relevance" || relevanceByActivityId.has(activity.id);
      const matchesStatus =
        filters.sort === "relevance" ? isOpenActivity(activity.status) : filters.status === "all" || activity.status === filters.status;
      const matchesPriority = filters.priority === "all" || activity.priority === filters.priority;
      const matchesResponsible =
        filters.sort === "relevance" ||
        filters.responsibleMemberId === "all" || activity.responsibleMemberId === filters.responsibleMemberId;
      const matchesCompletedVisibility = showOldCompleted || !isCompletedActivityOlderThanDays(activity, COMPLETED_ACTIVITY_HIDE_DAYS);

      return matchesSearch && matchesRelevance && matchesStatus && matchesPriority && matchesResponsible && matchesCompletedVisibility;
    });

    if (filters.sort === "relevance") {
      return [...filteredActivities].sort(
        (left, right) =>
          (relevanceByActivityId.get(left.id)?.position ?? Number.MAX_SAFE_INTEGER) -
          (relevanceByActivityId.get(right.id)?.position ?? Number.MAX_SAFE_INTEGER),
      );
    }

    return sortActivities(filteredActivities, filters.sort);
  }, [activities, filters, relevance, selectedProjectId, selectedCoreId, showOldCompleted]);

  const scopedActivities = useMemo(
    () =>
      activities.filter(
        (activity) =>
          (!selectedCoreId || activity.coreId === selectedCoreId) &&
          (!selectedProjectId || activity.projectId === selectedProjectId),
      ),
    [activities, selectedProjectId, selectedCoreId],
  );

  const hasOldCompletedActivities = useMemo(
    () => scopedActivities.some((activity) => isCompletedActivityOlderThanDays(activity, COMPLETED_ACTIVITY_HIDE_DAYS)),
    [scopedActivities],
  );

  const hasHiddenOldCompletedSearchMatch = useMemo(() => {
    if (showOldCompleted || !filters.search.trim()) {
      return false;
    }

    return scopedActivities.some((activity) => {
      const matchesStatus = filters.sort === "relevance" ? false : filters.status === "all" || activity.status === filters.status;
      const matchesPriority = filters.priority === "all" || activity.priority === filters.priority;
      const matchesResponsible =
        filters.sort === "relevance" ||
        filters.responsibleMemberId === "all" ||
        activity.responsibleMemberId === filters.responsibleMemberId;

      return (
        isCompletedActivityOlderThanDays(activity, COMPLETED_ACTIVITY_HIDE_DAYS) &&
        activityMatchesSearch(activity, filters.search) &&
        matchesStatus &&
        matchesPriority &&
        matchesResponsible
      );
    });
  }, [filters, scopedActivities, showOldCompleted]);

  const selectedScopeLabel = useMemo(() => {
    const project = projects.find((item) => item.id === selectedProjectId);
    if (project) {
      return project.name;
    }

    const core = cores.find((item) => item.id === selectedCoreId);
    return core?.name ?? "Todos os projetos";
  }, [projects, selectedProjectId, selectedCoreId, cores]);

  const selectedActivitySnapshot = selectedActivity
    ? activities.find((activity) => activity.id === selectedActivity.id) ?? selectedActivity
    : null;

  const currentSpaceMember = useMemo(
    () => members.find((member) => member.isCurrentUser) ?? members.find((member) => member.userId === session?.user.id) ?? null,
    [members, session?.user.id],
  );

  const canAssignActivityToMe = useCallback(
    (activity: Activity) => Boolean(currentSpaceMember && activity.canEdit && activity.responsibleMemberId !== currentSpaceMember.id),
    [currentSpaceMember],
  );

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
    setFilters((current) =>
      key === "sort" && value === "relevance"
        ? { ...current, sort: "relevance", status: "all", responsibleMemberId: "all" }
        : { ...current, [key]: value },
    );

    if (key === "sort") {
      storeActivitySort(value as ActivitySortState);
      if (value === "relevance") {
        setViewModeState("list");
        window.localStorage.setItem(uiStorageKeys.projectViewMode, "list");
      }
    }
  }, []);

  const resetFilters = useCallback(() => {
    setFilters(defaultActivityFilters);
    storeActivitySort(defaultActivityFilters.sort);
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
    async (token = session?.accessToken, spaceId = activeSpaceId) => {
      if (!token || !spaceId || (session?.user.accountState ?? "Active") !== "Active") {
        return;
      }

      setLoading(true);
      setError(null);
      try {
        const [nextCores, nextProjects, nextActivities, nextMembers] = await Promise.all([
          apiFetch<Core[]>("/api/cores", { token, spaceId }),
          apiFetch<Project[]>("/api/projects", { token, spaceId }),
          apiFetch<Activity[]>("/api/activities", { token, spaceId }),
          apiFetch<SpaceMember[]>("/api/spaces/members", { token, spaceId }),
        ]);

        setCores(nextCores);
        setProjects(nextProjects);
        setActivities(nextActivities);
        setMembers(nextMembers);
        if (session?.user.systemRole !== "SuperAdmin") {
          void apiFetch<EffortPlan>("/api/effort-plan", { token, spaceId })
            .then(setEffortPlan)
            .catch(() => setEffortPlan(null));
        }
      } catch (exception) {
        setError(getErrorMessage(exception, "Falha ao carregar dados."));
      } finally {
        setLoading(false);
      }
    },
    [activeSpaceId, session?.accessToken, session?.user],
  );

  useEffect(() => {
    if (filters.sort !== "relevance" || !session?.accessToken || !activeSpaceId || session.user.systemRole === "SuperAdmin") {
      return;
    }

    const current = new Date();
    const date = `${current.getFullYear()}-${String(current.getMonth() + 1).padStart(2, "0")}-${String(current.getDate()).padStart(2, "0")}`;
    const utcOffsetMinutes = -current.getTimezoneOffset();
    void apiFetch<ActivityRelevanceResponse>(`/api/activities/relevance?date=${date}&utcOffsetMinutes=${utcOffsetMinutes}`, {
      token: session.accessToken,
      spaceId: activeSpaceId,
    })
      .then(setRelevance)
      .catch((exception) => setError(getErrorMessage(exception, "Não foi possível calcular a fila de hoje.")));
  }, [activeSpaceId, activities, effortPlan, filters.sort, session]);

  const loadComments = useCallback(
    async (activityId: string) => {
      if (!session || !activeSpaceId || (session.user.accountState ?? "Active") !== "Active") {
        return;
      }

      setCommentsLoading(true);
      try {
        const nextComments = await apiFetch<ActivityComment[]>(`/api/activities/${activityId}/comments`, {
          token: session.accessToken,
          spaceId: activeSpaceId,
        });
        setActivityComments(nextComments);
      } catch (exception) {
        setError(getErrorMessage(exception, "Falha ao carregar comentários."));
      } finally {
        setCommentsLoading(false);
      }
    },
    [activeSpaceId, session],
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
    if (!session || !activeSpaceId || !isAccountActive) {
      return;
    }

    const timer = window.setTimeout(() => {
      void loadWorkspace(session.accessToken, activeSpaceId);
    }, 0);

    return () => window.clearTimeout(timer);
  }, [session, activeSpaceId, isAccountActive, loadWorkspace]);

  const handleAuthenticated = useCallback((auth: AuthResponse) => {
    storeSession(auth);
    toast.success("Sessão iniciada com sucesso.");
  }, []);

  const handleSpaceChange = useCallback((spaceId: string) => {
    setLoading(true);
    setCores([]);
    setProjects([]);
    setActivities([]);
    setMembers([]);
    setActiveSpaceId(spaceId);
    setSelectedCoreId("");
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

  const updateSessionSpaces = useCallback(
    (nextSpaces: Space[], preferredSpaceId?: string) => {
      const nextSession = updateStoredSession((currentSession) => ({
        ...currentSession,
        spaces: nextSpaces,
      }));

      if (!nextSession) {
        return;
      }

      setSession(nextSession);

      const storedSpaceId = readStoredActiveSpaceId(nextSession.user.id);
      const { spaceId, shouldClearStoredSpaceId } = resolveActiveSpaceSelection(
        nextSpaces,
        activeSpaceIdRef.current,
        storedSpaceId,
        preferredSpaceId,
      );

      if (shouldClearStoredSpaceId) {
        clearStoredActiveSpaceId(nextSession.user.id);
      }

      setActiveSpaceId(spaceId);
    },
    [],
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

  const refreshSpaces = useCallback(async () => {
    if (!session) {
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const nextSpaces = await apiFetch<Space[]>("/api/spaces", {
        token: session.accessToken,
      });
      updateSessionSpaces(nextSpaces);
      toast.success("Espaços atualizados.");
    } catch (exception) {
      setError(getErrorMessage(exception, "Falha ao carregar espaços."));
      toast.error(getErrorMessage(exception, "Falha ao carregar espaços."));
    } finally {
      setLoading(false);
    }
  }, [session, updateSessionSpaces]);

  async function createSpace(name: string) {
    if (!session) {
      return;
    }

    try {
      const created = await apiFetch<Space>("/api/spaces", {
        method: "POST",
        token: session.accessToken,
        body: JSON.stringify({ name }),
      });
      updateSessionSpaces(
        [...session.spaces, created].sort((a, b) => a.name.localeCompare(b.name)),
        created.id,
      );
      toast.success("Espaço criado.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar o espaço.");
    }
  }

  async function updateSpace(spaceId: string, name: string) {
    if (!session) {
      return;
    }

    try {
      const updated = await apiFetch<Space>(`/api/spaces/${spaceId}`, {
        method: "PUT",
        token: session.accessToken,
        spaceId,
        body: JSON.stringify({ name }),
      });
      updateSessionSpaces(
        session.spaces
          .map((space) => (space.id === updated.id ? updated : space))
          .sort((a, b) => a.name.localeCompare(b.name)),
        updated.id,
      );
      toast.success("Espaço atualizado.");
    } catch (exception) {
      reportError(exception, "Não foi possível salvar o espaço.");
    }
  }

  async function deleteSpace(space: Space) {
    if (!session) {
      return;
    }

    try {
      await apiFetch<void>(`/api/spaces/${space.id}`, {
        method: "DELETE",
        token: session.accessToken,
        spaceId: space.id,
      });

      const nextSpaces = session.spaces.filter((item) => item.id !== space.id);
      setCores([]);
      setProjects([]);
      setActivities([]);
      setMembers([]);
      setSelectedCoreId("");
      setSelectedProjectId("");
      setSelectedActivity(null);
      setActivityComments([]);
      updateSessionSpaces(nextSpaces);
      toast.success("Espaço excluído.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir o espaço.");
    }
  }

  function openCreateSpace() {
    setEditingSpace(null);
    setActiveModal("space");
  }

  function openEditSpace() {
    if (!activeSpace) {
      return;
    }

    setEditingSpace(activeSpace);
    setActiveModal("space");
  }

  function openCreateCore() {
    setEditingCore(null);
    setActiveModal("core");
  }

  function openEditCore(core: Core) {
    if (!core.canEdit) {
      return;
    }

    setEditingCore(core);
    setActiveModal("core");
  }

  function openCreateProject(coreId?: string) {
    setEditingProject(null);
    if (coreId) {
      setSelectedCoreId(coreId);
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

  function openEffortPlan() {
    setActiveModal("effort");
  }

  function openShareSpace() {
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
    setEditingSpace(null);
    setEditingCore(null);
    setEditingProject(null);
    setEditingActivity(null);
    setActivityDraftProjectId("");
  }

  async function createCore(input: { name: string; imageFile?: File | null; removeImage?: boolean }) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      let created = await apiFetch<Core>("/api/cores", {
        method: "POST",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify({ name: input.name, imageUrl: null }),
      });

      if (input.imageFile) {
        created = await uploadCoreImage(created.id, input.imageFile);
      }

      setCores((current) => [...current, created].sort((a, b) => a.name.localeCompare(b.name)));
      setSelectedCoreId(created.id);
      setSelectedProjectId("");
      toast.success("Núcleo criado.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar o núcleo.");
    }
  }

  async function updateCore(coreId: string, input: { name: string; imageFile?: File | null; removeImage?: boolean }) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      let updated = await apiFetch<Core>(`/api/cores/${coreId}`, {
        method: "PUT",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify({ name: input.name, imageUrl: null }),
      });

      if (input.imageFile) {
        updated = await uploadCoreImage(coreId, input.imageFile);
      } else if (input.removeImage) {
        updated = await deleteCoreImage(coreId);
      }

      setCores((current) =>
        current
          .map((core) => (core.id === updated.id ? updated : core))
          .sort((a, b) => a.name.localeCompare(b.name)),
      );
      setProjects((current) =>
        current.map((project) =>
          project.coreId === updated.id
            ? {
                ...project,
                coreName: updated.name,
                coreImageUrl: updated.imageUrl ?? null,
                coreHasImage: updated.hasImage,
                coreImageUpdatedAt: updated.imageUpdatedAt ?? null,
              }
            : project,
        ),
      );
      setActivities((current) =>
        current.map((activity) =>
          activity.coreId === updated.id
            ? {
                ...activity,
                coreName: updated.name,
                coreImageUrl: updated.imageUrl ?? null,
                coreHasImage: updated.hasImage,
                coreImageUpdatedAt: updated.imageUpdatedAt ?? null,
              }
            : activity,
        ),
      );
      toast.success("Núcleo atualizado.");
    } catch (exception) {
      reportError(exception, "Não foi possível salvar o núcleo.");
    }
  }

  async function deleteCore(core: Core) {
    if (!session || !activeSpaceId || !core.canDelete) {
      return;
    }

    try {
      await apiFetch<void>(`/api/cores/${core.id}`, {
        method: "DELETE",
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      setCores((current) => current.filter((item) => item.id !== core.id));
      setProjects((current) => current.filter((project) => project.coreId !== core.id));
      setActivities((current) => current.filter((activity) => activity.coreId !== core.id));
      setSelectedCoreId((current) => (current === core.id ? "" : current));
      setSelectedProjectId((current) => {
        const selectedProject = projects.find((project) => project.id === current);
        return selectedProject?.coreId === core.id ? "" : current;
      });
      if (selectedActivity?.coreId === core.id) {
        setSelectedActivity(null);
        setActivityComments([]);
      }
      toast.success("Núcleo excluído.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir o núcleo.");
    }
  }

  async function uploadCoreImage(coreId: string, imageFile: File) {
    if (!session || !activeSpaceId) {
      throw new Error("Sessão inválida para upload da imagem do núcleo.");
    }

    const formData = new FormData();
    formData.append("file", imageFile);

    return await apiFetch<Core>(`/api/cores/${coreId}/image`, {
      method: "POST",
      token: session.accessToken,
      spaceId: activeSpaceId,
      body: formData,
    });
  }

  async function deleteCoreImage(coreId: string) {
    if (!session || !activeSpaceId) {
      throw new Error("Sessão inválida para remoção da imagem do núcleo.");
    }

    return await apiFetch<Core>(`/api/cores/${coreId}/image`, {
      method: "DELETE",
      token: session.accessToken,
      spaceId: activeSpaceId,
    });
  }

  async function createProject(coreId: string, name: string) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      const created = await apiFetch<Project>("/api/projects", {
        method: "POST",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify({ coreId, name }),
      });
      setProjects((current) => [...current, created].sort((a, b) => a.name.localeCompare(b.name)));
      setCores((current) =>
        current.map((core) =>
          core.id === created.coreId
            ? { ...core, projectCount: core.projectCount + 1 }
            : core,
        ),
      );
      setSelectedCoreId(created.coreId);
      setSelectedProjectId(created.id);
      toast.success("Projeto criado.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar o projeto.");
    }
  }

  async function updateProject(projectId: string, coreId: string, name: string) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      const previousProject = projects.find((project) => project.id === projectId) ?? null;
      const updated = await apiFetch<Project>(`/api/projects/${projectId}`, {
        method: "PUT",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify({ coreId, name }),
      });

      setProjects((current) =>
        current
          .map((project) => (project.id === updated.id ? updated : project))
          .sort((a, b) => a.name.localeCompare(b.name)),
      );
      setCores((current) =>
        current.map((core) => {
          if (!previousProject || previousProject.coreId === updated.coreId) {
            return core;
          }

          if (core.id === previousProject.coreId) {
            return { ...core, projectCount: Math.max(0, core.projectCount - 1) };
          }

          if (core.id === updated.coreId) {
            return { ...core, projectCount: core.projectCount + 1 };
          }

          return core;
        }),
      );
      setActivities((current) =>
        current.map((activity) =>
          activity.projectId === updated.id
            ? {
                ...activity,
                projectName: updated.name,
                coreId: updated.coreId,
                coreName: updated.coreName,
              }
            : activity,
        ),
      );
      setSelectedCoreId(updated.coreId);
      setSelectedProjectId(updated.id);
      toast.success("Projeto atualizado.");
    } catch (exception) {
      reportError(exception, "Não foi possível salvar o projeto.");
    }
  }

  async function deleteProject(project: Project) {
    if (!session || !activeSpaceId || !project.canDelete) {
      return;
    }

    try {
      await apiFetch<void>(`/api/projects/${project.id}`, {
        method: "DELETE",
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      setProjects((current) => current.filter((item) => item.id !== project.id));
      setCores((current) =>
        current.map((core) =>
          core.id === project.coreId
            ? { ...core, projectCount: Math.max(0, core.projectCount - 1) }
            : core,
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
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      const { imageFile, removeImage, ...payload } = input;
      const created = await apiFetch<Activity>("/api/activities", {
        method: "POST",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify({
          ...payload,
          description: input.description || null,
          dueDate: input.dueDate || null,
          size: input.size ?? null,
          responsibleMemberId: input.responsibleMemberId || null,
        }),
      });
      setActivities((current) => [...current, created]);
      setProjects((current) => updateProjectActivityCounts(current, null, created));
      setSelectedCoreId(created.coreId);
      setSelectedProjectId(created.projectId);

      if (imageFile) {
        const uploaded = await uploadActivityImage(created.id, imageFile);
        replaceActivityInState(uploaded);
      } else if (removeImage) {
        replaceActivityInState(created);
      }

      toast.success("Atividade criada.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar a atividade.");
    }
  }

  async function updateActivity(
    activityId: string,
    input: ActivityFormInput,
    options?: {
      successMessage?: string;
    },
  ) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      const previousActivity = activities.find((activity) => activity.id === activityId) ?? null;
      const { imageFile, removeImage, ...payload } = input;
      const updated = await apiFetch<Activity>(`/api/activities/${activityId}`, {
        method: "PUT",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify({
          ...payload,
          description: input.description || null,
          dueDate: input.dueDate || null,
          size: input.size ?? null,
          responsibleMemberId: input.responsibleMemberId || null,
        }),
      });

      setActivities((current) => current.map((activity) => (activity.id === updated.id ? updated : activity)));
      setProjects((current) => updateProjectActivityCounts(current, previousActivity, updated));
      setSelectedCoreId(updated.coreId);
      setSelectedProjectId(updated.projectId);
      setSelectedActivity((current) => (current?.id === updated.id ? updated : current));

      if (imageFile) {
        await uploadActivityImage(updated.id, imageFile);
      } else if (removeImage && updated.hasImage) {
        await deleteActivityImage(updated.id);
      }

      toast.success(options?.successMessage ?? "Atividade atualizada.");
    } catch (exception) {
      reportError(exception, "Não foi possível salvar a atividade.");
    }
  }

  async function assignActivityToMe(activity: Activity) {
    if (!session || !activeSpaceId || !activity.canEdit) {
      return;
    }

    if (!currentSpaceMember) {
      reportError(new Error("Seu vínculo com o espaço não foi encontrado."), "Não foi possível atribuir a atividade.");
      return;
    }

    if (activity.responsibleMemberId === currentSpaceMember.id) {
      return;
    }

    await updateActivity(
      activity.id,
      {
        projectId: activity.projectId,
        title: activity.title,
        description: activity.description ?? undefined,
        dueDate: activity.dueDate ?? "",
        status: activity.status,
        priority: activity.priority,
        size: activity.size ?? undefined,
        responsibleMemberId: currentSpaceMember.id,
      },
      {
        successMessage: "Atividade atribuída a você.",
      },
    );
  }

  async function deleteActivity(activity: Activity) {
    if (
      !session ||
      !activeSpaceId ||
      !activity.canDelete ||
      !window.confirm(`Excluir a atividade "${activity.title}"?`)
    ) {
      return;
    }

    try {
      await apiFetch<void>(`/api/activities/${activity.id}`, {
        method: "DELETE",
        token: session.accessToken,
        spaceId: activeSpaceId,
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

  async function uploadActivityImage(activityId: string, imageFile: File) {
    if (!session || !activeSpaceId) {
      throw new Error("Sessão inválida para upload da imagem da atividade.");
    }

    const formData = new FormData();
    formData.append("file", imageFile);

    const updated = await apiFetch<Activity>(`/api/activities/${activityId}/image`, {
      method: "POST",
      token: session.accessToken,
      spaceId: activeSpaceId,
      body: formData,
    });

    replaceActivityInState(updated);
    return updated;
  }

  async function deleteActivityImage(activityId: string) {
    if (!session || !activeSpaceId) {
      throw new Error("Sessão inválida para remoção da imagem da atividade.");
    }

    const updated = await apiFetch<Activity>(`/api/activities/${activityId}/image`, {
      method: "DELETE",
      token: session.accessToken,
      spaceId: activeSpaceId,
    });

    replaceActivityInState(updated);
    return updated;
  }

  async function shareSpace(input: { email: string; role: "Admin" | "Member" }) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      const created = await apiFetch<SpaceMember>("/api/spaces/share", {
        method: "POST",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify(input),
      });
      setMembers((current) => [...current, created].sort((a, b) => a.displayName.localeCompare(b.displayName)));
      toast.success("Pessoa adicionada ao espaço.");
    } catch (exception) {
      reportError(exception, "Não foi possível compartilhar o espaço.");
    }
  }

  async function updateSpaceMember(memberId: string, role: SpaceMember["role"]) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      const updated = await apiFetch<SpaceMember>(`/api/spaces/members/${memberId}`, {
        method: "PUT",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify({ role }),
      });
      setMembers((current) =>
        current
          .map((member) => (member.id === updated.id ? updated : member))
          .sort((a, b) => a.displayName.localeCompare(b.displayName)),
      );

      if (updated.isCurrentUser) {
        updateSessionSpaces(
          session.spaces.map((space) =>
            space.id === activeSpaceId ? { ...space, role: updated.role } : space,
          ),
          activeSpaceId,
        );
      }

      toast.success("Pessoa atualizada.");
    } catch (exception) {
      reportError(exception, "Não foi possível atualizar a pessoa.");
    }
  }

  async function removeSpaceMember(member: SpaceMember) {
    if (
      !session ||
      !activeSpaceId ||
      !window.confirm(
        `Remover ${member.displayName} do espaço? O histórico de ações e comentários será preservado.`,
      )
    ) {
      return;
    }

    try {
      await apiFetch<void>(`/api/spaces/members/${member.id}`, {
        method: "DELETE",
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      setMembers((current) => current.filter((item) => item.id !== member.id));

      if (member.isCurrentUser) {
        updateSessionSpaces(session.spaces.filter((space) => space.id !== activeSpaceId));
      }

      toast.success("Pessoa removida do espaço.");
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
    if (!session || !activeSpaceId || !activity.canEdit || activity.status === nextStatus) {
      return;
    }

    try {
      const updated = await apiFetch<Activity>(`/api/activities/${activity.id}/status`, {
        method: "PATCH",
        token: session.accessToken,
        spaceId: activeSpaceId,
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
    if (!session || !activeSpaceId || !activity.canEdit || activity.status === nextStatus) {
      return;
    }

    const mutationVersion = (activityStatusMutationVersionRef.current[activity.id] ?? 0) + 1;
    activityStatusMutationVersionRef.current[activity.id] = mutationVersion;

    const previousActivity = activities.find((item) => item.id === activity.id) ?? activity;
    const optimisticActivity = {
      ...previousActivity,
      status: nextStatus,
      completedAt: nextStatus === "Concluido" ? new Date().toISOString() : null,
    };

    replaceActivityInState(optimisticActivity);
    setProjects((current) => updateProjectActivityCounts(current, previousActivity, optimisticActivity));

    try {
      const updated = await apiFetch<Activity>(`/api/activities/${activity.id}/status`, {
        method: "PATCH",
        token: session.accessToken,
        spaceId: activeSpaceId,
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
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      const created = await apiFetch<ActivityComment>(`/api/activities/${activityId}/comments`, {
        method: "POST",
        token: session.accessToken,
        spaceId: activeSpaceId,
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
      toast.success("Comentário publicada.");
    } catch (exception) {
      reportError(exception, "Não foi possível comentar.");
    }
  }

  async function updateComment(activityId: string, commentId: string, body: string) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      const updated = await apiFetch<ActivityComment>(`/api/activities/${activityId}/comments/${commentId}`, {
        method: "PUT",
        token: session.accessToken,
        spaceId: activeSpaceId,
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
      !activeSpaceId ||
      !comment.canDelete ||
      !window.confirm("Excluir este comentário?")
    ) {
      return;
    }

    try {
      await apiFetch<void>(`/api/activities/${activityId}/comments/${comment.id}`, {
        method: "DELETE",
        token: session.accessToken,
        spaceId: activeSpaceId,
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
    setSelectedCoreId("");
    setSelectedProjectId("");
  }

  function selectCoreScope(coreId: string) {
    setSelectedCoreId(coreId);
    setSelectedProjectId("");
  }

  function selectProjectScope(project: Project) {
    setSelectedCoreId(project.coreId);
    setSelectedProjectId(project.id);
  }

  async function saveEffortPlan(
    allocations: Array<{ scopeType: EffortScopeType; scopeId?: string | null; weekday: EffortWeekday; points: number }>,
  ) {
    if (!session || !activeSpaceId) {
      throw new Error("Selecione um espaço antes de salvar o esforço.");
    }

    try {
      const nextPlan = await apiFetch<EffortPlan>("/api/effort-plan", {
        method: "PUT",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify({ allocations }),
      });
      setEffortPlan(nextPlan);
      toast.success("Esforço semanal atualizado.");
    } catch (exception) {
      reportError(exception, "Não foi possível salvar o esforço semanal.");
    }
  }

  return {
    session,
    activeSpaceId,
    activeSpace,
    cores,
    projects,
    activities,
    effortPlan,
    relevance,
    members,
    selectedCoreId,
    selectedProjectId,
    filters,
    viewMode,
    sidebarCollapsed,
    theme,
    activeModal,
    editingSpace,
    editingCore,
    editingProject,
    editingActivity,
    activityDraftProjectId,
    selectedActivity: selectedActivitySnapshot,
    canAssignActivityToMe,
    activityComments,
    commentsLoading,
    loading,
    error,
    canShareSpace,
    canManageSpace,
    filteredProjects,
    activityDialogProjects,
    visibleActivities,
    showOldCompleted,
    setShowOldCompleted,
    hasOldCompletedActivities,
    hasHiddenOldCompletedSearchMatch,
    groupedActivities,
    selectedScopeLabel,
    setError,
    setViewMode,
    setSidebarCollapsed,
    setTheme,
    updateFilter,
    resetFilters,
    handleAuthenticated,
    handleSpaceChange,
    handleLogout,
    loadWorkspace,
    refreshSpaces,
    createSpace,
    updateSpace,
    deleteSpace,
    openCreateSpace,
    openEditSpace,
    openCreateCore,
    openEditCore,
    openCreateProject,
    openEditProject,
    openCreateActivity,
    openEffortPlan,
    openShareSpace,
    openEditActivity,
    closeModal,
    createCore,
    updateCore,
    deleteCore,
    createProject,
    updateProject,
    deleteProject,
    createActivity,
    updateActivity,
    assignActivityToMe,
    deleteActivity,
    shareSpace,
    updateSpaceMember,
    removeSpaceMember,
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
    selectCoreScope,
    selectProjectScope,
    saveEffortPlan,
  };
}

export type ProjectDashboardController = ReturnType<typeof useProjectDashboard>;

"use client";

import { useCallback, useDeferredValue, useEffect, useMemo, useRef, useState } from "react";
import { toast } from "sonner";
import {
  PromptCategory,
  PromptDetail,
  PromptListItem,
  PromptListResponse,
  AuthResponse,
  Space,
  SpaceMember,
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
import { defaultAppTheme, uiStorageKeys } from "@/features/projects/project-dashboard.constants";
import type { AppTheme } from "@/features/projects/project-dashboard.types";
import { getErrorMessage } from "@/features/projects/project-dashboard.utils";

type PromptActiveModal = "space" | "share" | "prompt" | "category" | null;
export type PromptViewMode = "grid" | "list";

export type PromptFormInput = {
  coreId?: string;
  title: string;
  description?: string;
  promptText: string;
  categoryIds: string[];
  linkUrl?: string;
  linkTitle?: string;
  imageFile?: File | null;
  removeImage?: boolean;
};

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

export function readStoredPromptImagesHidden() {
  if (typeof window === "undefined") {
    return false;
  }

  try {
    return window.localStorage.getItem(uiStorageKeys.promptImagesHidden) === "true";
  } catch {
    return false;
  }
}

export function storePromptImagesHidden(hidden: boolean) {
  if (typeof window === "undefined") {
    return;
  }

  try {
    if (hidden) {
      window.localStorage.setItem(uiStorageKeys.promptImagesHidden, "true");
      return;
    }

    window.localStorage.removeItem(uiStorageKeys.promptImagesHidden);
  } catch {
    // Ignore storage failures so a restricted browser does not block the UI.
  }
}

export function usePromptBank() {
  const [session, setSession] = useState<AuthResponse | null>(null);
  const [activeSpaceId, setActiveSpaceId] = useState("");
  const [cores, setCores] = useState<Core[]>([]);
  const [members, setMembers] = useState<SpaceMember[]>([]);
  const [categories, setCategories] = useState<PromptCategory[]>([]);
  const [promptPage, setPromptPage] = useState<PromptListResponse>({ items: [], page: 1, pageSize: 12, totalCount: 0 });
  const [search, setSearch] = useState("");
  const deferredSearch = useDeferredValue(search);
  const [coreFilter, setCoreFilter] = useState("all");
  const [selectedCategoryIds, setSelectedCategoryIds] = useState<string[]>([]);
  const [viewMode, setViewModeState] = useState<PromptViewMode>("grid");
  const [page, setPage] = useState(1);
  const [archivedOnly, setArchivedOnlyState] = useState(false);
  const [showImages, setShowImagesState] = useState(true);
  const [sidebarCollapsed, setSidebarCollapsedState] = useState(false);
  const [theme, setThemeState] = useState<AppTheme>(defaultAppTheme);
  const [activeModal, setActiveModal] = useState<PromptActiveModal>(null);
  const [editingSpace, setEditingSpace] = useState<Space | null>(null);
  const [editingPrompt, setEditingPrompt] = useState<PromptDetail | null>(null);
  const [editingCategory, setEditingCategory] = useState<PromptCategory | null>(null);
  const [deletingCategory, setDeletingCategory] = useState<PromptCategory | null>(null);
  const [selectedPromptDetail, setSelectedPromptDetail] = useState<PromptDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [loadingReferences, setLoadingReferences] = useState(false);
  const [loadingPrompts, setLoadingPrompts] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const sessionUserIdRef = useRef<string | null>(null);
  const activeSpaceIdRef = useRef("");

  const resetWorkspaceState = useCallback(() => {
    setCores([]);
    setMembers([]);
    setCategories([]);
    setPromptPage({ items: [], page: 1, pageSize: 12, totalCount: 0 });
    setSearch("");
    setCoreFilter("all");
    setSelectedCategoryIds([]);
    setViewModeState("grid");
    setPage(1);
    setArchivedOnlyState(false);
    setEditingSpace(null);
    setEditingPrompt(null);
    setEditingCategory(null);
    setDeletingCategory(null);
    setSelectedPromptDetail(null);
    setActiveModal(null);
    setDetailLoading(false);
    setLoadingReferences(false);
    setLoadingPrompts(false);
    setError(null);
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
      const hasSpaces = Boolean(nextSession && nextSession.spaces.length > 0);
      setLoadingReferences(hasSpaces);
      setLoadingPrompts(hasSpaces);
    },
    [resetWorkspaceState],
  );

  useEffect(() => {
    let cancelled = false;
    const savedSidebarState = window.localStorage.getItem(uiStorageKeys.sidebarCollapsed);
    const savedTheme = window.localStorage.getItem(uiStorageKeys.theme);
    const savedPromptImagesHidden = window.localStorage.getItem(uiStorageKeys.promptImagesHidden);

    void Promise.resolve().then(() => {
      if (cancelled) {
        return;
      }

      syncSession(readSession());

      if (savedSidebarState === "true" || savedSidebarState === "false") {
        setSidebarCollapsedState(savedSidebarState === "true");
      }

      if (isAppTheme(savedTheme)) {
        setThemeState(savedTheme);
        applyDocumentTheme(savedTheme);
      } else {
        applyDocumentTheme(defaultAppTheme);
      }

      setShowImagesState(savedPromptImagesHidden !== "true");
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
  const loading = loadingReferences || loadingPrompts || detailLoading;

  const selectedCore = useMemo(() => {
    return cores.find((core) => core.id === coreFilter) ?? null;
  }, [coreFilter, cores]);

  const subtitle = useMemo(() => {
    const archivedPrefix = archivedOnly ? "Arquivados" : "Prompts";

    if (coreFilter === "none") {
      return archivedOnly ? "Prompts arquivados sem núcleo" : "Prompts sem núcleo";
    }

    if (selectedCore) {
      return archivedOnly ? `Prompts arquivados em ${selectedCore.name}` : `Prompts em ${selectedCore.name}`;
    }

    if (selectedCategoryIds.length === 1) {
      const category = categories.find((item) => item.id === selectedCategoryIds[0]);
      if (category) {
        return archivedOnly ? `${archivedPrefix} por ${category.name}` : `Filtrado por ${category.name}`;
      }
    }

    if (selectedCategoryIds.length > 1) {
      return archivedOnly ? `${archivedPrefix} por ${selectedCategoryIds.length} categorias` : `Filtrado por ${selectedCategoryIds.length} categorias`;
    }

    return archivedOnly ? "Banco de prompts arquivados" : "Banco de Prompts";
  }, [archivedOnly, categories, selectedCategoryIds, selectedCore, coreFilter]);

  const imageCount = useMemo(() => promptPage.items.filter((item) => item.hasImage).length, [promptPage.items]);

  const reportError = useCallback((exception: unknown, fallback: string) => {
    const message = getErrorMessage(exception, fallback);
    setError(message);
    toast.error(message);

    if (exception instanceof Error) {
      throw exception;
    }

    throw new Error(message);
  }, []);

  const loadReferenceData = useCallback(
    async (token = session?.accessToken, spaceId = activeSpaceId) => {
      if (!token || !spaceId || (session?.user.accountState ?? "Active") !== "Active") {
        return;
      }

      setLoadingReferences(true);
      setError(null);
      try {
        const [nextCores, nextMembers, nextCategories] = await Promise.all([
          apiFetch<Core[]>("/api/cores", { token, spaceId }),
          apiFetch<SpaceMember[]>("/api/spaces/members", { token, spaceId }),
          apiFetch<PromptCategory[]>("/api/prompt-categories", { token, spaceId }),
        ]);

        setCores(nextCores);
        setMembers(nextMembers);
        setCategories(nextCategories);
      } catch (exception) {
        setError(getErrorMessage(exception, "Falha ao carregar referências."));
      } finally {
        setLoadingReferences(false);
      }
    },
    [activeSpaceId, session?.accessToken, session?.user],
  );

  const loadPrompts = useCallback(
    async (
      token = session?.accessToken,
      spaceId = activeSpaceId,
      options?: {
        search?: string;
        coreFilter?: string;
        categoryIds?: string[];
        page?: number;
        archivedOnly?: boolean;
      },
    ) => {
      if (!token || !spaceId || (session?.user.accountState ?? "Active") !== "Active") {
        return;
      }

      const nextSearch = options?.search ?? deferredSearch;
      const nextCoreFilter = options?.coreFilter ?? coreFilter;
      const nextCategoryIds = options?.categoryIds ?? selectedCategoryIds;
      const nextPage = options?.page ?? page;
      const nextArchivedOnly = options?.archivedOnly ?? archivedOnly;
      const query = new URLSearchParams();
      if (nextSearch.trim()) {
        query.set("search", nextSearch.trim());
      }

      if (nextCoreFilter === "none") {
        query.set("withoutCore", "true");
      } else if (nextCoreFilter !== "all") {
        query.set("coreId", nextCoreFilter);
      }

      nextCategoryIds.forEach((categoryId) => query.append("categoryId", categoryId));
      if (nextArchivedOnly) {
        query.set("archivedOnly", "true");
      }
      query.set("page", String(nextPage));
      query.set("pageSize", String(promptPage.pageSize));

      setLoadingPrompts(true);
      setError(null);
      try {
        const response = await apiFetch<PromptListResponse>(`/api/prompts?${query.toString()}`, {
          token,
          spaceId,
        });
        setPromptPage(response);
      } catch (exception) {
        setError(getErrorMessage(exception, "Falha ao carregar prompts."));
      } finally {
        setLoadingPrompts(false);
      }
    },
    [activeSpaceId, archivedOnly, deferredSearch, page, promptPage.pageSize, selectedCategoryIds, session?.accessToken, session?.user, coreFilter],
  );

  const refreshWorkspace = useCallback(async () => {
    if (!session || !activeSpaceId || !isAccountActive) {
      return;
    }

    await Promise.all([
      loadReferenceData(session.accessToken, activeSpaceId),
      loadPrompts(session.accessToken, activeSpaceId),
    ]);
  }, [activeSpaceId, isAccountActive, loadPrompts, loadReferenceData, session]);

  useEffect(() => {
    if (!session || !activeSpaceId || !isAccountActive) {
      return;
    }

    const timer = window.setTimeout(() => {
      void loadReferenceData(session.accessToken, activeSpaceId);
    }, 0);

    return () => window.clearTimeout(timer);
  }, [activeSpaceId, isAccountActive, loadReferenceData, session]);

  useEffect(() => {
    if (!session || !activeSpaceId || !isAccountActive) {
      return;
    }

    const timer = window.setTimeout(() => {
      void loadPrompts(session.accessToken, activeSpaceId);
    }, 0);

    return () => window.clearTimeout(timer);
  }, [activeSpaceId, archivedOnly, deferredSearch, isAccountActive, loadPrompts, page, selectedCategoryIds, session, coreFilter]);

  const handleAuthenticated = useCallback((auth: AuthResponse) => {
    storeSession(auth);
    toast.success("Sessão iniciada com sucesso.");
  }, []);

  const handleSpaceChange = useCallback((spaceId: string) => {
    setLoadingReferences(true);
    setLoadingPrompts(true);
    setCores([]);
    setMembers([]);
    setCategories([]);
    setPromptPage({ items: [], page: 1, pageSize: 12, totalCount: 0 });
    setActiveSpaceId(spaceId);
    setSearch("");
    setCoreFilter("all");
    setSelectedCategoryIds([]);
    setPage(1);
    setArchivedOnlyState(false);
    setSelectedPromptDetail(null);
    setEditingPrompt(null);
    setEditingCategory(null);
    setDeletingCategory(null);
    setDetailLoading(false);
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

    setLoadingReferences(true);
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
      setLoadingReferences(false);
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
      setMembers([]);
      setCategories([]);
      setPromptPage({ items: [], page: 1, pageSize: 12, totalCount: 0 });
      setSelectedPromptDetail(null);
      setEditingPrompt(null);
      setEditingCategory(null);
      setDeletingCategory(null);
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

  function openShareSpace() {
    setActiveModal("share");
  }

  function openCreatePrompt() {
    setEditingPrompt(null);
    setActiveModal("prompt");
  }

  async function openEditPrompt(promptId: string) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      const detail = await apiFetch<PromptDetail>(`/api/prompts/${promptId}`, {
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      setEditingPrompt(detail);
      setActiveModal("prompt");
    } catch (exception) {
      reportError(exception, "Não foi possível carregar o prompt para edição.");
    }
  }

  function openCreateCategory() {
    setEditingCategory(null);
    setActiveModal("category");
  }

  function openEditCategory(category: PromptCategory) {
    if (!category.canEdit) {
      return;
    }

    setEditingCategory(category);
    setActiveModal("category");
  }

  function openDeleteCategory(category: PromptCategory) {
    if (!category.canDelete) {
      return;
    }

    setDeletingCategory(category);
  }

  function closeCommonModal() {
    if (activeModal === "space" || activeModal === "share") {
      setActiveModal(null);
      setEditingSpace(null);
    }
  }

  function closeModuleModal() {
    setActiveModal(null);
    setEditingPrompt(null);
    setEditingCategory(null);
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

  async function createPrompt(input: PromptFormInput) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      const created = await apiFetch<PromptDetail>("/api/prompts", {
        method: "POST",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify({
          coreId: input.coreId || null,
          title: input.title,
          description: input.description || null,
          promptText: input.promptText,
          categoryIds: input.categoryIds,
          linkUrl: input.linkUrl || null,
          linkTitle: input.linkTitle || null,
        }),
      });

      if (input.imageFile) {
        const formData = new FormData();
        formData.append("file", input.imageFile);
        await apiFetch<PromptDetail>(`/api/prompts/${created.id}/image`, {
          method: "POST",
          token: session.accessToken,
          spaceId: activeSpaceId,
          body: formData,
        });
      }

      await refreshWorkspace();
      toast.success("Prompt criado.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar o prompt.");
    }
  }

  async function updatePrompt(promptId: string, input: PromptFormInput) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      await apiFetch<PromptDetail>(`/api/prompts/${promptId}`, {
        method: "PUT",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify({
          coreId: input.coreId || null,
          title: input.title,
          description: input.description || null,
          promptText: input.promptText,
          categoryIds: input.categoryIds,
          linkUrl: input.linkUrl || null,
          linkTitle: input.linkTitle || null,
        }),
      });

      if (input.imageFile) {
        const formData = new FormData();
        formData.append("file", input.imageFile);
        await apiFetch<PromptDetail>(`/api/prompts/${promptId}/image`, {
          method: "POST",
          token: session.accessToken,
          spaceId: activeSpaceId,
          body: formData,
        });
      } else if (input.removeImage) {
        await apiFetch<PromptDetail>(`/api/prompts/${promptId}/image`, {
          method: "DELETE",
          token: session.accessToken,
          spaceId: activeSpaceId,
        });
      }

      await refreshWorkspace();
      if (selectedPromptDetail?.id === promptId) {
        await openPrompt(promptId);
      }
      toast.success("Prompt atualizado.");
    } catch (exception) {
      reportError(exception, "Não foi possível salvar o prompt.");
    }
  }

  async function deletePrompt(prompt: PromptListItem | PromptDetail) {
    if (!session || !activeSpaceId || !prompt.canDelete || !window.confirm(`Excluir o prompt "${prompt.title}"?`)) {
      return;
    }

    try {
      await apiFetch<void>(`/api/prompts/${prompt.id}`, {
        method: "DELETE",
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      if (selectedPromptDetail?.id === prompt.id) {
        setSelectedPromptDetail(null);
      }
      await refreshWorkspace();
      toast.success("Prompt excluído.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir o prompt.");
    }
  }

  async function setPromptArchived(promptId: string, isArchived: boolean) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      const updated = await apiFetch<PromptDetail>(`/api/prompts/${promptId}/archive`, {
        method: isArchived ? "POST" : "DELETE",
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      if (selectedPromptDetail?.id === promptId) {
        setSelectedPromptDetail(updated);
      }
      await refreshWorkspace();
      toast.success(isArchived ? "Prompt arquivado." : "Prompt desarquivado.");
    } catch (exception) {
      reportError(exception, isArchived ? "Não foi possível arquivar o prompt." : "Não foi possível desarquivar o prompt.");
    }
  }

  async function openPrompt(promptId: string) {
    if (!session || !activeSpaceId) {
      return;
    }

    setDetailLoading(true);
    try {
      const detail = await apiFetch<PromptDetail>(`/api/prompts/${promptId}`, {
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      setSelectedPromptDetail(detail);
    } catch (exception) {
      setError(getErrorMessage(exception, "Falha ao carregar o prompt."));
    } finally {
      setDetailLoading(false);
    }
  }

  function closePrompt() {
    setSelectedPromptDetail(null);
  }

  async function createCategory(name: string) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      await apiFetch<PromptCategory>("/api/prompt-categories", {
        method: "POST",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify({ name }),
      });
      await refreshWorkspace();
      toast.success("Categoria criada.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar a categoria.");
    }
  }

  async function updateCategory(categoryId: string, name: string) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      await apiFetch<PromptCategory>(`/api/prompt-categories/${categoryId}`, {
        method: "PUT",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify({ name }),
      });
      await refreshWorkspace();
      toast.success("Categoria atualizada.");
    } catch (exception) {
      reportError(exception, "Não foi possível salvar a categoria.");
    }
  }

  async function deleteCategory(categoryId: string, replacementCategoryId?: string) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      const query = replacementCategoryId ? `?replacementCategoryId=${replacementCategoryId}` : "";
      await apiFetch<void>(`/api/prompt-categories/${categoryId}${query}`, {
        method: "DELETE",
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      setSelectedCategoryIds((current) => current.filter((id) => id !== categoryId));
      setDeletingCategory(null);
      await refreshWorkspace();
      toast.success("Categoria excluída.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir a categoria.");
    }
  }

  function toggleCategoryFilter(categoryId: string) {
    setPage(1);
    setSelectedCategoryIds((current) =>
      current.includes(categoryId) ? current.filter((item) => item !== categoryId) : [...current, categoryId],
    );
  }

  function setCoreFilterValue(value: string) {
    setPage(1);
    setCoreFilter(value);
  }

  function setSearchValue(value: string) {
    setPage(1);
    setSearch(value);
  }

  const totalPages = Math.max(1, Math.ceil(promptPage.totalCount / promptPage.pageSize));

  return {
    session,
    activeSpaceId,
    activeSpace,
    cores,
    members,
    categories,
    promptPage,
    search,
    coreFilter,
    selectedCategoryIds,
    viewMode,
    page,
    archivedOnly,
    showImages,
    totalPages,
    subtitle,
    imageCount,
    sidebarCollapsed,
    theme,
    activeModal,
    editingSpace,
    editingPrompt,
    editingCategory,
    deletingCategory,
    selectedPromptDetail,
    detailLoading,
    loading,
    error,
    canShareSpace,
    canManageSpace,
    setError,
    setSidebarCollapsed: (collapsed: boolean) => {
      setSidebarCollapsedState(collapsed);
      window.localStorage.setItem(uiStorageKeys.sidebarCollapsed, String(collapsed));
    },
    setTheme: (nextTheme: AppTheme) => {
      setThemeState(nextTheme);
      applyDocumentTheme(nextTheme);
      window.localStorage.setItem(uiStorageKeys.theme, nextTheme);
    },
    setSearchValue,
    setCoreFilterValue,
    toggleCategoryFilter,
    setViewMode: (mode: PromptViewMode) => {
      setViewModeState(mode);
    },
    setPage,
    setArchivedOnly: (value: boolean) => {
      setPage(1);
      setArchivedOnlyState(value);
    },
    setShowImages: (value: boolean) => {
      setShowImagesState(value);
      storePromptImagesHidden(!value);
    },
    handleAuthenticated,
    handleSpaceChange,
    handleLogout,
    refreshSpaces,
    refreshWorkspace,
    createSpace,
    updateSpace,
    deleteSpace,
    openCreateSpace,
    openEditSpace,
    openShareSpace,
    closeCommonModal,
    closeModuleModal,
    shareSpace,
    updateProfile,
    openCreatePrompt,
    openEditPrompt,
    createPrompt,
    updatePrompt,
    deletePrompt,
    openPrompt,
    closePrompt,
    setPromptArchived,
    openCreateCategory,
    openEditCategory,
    openDeleteCategory,
    createCategory,
    updateCategory,
    deleteCategory,
    setDeletingCategory,
  };
}

export type PromptBankController = ReturnType<typeof usePromptBank>;

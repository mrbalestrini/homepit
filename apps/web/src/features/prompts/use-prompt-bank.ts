"use client";

import { useCallback, useDeferredValue, useEffect, useMemo, useRef, useState } from "react";
import { toast } from "sonner";
import {
  PromptCategory,
  PromptDetail,
  PromptListItem,
  PromptListResponse,
  AuthResponse,
  Household,
  HouseholdMember,
  Universe,
  apiFetch,
  clearSession,
  readSession,
  storeSession,
  subscribeToSessionChanges,
  updateStoredSession,
} from "@/lib/api";
import {
  clearStoredActiveHouseholdId,
  readStoredActiveHouseholdId,
  resolveActiveHouseholdSelection,
  storeActiveHouseholdId,
} from "@/lib/household-selection";
import { defaultAppTheme, uiStorageKeys } from "@/features/projects/project-dashboard.constants";
import type { AppTheme } from "@/features/projects/project-dashboard.types";
import { getErrorMessage } from "@/features/projects/project-dashboard.utils";

type PromptActiveModal = "household" | "share" | "prompt" | "category" | null;

export type PromptFormInput = {
  universeId?: string;
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
  return value === "cozy" || value === "earthy" || value === "dark";
}

function applyDocumentTheme(theme: AppTheme) {
  document.documentElement.dataset.theme = theme;
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
  const [activeHouseholdId, setActiveHouseholdId] = useState("");
  const [universes, setUniverses] = useState<Universe[]>([]);
  const [members, setMembers] = useState<HouseholdMember[]>([]);
  const [categories, setCategories] = useState<PromptCategory[]>([]);
  const [promptPage, setPromptPage] = useState<PromptListResponse>({ items: [], page: 1, pageSize: 12, totalCount: 0 });
  const [search, setSearch] = useState("");
  const deferredSearch = useDeferredValue(search);
  const [universeFilter, setUniverseFilter] = useState("all");
  const [selectedCategoryIds, setSelectedCategoryIds] = useState<string[]>([]);
  const [page, setPage] = useState(1);
  const [archivedOnly, setArchivedOnlyState] = useState(false);
  const [showImages, setShowImagesState] = useState(true);
  const [sidebarCollapsed, setSidebarCollapsedState] = useState(false);
  const [theme, setThemeState] = useState<AppTheme>(defaultAppTheme);
  const [activeModal, setActiveModal] = useState<PromptActiveModal>(null);
  const [editingHousehold, setEditingHousehold] = useState<Household | null>(null);
  const [editingPrompt, setEditingPrompt] = useState<PromptDetail | null>(null);
  const [editingCategory, setEditingCategory] = useState<PromptCategory | null>(null);
  const [deletingCategory, setDeletingCategory] = useState<PromptCategory | null>(null);
  const [selectedPromptDetail, setSelectedPromptDetail] = useState<PromptDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [loadingReferences, setLoadingReferences] = useState(false);
  const [loadingPrompts, setLoadingPrompts] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const sessionUserIdRef = useRef<string | null>(null);
  const activeHouseholdIdRef = useRef("");

  const resetWorkspaceState = useCallback(() => {
    setUniverses([]);
    setMembers([]);
    setCategories([]);
    setPromptPage({ items: [], page: 1, pageSize: 12, totalCount: 0 });
    setSearch("");
    setUniverseFilter("all");
    setSelectedCategoryIds([]);
    setPage(1);
    setArchivedOnlyState(false);
    setEditingHousehold(null);
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
        setActiveHouseholdId("");
        return;
      }

      const storedHouseholdId = readStoredActiveHouseholdId(nextSession.user.id);
      const { householdId, shouldClearStoredHouseholdId } = resolveActiveHouseholdSelection(
        nextSession.households,
        activeHouseholdIdRef.current,
        storedHouseholdId,
      );

      if (shouldClearStoredHouseholdId) {
        clearStoredActiveHouseholdId(nextSession.user.id);
      }

      setActiveHouseholdId(householdId);
      const hasHouseholds = Boolean(nextSession && nextSession.households.length > 0);
      setLoadingReferences(hasHouseholds);
      setLoadingPrompts(hasHouseholds);
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
    activeHouseholdIdRef.current = activeHouseholdId;
  }, [activeHouseholdId]);

  useEffect(() => {
    const userId = session?.user.id;

    if (!userId) {
      return;
    }

    if (activeHouseholdId) {
      storeActiveHouseholdId(userId, activeHouseholdId);
      return;
    }

    clearStoredActiveHouseholdId(userId);
  }, [activeHouseholdId, session?.user.id]);

  const activeHousehold = useMemo(() => {
    return session?.households.find((household) => household.id === activeHouseholdId) ?? null;
  }, [activeHouseholdId, session?.households]);
  const isAccountActive = (session?.user.accountState ?? "Active") === "Active";

  const canShareHousehold = activeHousehold?.role === "Owner" || activeHousehold?.role === "Admin";
  const canManageHousehold = activeHousehold?.role === "Owner";
  const loading = loadingReferences || loadingPrompts || detailLoading;

  const selectedUniverse = useMemo(() => {
    return universes.find((universe) => universe.id === universeFilter) ?? null;
  }, [universeFilter, universes]);

  const subtitle = useMemo(() => {
    const archivedPrefix = archivedOnly ? "Arquivados" : "Prompts";

    if (universeFilter === "none") {
      return archivedOnly ? "Prompts arquivados sem universo" : "Prompts sem universo";
    }

    if (selectedUniverse) {
      return archivedOnly ? `Prompts arquivados em ${selectedUniverse.name}` : `Prompts em ${selectedUniverse.name}`;
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
  }, [archivedOnly, categories, selectedCategoryIds, selectedUniverse, universeFilter]);

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
    async (token = session?.accessToken, householdId = activeHouseholdId) => {
      if (!token || !householdId || (session?.user.accountState ?? "Active") !== "Active") {
        return;
      }

      setLoadingReferences(true);
      setError(null);
      try {
        const [nextUniverses, nextMembers, nextCategories] = await Promise.all([
          apiFetch<Universe[]>("/api/universes", { token, householdId }),
          apiFetch<HouseholdMember[]>("/api/households/members", { token, householdId }),
          apiFetch<PromptCategory[]>("/api/prompt-categories", { token, householdId }),
        ]);

        setUniverses(nextUniverses);
        setMembers(nextMembers);
        setCategories(nextCategories);
      } catch (exception) {
        setError(getErrorMessage(exception, "Falha ao carregar referências."));
      } finally {
        setLoadingReferences(false);
      }
    },
    [activeHouseholdId, session?.accessToken, session?.user],
  );

  const loadPrompts = useCallback(
    async (
      token = session?.accessToken,
      householdId = activeHouseholdId,
      options?: {
        search?: string;
        universeFilter?: string;
        categoryIds?: string[];
        page?: number;
        archivedOnly?: boolean;
      },
    ) => {
      if (!token || !householdId || (session?.user.accountState ?? "Active") !== "Active") {
        return;
      }

      const nextSearch = options?.search ?? deferredSearch;
      const nextUniverseFilter = options?.universeFilter ?? universeFilter;
      const nextCategoryIds = options?.categoryIds ?? selectedCategoryIds;
      const nextPage = options?.page ?? page;
      const nextArchivedOnly = options?.archivedOnly ?? archivedOnly;
      const query = new URLSearchParams();
      if (nextSearch.trim()) {
        query.set("search", nextSearch.trim());
      }

      if (nextUniverseFilter === "none") {
        query.set("withoutUniverse", "true");
      } else if (nextUniverseFilter !== "all") {
        query.set("universeId", nextUniverseFilter);
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
          householdId,
        });
        setPromptPage(response);
      } catch (exception) {
        setError(getErrorMessage(exception, "Falha ao carregar prompts."));
      } finally {
        setLoadingPrompts(false);
      }
    },
    [activeHouseholdId, archivedOnly, deferredSearch, page, promptPage.pageSize, selectedCategoryIds, session?.accessToken, session?.user, universeFilter],
  );

  const refreshWorkspace = useCallback(async () => {
    if (!session || !activeHouseholdId || !isAccountActive) {
      return;
    }

    await Promise.all([
      loadReferenceData(session.accessToken, activeHouseholdId),
      loadPrompts(session.accessToken, activeHouseholdId),
    ]);
  }, [activeHouseholdId, isAccountActive, loadPrompts, loadReferenceData, session]);

  useEffect(() => {
    if (!session || !activeHouseholdId || !isAccountActive) {
      return;
    }

    const timer = window.setTimeout(() => {
      void loadReferenceData(session.accessToken, activeHouseholdId);
    }, 0);

    return () => window.clearTimeout(timer);
  }, [activeHouseholdId, isAccountActive, loadReferenceData, session]);

  useEffect(() => {
    if (!session || !activeHouseholdId || !isAccountActive) {
      return;
    }

    const timer = window.setTimeout(() => {
      void loadPrompts(session.accessToken, activeHouseholdId);
    }, 0);

    return () => window.clearTimeout(timer);
  }, [activeHouseholdId, archivedOnly, deferredSearch, isAccountActive, loadPrompts, page, selectedCategoryIds, session, universeFilter]);

  const handleAuthenticated = useCallback((auth: AuthResponse) => {
    storeSession(auth);
    toast.success("Sessão iniciada com sucesso.");
  }, []);

  const handleHouseholdChange = useCallback((householdId: string) => {
    setLoadingReferences(true);
    setLoadingPrompts(true);
    setUniverses([]);
    setMembers([]);
    setCategories([]);
    setPromptPage({ items: [], page: 1, pageSize: 12, totalCount: 0 });
    setActiveHouseholdId(householdId);
    setSearch("");
    setUniverseFilter("all");
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

      const storedHouseholdId = readStoredActiveHouseholdId(nextSession.user.id);
      const { householdId, shouldClearStoredHouseholdId } = resolveActiveHouseholdSelection(
        nextHouseholds,
        activeHouseholdIdRef.current,
        storedHouseholdId,
        preferredHouseholdId,
      );

      if (shouldClearStoredHouseholdId) {
        clearStoredActiveHouseholdId(nextSession.user.id);
      }

      setActiveHouseholdId(householdId);
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

  const refreshHouseholds = useCallback(async () => {
    if (!session) {
      return;
    }

    setLoadingReferences(true);
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
      setLoadingReferences(false);
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
    if (!session) {
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
      setMembers([]);
      setCategories([]);
      setPromptPage({ items: [], page: 1, pageSize: 12, totalCount: 0 });
      setSelectedPromptDetail(null);
      setEditingPrompt(null);
      setEditingCategory(null);
      setDeletingCategory(null);
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

  function openShareHousehold() {
    setActiveModal("share");
  }

  function openCreatePrompt() {
    setEditingPrompt(null);
    setActiveModal("prompt");
  }

  async function openEditPrompt(promptId: string) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      const detail = await apiFetch<PromptDetail>(`/api/prompts/${promptId}`, {
        token: session.accessToken,
        householdId: activeHouseholdId,
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
    if (activeModal === "household" || activeModal === "share") {
      setActiveModal(null);
      setEditingHousehold(null);
    }
  }

  function closeModuleModal() {
    setActiveModal(null);
    setEditingPrompt(null);
    setEditingCategory(null);
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
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      const created = await apiFetch<PromptDetail>("/api/prompts", {
        method: "POST",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify({
          universeId: input.universeId || null,
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
          householdId: activeHouseholdId,
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
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<PromptDetail>(`/api/prompts/${promptId}`, {
        method: "PUT",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify({
          universeId: input.universeId || null,
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
          householdId: activeHouseholdId,
          body: formData,
        });
      } else if (input.removeImage) {
        await apiFetch<PromptDetail>(`/api/prompts/${promptId}/image`, {
          method: "DELETE",
          token: session.accessToken,
          householdId: activeHouseholdId,
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
    if (!session || !activeHouseholdId || !prompt.canDelete || !window.confirm(`Excluir o prompt "${prompt.title}"?`)) {
      return;
    }

    try {
      await apiFetch<void>(`/api/prompts/${prompt.id}`, {
        method: "DELETE",
        token: session.accessToken,
        householdId: activeHouseholdId,
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
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      const updated = await apiFetch<PromptDetail>(`/api/prompts/${promptId}/archive`, {
        method: isArchived ? "POST" : "DELETE",
        token: session.accessToken,
        householdId: activeHouseholdId,
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
    if (!session || !activeHouseholdId) {
      return;
    }

    setDetailLoading(true);
    try {
      const detail = await apiFetch<PromptDetail>(`/api/prompts/${promptId}`, {
        token: session.accessToken,
        householdId: activeHouseholdId,
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
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<PromptCategory>("/api/prompt-categories", {
        method: "POST",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify({ name }),
      });
      await refreshWorkspace();
      toast.success("Categoria criada.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar a categoria.");
    }
  }

  async function updateCategory(categoryId: string, name: string) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<PromptCategory>(`/api/prompt-categories/${categoryId}`, {
        method: "PUT",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify({ name }),
      });
      await refreshWorkspace();
      toast.success("Categoria atualizada.");
    } catch (exception) {
      reportError(exception, "Não foi possível salvar a categoria.");
    }
  }

  async function deleteCategory(categoryId: string, replacementCategoryId?: string) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      const query = replacementCategoryId ? `?replacementCategoryId=${replacementCategoryId}` : "";
      await apiFetch<void>(`/api/prompt-categories/${categoryId}${query}`, {
        method: "DELETE",
        token: session.accessToken,
        householdId: activeHouseholdId,
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

  function setUniverseFilterValue(value: string) {
    setPage(1);
    setUniverseFilter(value);
  }

  function setSearchValue(value: string) {
    setPage(1);
    setSearch(value);
  }

  const totalPages = Math.max(1, Math.ceil(promptPage.totalCount / promptPage.pageSize));

  return {
    session,
    activeHouseholdId,
    activeHousehold,
    universes,
    members,
    categories,
    promptPage,
    search,
    universeFilter,
    selectedCategoryIds,
    page,
    archivedOnly,
    showImages,
    totalPages,
    subtitle,
    imageCount,
    sidebarCollapsed,
    theme,
    activeModal,
    editingHousehold,
    editingPrompt,
    editingCategory,
    deletingCategory,
    selectedPromptDetail,
    detailLoading,
    loading,
    error,
    canShareHousehold,
    canManageHousehold,
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
    setUniverseFilterValue,
    toggleCategoryFilter,
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
    handleHouseholdChange,
    handleLogout,
    refreshHouseholds,
    refreshWorkspace,
    createHousehold,
    updateHousehold,
    deleteHousehold,
    openCreateHousehold,
    openEditHousehold,
    openShareHousehold,
    closeCommonModal,
    closeModuleModal,
    shareHousehold,
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

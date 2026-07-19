"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { toast } from "sonner";
import {
  type AuthResponse,
  type GsmNumber,
  type GsmRecharge,
  type GsmNumberPlan,
  type GsmNumberStatus,
  type Space,
  type SpaceMember,
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
import { sortGsmNumbersByUrgency } from "./gsm-dashboard.utils";

type GsmActiveModal = "space" | "share" | "gsm" | "recharge-history" | "recharge" | null;

export type GsmFormInput = {
  title: string;
  number: string;
  description?: string;
  plan: GsmNumberPlan;
  monthlyCost?: number | null;
  daysWithoutRecharge?: number | null;
  acquiredOn: string;
  status: GsmNumberStatus;
};

export type GsmRechargeFormInput = {
  rechargedOn: string;
  amount: number;
  note?: string;
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

export function useGsmDashboard() {
  const [session, setSession] = useState<AuthResponse | null>(null);
  const [activeSpaceId, setActiveSpaceId] = useState("");
  const [members, setMembers] = useState<SpaceMember[]>([]);
  const [gsmNumbers, setGsmNumbers] = useState<GsmNumber[]>([]);
  const [gsmRecharges, setGsmRecharges] = useState<GsmRecharge[]>([]);
  const [sidebarCollapsed, setSidebarCollapsedState] = useState(false);
  const [theme, setThemeState] = useState<AppTheme>(defaultAppTheme);
  const [activeModal, setActiveModal] = useState<GsmActiveModal>(null);
  const [editingSpace, setEditingSpace] = useState<Space | null>(null);
  const [editingGsmNumber, setEditingGsmNumber] = useState<GsmNumber | null>(null);
  const [editingGsmRecharge, setEditingGsmRecharge] = useState<GsmRecharge | null>(null);
  const [selectedRechargeGsmNumber, setSelectedRechargeGsmNumber] = useState<GsmNumber | null>(null);
  const [gsmRechargesLoading, setGsmRechargesLoading] = useState(false);
  const [gsmRechargesError, setGsmRechargesError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const sessionUserIdRef = useRef<string | null>(null);
  const activeSpaceIdRef = useRef("");

  const resetWorkspaceState = useCallback(() => {
    setMembers([]);
    setGsmNumbers([]);
    setGsmRecharges([]);
    setEditingSpace(null);
    setEditingGsmNumber(null);
    setEditingGsmRecharge(null);
    setSelectedRechargeGsmNumber(null);
    setActiveModal(null);
    setGsmRechargesLoading(false);
    setGsmRechargesError(null);
    setLoading(false);
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
      setLoading(Boolean(nextSession.spaces.length > 0));
    },
    [resetWorkspaceState],
  );

  useEffect(() => {
    let cancelled = false;
    const savedSidebarState = window.localStorage.getItem(uiStorageKeys.sidebarCollapsed);
    const savedTheme = window.localStorage.getItem(uiStorageKeys.theme);

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
        const [nextMembers, nextNumbers] = await Promise.all([
          apiFetch<SpaceMember[]>("/api/spaces/members", { token, spaceId }),
          apiFetch<GsmNumber[]>("/api/gsm-numbers", { token, spaceId }),
        ]);

        setMembers(nextMembers);
        setGsmNumbers(sortGsmNumbersByUrgency(nextNumbers));
        setSelectedRechargeGsmNumber((current) => {
          if (!current) {
            return current;
          }

          return nextNumbers.find((item) => item.id === current.id) ?? current;
        });
      } catch (exception) {
        setError(getErrorMessage(exception, "Falha ao carregar os números GSM."));
      } finally {
        setLoading(false);
      }
    },
    [activeSpaceId, session?.accessToken, session?.user],
  );

  useEffect(() => {
    if (!session || !activeSpaceId || !isAccountActive) {
      return;
    }

    const timer = window.setTimeout(() => {
      void loadWorkspace(session.accessToken, activeSpaceId);
    }, 0);

    return () => window.clearTimeout(timer);
  }, [activeSpaceId, isAccountActive, loadWorkspace, session]);

  const handleAuthenticated = useCallback((auth: AuthResponse) => {
    storeSession(auth);
    toast.success("Sessão iniciada com sucesso.");
  }, []);

  const handleSpaceChange = useCallback((spaceId: string) => {
    setLoading(true);
    setMembers([]);
    setGsmNumbers([]);
    setGsmRecharges([]);
    setEditingGsmNumber(null);
    setEditingGsmRecharge(null);
    setSelectedRechargeGsmNumber(null);
    setGsmRechargesError(null);
    setGsmRechargesLoading(false);
    setActiveSpaceId(spaceId);
    setError(null);
    setActiveModal(null);
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

  const applyUpdatedUser = useCallback((updatedUser: AuthResponse["user"]) => {
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
  }, []);

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
      setMembers([]);
      setGsmNumbers([]);
      setGsmRecharges([]);
      setEditingGsmNumber(null);
      setEditingGsmRecharge(null);
      setSelectedRechargeGsmNumber(null);
      setGsmRechargesError(null);
      setGsmRechargesLoading(false);
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

  function closeCommonModal() {
    if (activeModal === "space" || activeModal === "share") {
      setActiveModal(null);
      setEditingSpace(null);
    }
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

  function openCreateGsmNumber() {
    setEditingGsmNumber(null);
    setActiveModal("gsm");
  }

  function openEditGsmNumber(gsmNumber: GsmNumber) {
    if (!gsmNumber.canEdit) {
      return;
    }

    setEditingGsmNumber(gsmNumber);
    setActiveModal("gsm");
  }

  function closeModuleModal() {
    setActiveModal(null);
    setEditingGsmNumber(null);
  }

  async function loadRechargeHistory(gsmNumberId: string) {
    if (!session || !activeSpaceId) {
      return;
    }

    setGsmRechargesLoading(true);
    setGsmRechargesError(null);
    try {
      const history = await apiFetch<GsmRecharge[]>(`/api/gsm-numbers/${gsmNumberId}/recharges`, {
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      setGsmRecharges(history);
    } catch (exception) {
      const message = getErrorMessage(exception, "Falha ao carregar o histórico de recargas.");
      setGsmRechargesError(message);
      toast.error(message);
    } finally {
      setGsmRechargesLoading(false);
    }
  }

  async function refreshRechargeHistory() {
    if (!selectedRechargeGsmNumber) {
      return;
    }

    await loadRechargeHistory(selectedRechargeGsmNumber.id);
  }

  function openRechargeHistory(gsmNumber: GsmNumber) {
    setSelectedRechargeGsmNumber(gsmNumber);
    setEditingGsmRecharge(null);
    setGsmRecharges([]);
    setGsmRechargesError(null);
    setActiveModal("recharge-history");
    void loadRechargeHistory(gsmNumber.id);
  }

  function closeRechargeHistory() {
    setActiveModal(null);
    setSelectedRechargeGsmNumber(null);
    setEditingGsmRecharge(null);
    setGsmRecharges([]);
    setGsmRechargesError(null);
    setGsmRechargesLoading(false);
  }

  function openCreateRecharge(gsmNumber: GsmNumber) {
    if (!gsmNumber.canEdit) {
      return;
    }

    setSelectedRechargeGsmNumber(gsmNumber);
    setEditingGsmRecharge(null);
    setActiveModal("recharge");
    setGsmRechargesError(null);
  }

  function openEditRecharge(gsmNumber: GsmNumber, recharge: GsmRecharge) {
    if (!gsmNumber.canEdit || !recharge.canEdit) {
      return;
    }

    setSelectedRechargeGsmNumber(gsmNumber);
    setEditingGsmRecharge(recharge);
    setActiveModal("recharge");
    setGsmRechargesError(null);
  }

  function closeRechargeModal() {
    setActiveModal(null);
    setEditingGsmRecharge(null);
    setGsmRechargesError(null);
  }

  async function createGsmNumber(input: GsmFormInput) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      await apiFetch<GsmNumber>("/api/gsm-numbers", {
        method: "POST",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify({
          title: input.title,
          number: input.number,
          description: input.description || null,
          plan: input.plan,
          monthlyCost: input.monthlyCost ?? null,
          daysWithoutRecharge: input.daysWithoutRecharge ?? null,
          acquiredOn: input.acquiredOn,
          status: input.status,
        }),
      });
      await loadWorkspace();
      closeModuleModal();
      toast.success("Número GSM cadastrado.");
    } catch (exception) {
      reportError(exception, "Não foi possível cadastrar o número GSM.");
    }
  }

  async function updateGsmNumber(gsmNumberId: string, input: GsmFormInput) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      await apiFetch<GsmNumber>(`/api/gsm-numbers/${gsmNumberId}`, {
        method: "PUT",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify({
          title: input.title,
          number: input.number,
          description: input.description || null,
          plan: input.plan,
          monthlyCost: input.monthlyCost ?? null,
          daysWithoutRecharge: input.daysWithoutRecharge ?? null,
          acquiredOn: input.acquiredOn,
          status: input.status,
        }),
      });
      await loadWorkspace();
      closeModuleModal();
      toast.success("Número GSM atualizado.");
    } catch (exception) {
      reportError(exception, "Não foi possível salvar o número GSM.");
    }
  }

  async function deleteGsmNumber(gsmNumber: GsmNumber) {
    if (!session || !activeSpaceId || !gsmNumber.canDelete) {
      return;
    }

    try {
      await apiFetch<void>(`/api/gsm-numbers/${gsmNumber.id}`, {
        method: "DELETE",
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      await loadWorkspace();
      if (selectedRechargeGsmNumber?.id === gsmNumber.id) {
        closeRechargeHistory();
      }
      toast.success("Número GSM excluído.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir o número GSM.");
    }
  }

  async function createRecharge(input: GsmRechargeFormInput) {
    if (!session || !activeSpaceId || !selectedRechargeGsmNumber) {
      return;
    }

    try {
      await apiFetch<GsmRecharge>(`/api/gsm-numbers/${selectedRechargeGsmNumber.id}/recharges`, {
        method: "POST",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify({
          rechargedOn: input.rechargedOn,
          amount: input.amount,
          note: input.note?.trim() || null,
        }),
      });
      await loadWorkspace();
      await loadRechargeHistory(selectedRechargeGsmNumber.id);
      closeRechargeModal();
      toast.success("Recarga informada.");
    } catch (exception) {
      reportError(exception, "Não foi possível informar a recarga.");
    }
  }

  async function updateRecharge(rechargeId: string, input: GsmRechargeFormInput) {
    if (!session || !activeSpaceId || !selectedRechargeGsmNumber) {
      return;
    }

    try {
      await apiFetch<GsmRecharge>(`/api/gsm-numbers/${selectedRechargeGsmNumber.id}/recharges/${rechargeId}`, {
        method: "PUT",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify({
          rechargedOn: input.rechargedOn,
          amount: input.amount,
          note: input.note?.trim() || null,
        }),
      });
      await loadWorkspace();
      await loadRechargeHistory(selectedRechargeGsmNumber.id);
      closeRechargeModal();
      toast.success("Recarga atualizada.");
    } catch (exception) {
      reportError(exception, "Não foi possível salvar a recarga.");
    }
  }

  async function deleteRecharge(recharge: GsmRecharge) {
    if (!session || !activeSpaceId || !selectedRechargeGsmNumber || !recharge.canDelete) {
      return;
    }

    try {
      await apiFetch<void>(`/api/gsm-numbers/${selectedRechargeGsmNumber.id}/recharges/${recharge.id}`, {
        method: "DELETE",
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      await loadWorkspace();
      await loadRechargeHistory(selectedRechargeGsmNumber.id);
      toast.success("Recarga excluída.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir a recarga.");
    }
  }

  return {
    session,
    activeSpaceId,
    activeSpace,
    members,
    gsmNumbers,
    gsmRecharges,
    sidebarCollapsed,
    theme,
    activeModal,
    editingSpace,
    editingGsmNumber,
    editingGsmRecharge,
    selectedRechargeGsmNumber,
    gsmRechargesLoading,
    gsmRechargesError,
    loading,
    error,
    subtitle: "Gestão compartilhada de linhas, chips e recargas do espaço",
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
    handleAuthenticated,
    handleSpaceChange,
    handleLogout,
    refreshSpaces,
    refreshWorkspace: loadWorkspace,
    createSpace,
    updateSpace,
    deleteSpace,
    openCreateSpace,
    openEditSpace,
    openShareSpace,
    closeCommonModal,
    shareSpace,
    updateProfile,
    openCreateGsmNumber,
    openEditGsmNumber,
    closeModuleModal,
    openRechargeHistory,
    closeRechargeHistory,
    openCreateRecharge,
    openEditRecharge,
    closeRechargeModal,
    refreshRechargeHistory,
    createGsmNumber,
    updateGsmNumber,
    deleteGsmNumber,
    createRecharge,
    updateRecharge,
    deleteRecharge,
  };
}

export type GsmDashboardController = ReturnType<typeof useGsmDashboard>;

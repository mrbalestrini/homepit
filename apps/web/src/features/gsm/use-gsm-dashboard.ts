"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { toast } from "sonner";
import {
  type AuthResponse,
  type GsmNumber,
  type GsmRecharge,
  type GsmNumberPlan,
  type GsmNumberStatus,
  type Household,
  type HouseholdMember,
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
import { sortGsmNumbersByUrgency } from "./gsm-dashboard.utils";

type GsmActiveModal = "household" | "share" | "gsm" | "recharge-history" | "recharge" | null;

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
  return value === "cozy" || value === "earthy" || value === "dark";
}

function applyDocumentTheme(theme: AppTheme) {
  document.documentElement.dataset.theme = theme;
}

export function useGsmDashboard() {
  const [session, setSession] = useState<AuthResponse | null>(null);
  const [activeHouseholdId, setActiveHouseholdId] = useState("");
  const [members, setMembers] = useState<HouseholdMember[]>([]);
  const [gsmNumbers, setGsmNumbers] = useState<GsmNumber[]>([]);
  const [gsmRecharges, setGsmRecharges] = useState<GsmRecharge[]>([]);
  const [sidebarCollapsed, setSidebarCollapsedState] = useState(false);
  const [theme, setThemeState] = useState<AppTheme>(defaultAppTheme);
  const [activeModal, setActiveModal] = useState<GsmActiveModal>(null);
  const [editingHousehold, setEditingHousehold] = useState<Household | null>(null);
  const [editingGsmNumber, setEditingGsmNumber] = useState<GsmNumber | null>(null);
  const [editingGsmRecharge, setEditingGsmRecharge] = useState<GsmRecharge | null>(null);
  const [selectedRechargeGsmNumber, setSelectedRechargeGsmNumber] = useState<GsmNumber | null>(null);
  const [gsmRechargesLoading, setGsmRechargesLoading] = useState(false);
  const [gsmRechargesError, setGsmRechargesError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const sessionUserIdRef = useRef<string | null>(null);
  const activeHouseholdIdRef = useRef("");

  const resetWorkspaceState = useCallback(() => {
    setMembers([]);
    setGsmNumbers([]);
    setGsmRecharges([]);
    setEditingHousehold(null);
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
      setLoading(Boolean(nextSession.households.length > 0));
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
      if (!token || !householdId || (session?.user.accountState ?? "Active") !== "Active") {
        return;
      }

      setLoading(true);
      setError(null);
      try {
        const [nextMembers, nextNumbers] = await Promise.all([
          apiFetch<HouseholdMember[]>("/api/households/members", { token, householdId }),
          apiFetch<GsmNumber[]>("/api/gsm-numbers", { token, householdId }),
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
    [activeHouseholdId, session?.accessToken, session?.user],
  );

  useEffect(() => {
    if (!session || !activeHouseholdId || !isAccountActive) {
      return;
    }

    const timer = window.setTimeout(() => {
      void loadWorkspace(session.accessToken, activeHouseholdId);
    }, 0);

    return () => window.clearTimeout(timer);
  }, [activeHouseholdId, isAccountActive, loadWorkspace, session]);

  const handleAuthenticated = useCallback((auth: AuthResponse) => {
    storeSession(auth);
    toast.success("Sessão iniciada com sucesso.");
  }, []);

  const handleHouseholdChange = useCallback((householdId: string) => {
    setLoading(true);
    setMembers([]);
    setGsmNumbers([]);
    setGsmRecharges([]);
    setEditingGsmNumber(null);
    setEditingGsmRecharge(null);
    setSelectedRechargeGsmNumber(null);
    setGsmRechargesError(null);
    setGsmRechargesLoading(false);
    setActiveHouseholdId(householdId);
    setError(null);
    setActiveModal(null);
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
      setMembers([]);
      setGsmNumbers([]);
      setGsmRecharges([]);
      setEditingGsmNumber(null);
      setEditingGsmRecharge(null);
      setSelectedRechargeGsmNumber(null);
      setGsmRechargesError(null);
      setGsmRechargesLoading(false);
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

  function closeCommonModal() {
    if (activeModal === "household" || activeModal === "share") {
      setActiveModal(null);
      setEditingHousehold(null);
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
    if (!session || !activeHouseholdId) {
      return;
    }

    setGsmRechargesLoading(true);
    setGsmRechargesError(null);
    try {
      const history = await apiFetch<GsmRecharge[]>(`/api/gsm-numbers/${gsmNumberId}/recharges`, {
        token: session.accessToken,
        householdId: activeHouseholdId,
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
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<GsmNumber>("/api/gsm-numbers", {
        method: "POST",
        token: session.accessToken,
        householdId: activeHouseholdId,
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
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<GsmNumber>(`/api/gsm-numbers/${gsmNumberId}`, {
        method: "PUT",
        token: session.accessToken,
        householdId: activeHouseholdId,
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
    if (!session || !activeHouseholdId || !gsmNumber.canDelete) {
      return;
    }

    try {
      await apiFetch<void>(`/api/gsm-numbers/${gsmNumber.id}`, {
        method: "DELETE",
        token: session.accessToken,
        householdId: activeHouseholdId,
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
    if (!session || !activeHouseholdId || !selectedRechargeGsmNumber) {
      return;
    }

    try {
      await apiFetch<GsmRecharge>(`/api/gsm-numbers/${selectedRechargeGsmNumber.id}/recharges`, {
        method: "POST",
        token: session.accessToken,
        householdId: activeHouseholdId,
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
    if (!session || !activeHouseholdId || !selectedRechargeGsmNumber) {
      return;
    }

    try {
      await apiFetch<GsmRecharge>(`/api/gsm-numbers/${selectedRechargeGsmNumber.id}/recharges/${rechargeId}`, {
        method: "PUT",
        token: session.accessToken,
        householdId: activeHouseholdId,
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
    if (!session || !activeHouseholdId || !selectedRechargeGsmNumber || !recharge.canDelete) {
      return;
    }

    try {
      await apiFetch<void>(`/api/gsm-numbers/${selectedRechargeGsmNumber.id}/recharges/${recharge.id}`, {
        method: "DELETE",
        token: session.accessToken,
        householdId: activeHouseholdId,
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
    activeHouseholdId,
    activeHousehold,
    members,
    gsmNumbers,
    gsmRecharges,
    sidebarCollapsed,
    theme,
    activeModal,
    editingHousehold,
    editingGsmNumber,
    editingGsmRecharge,
    selectedRechargeGsmNumber,
    gsmRechargesLoading,
    gsmRechargesError,
    loading,
    error,
    subtitle: "Gestão compartilhada de linhas, chips e recargas da casa",
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
    handleAuthenticated,
    handleHouseholdChange,
    handleLogout,
    refreshHouseholds,
    refreshWorkspace: loadWorkspace,
    createHousehold,
    updateHousehold,
    deleteHousehold,
    openCreateHousehold,
    openEditHousehold,
    openShareHousehold,
    closeCommonModal,
    shareHousehold,
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

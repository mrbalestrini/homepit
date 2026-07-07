"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { toast } from "sonner";
import type {
  Asset,
  AssetType,
  AssetValuation,
  AuthResponse,
  CreditCardAccount,
  CreditCardStatement,
  CreditCardTransaction,
  FinanceCategory,
  FinanceEntry,
  FinanceEntryType,
  FinancePeriodDetail,
  FinancePeriodListItem,
  FinanceRecurringTemplate,
  FinanceRecurrence,
  Household,
  HouseholdMember,
  Project,
  Universe,
} from "@/lib/api";
import {
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
import { getCurrentPeriodParts } from "./finance-dashboard.utils";

type WorkspaceTheme = AppTheme;

export type FinanceEntryFormInput = {
  year: number;
  month: number;
  title: string;
  notes?: string;
  amount: number;
  type: FinanceEntryType;
  verified: boolean;
  referenceDate: string;
  recurringTemplateId?: string | null;
  categoryId?: string | null;
  universeId?: string | null;
  projectId?: string | null;
};

export type FinanceRecurringTemplateFormInput = {
  title: string;
  notes?: string;
  type: FinanceEntryType;
  defaultAmount: number;
  recurrence: FinanceRecurrence;
  dayOfMonth?: number | null;
  monthOfYear?: number | null;
  isActive: boolean;
  categoryId?: string | null;
  universeId?: string | null;
  projectId?: string | null;
};

export type FinanceCategoryFormInput = {
  name: string;
};

export type AssetFormInput = {
  title: string;
  type: AssetType;
  currentValue?: number | null;
  remainingDebt?: number | null;
  isPaidOff: boolean;
  notes?: string;
  propertyDetails?: {
    registryNumber?: string;
    propertyInscription?: string;
    privateAreaSquareMeters?: number | null;
    debtCheckOn?: string | null;
  } | null;
  vehicleDetails?: {
    brand?: string;
    model?: string;
    yearModel?: string;
    renavam?: string;
  } | null;
};

export type AssetValuationFormInput = {
  referenceYear: number;
  label: string;
  amount: number;
  notes?: string;
};

export type CreditCardAccountFormInput = {
  name: string;
  brand?: string;
  lastFourDigits?: string;
  closingDay: number;
  dueDay: number;
  notes?: string;
  isActive: boolean;
};

export type CreditCardTransactionFormInput = {
  title: string;
  merchant?: string;
  amount: number;
  purchasedOn: string;
  notes?: string;
  categoryId?: string | null;
  universeId?: string | null;
  projectId?: string | null;
  externalSource?: string;
  externalReference?: string;
};

export type CreditCardStatementFormInput = {
  closingDate: string;
  dueDate: string;
  notes?: string;
  transactionIds: string[];
  externalSource?: string;
  externalReference?: string;
};

function isAppTheme(value: string | null): value is WorkspaceTheme {
  return value === "cozy" || value === "earthy" || value === "dark";
}

function applyDocumentTheme(theme: WorkspaceTheme) {
  document.documentElement.dataset.theme = theme;
}

export function useFinanceDashboard() {
  const currentPeriod = getCurrentPeriodParts();
  const [session, setSession] = useState<AuthResponse | null>(null);
  const [activeHouseholdId, setActiveHouseholdId] = useState("");
  const [members, setMembers] = useState<HouseholdMember[]>([]);
  const [universes, setUniverses] = useState<Universe[]>([]);
  const [projects, setProjects] = useState<Project[]>([]);
  const [categories, setCategories] = useState<FinanceCategory[]>([]);
  const [financePeriods, setFinancePeriods] = useState<FinancePeriodListItem[]>([]);
  const [activeYear, setActiveYear] = useState(currentPeriod.year);
  const [activeMonth, setActiveMonth] = useState(currentPeriod.month);
  const [periodDetail, setPeriodDetail] = useState<FinancePeriodDetail | null>(null);
  const [recurringTemplates, setRecurringTemplates] = useState<FinanceRecurringTemplate[]>([]);
  const [assets, setAssets] = useState<Asset[]>([]);
  const [assetValuations, setAssetValuations] = useState<Record<string, AssetValuation[]>>({});
  const [assetValuationsLoadingFor, setAssetValuationsLoadingFor] = useState<string | null>(null);
  const [creditCardAccounts, setCreditCardAccounts] = useState<CreditCardAccount[]>([]);
  const [selectedCreditCardId, setSelectedCreditCardIdState] = useState("");
  const [creditCardTransactions, setCreditCardTransactions] = useState<CreditCardTransaction[]>([]);
  const [creditCardStatements, setCreditCardStatements] = useState<CreditCardStatement[]>([]);
  const [cardDetailsLoading, setCardDetailsLoading] = useState(false);
  const [activeCommonModal, setActiveCommonModal] = useState<"household" | "share" | null>(null);
  const [editingHousehold, setEditingHousehold] = useState<Household | null>(null);
  const [sidebarCollapsed, setSidebarCollapsedState] = useState(false);
  const [theme, setThemeState] = useState<WorkspaceTheme>(defaultAppTheme);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const sessionUserIdRef = useRef<string | null>(null);
  const activeHouseholdIdRef = useRef("");

  const resetWorkspaceState = useCallback(() => {
    setMembers([]);
    setUniverses([]);
    setProjects([]);
    setCategories([]);
    setFinancePeriods([]);
    setPeriodDetail(null);
    setRecurringTemplates([]);
    setAssets([]);
    setAssetValuations({});
    setAssetValuationsLoadingFor(null);
    setCreditCardAccounts([]);
    setSelectedCreditCardIdState("");
    setCreditCardTransactions([]);
    setCreditCardStatements([]);
    setCardDetailsLoading(false);
    setActiveCommonModal(null);
    setEditingHousehold(null);
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

  const loadCardDetails = useCallback(
    async (cardId: string, token = session?.accessToken, householdId = activeHouseholdId) => {
      if (!cardId || !token || !householdId) {
        setCreditCardTransactions([]);
        setCreditCardStatements([]);
        return;
      }

      setCardDetailsLoading(true);
      try {
        const [transactions, statements] = await Promise.all([
          apiFetch<CreditCardTransaction[]>(`/api/finance/credit-cards/${cardId}/transactions`, { token, householdId }),
          apiFetch<CreditCardStatement[]>(`/api/finance/credit-cards/${cardId}/statements`, { token, householdId }),
        ]);
        setCreditCardTransactions(transactions);
        setCreditCardStatements(statements);
      } catch (exception) {
        setError(getErrorMessage(exception, "Falha ao carregar os detalhes do cartão."));
      } finally {
        setCardDetailsLoading(false);
      }
    },
    [activeHouseholdId, session?.accessToken],
  );

  const loadWorkspace = useCallback(
    async (
      token = session?.accessToken,
      householdId = activeHouseholdId,
      year = activeYear,
      month = activeMonth,
      preferredCardId = selectedCreditCardId,
    ) => {
      if (!token || !householdId) {
        return;
      }

      setLoading(true);
      setError(null);

      try {
        const [nextMembers, nextUniverses, nextProjects, nextCategories, nextPeriods, nextPeriodDetail, nextTemplates, nextAssets, nextCards] =
          await Promise.all([
            apiFetch<HouseholdMember[]>("/api/households/members", { token, householdId }),
            apiFetch<Universe[]>("/api/universes", { token, householdId }),
            apiFetch<Project[]>("/api/projects", { token, householdId }),
            apiFetch<FinanceCategory[]>("/api/finance/categories", { token, householdId }),
            apiFetch<FinancePeriodListItem[]>("/api/finance/periods", { token, householdId }),
            apiFetch<FinancePeriodDetail>(`/api/finance/periods/${year}/${month}`, { token, householdId }),
            apiFetch<FinanceRecurringTemplate[]>("/api/finance/recurring-templates", { token, householdId }),
            apiFetch<Asset[]>("/api/finance/assets", { token, householdId }),
            apiFetch<CreditCardAccount[]>("/api/finance/credit-cards", { token, householdId }),
          ]);

        setMembers(nextMembers);
        setUniverses(nextUniverses);
        setProjects(nextProjects);
        setCategories(nextCategories);
        setFinancePeriods(nextPeriods);
        setPeriodDetail(nextPeriodDetail);
        setRecurringTemplates(nextTemplates);
        setAssets(nextAssets);
        setCreditCardAccounts(nextCards);

        const nextSelectedCardId =
          nextCards.find((card) => card.id === preferredCardId)?.id ?? nextCards[0]?.id ?? "";
        setSelectedCreditCardIdState(nextSelectedCardId);

        if (nextSelectedCardId) {
          await loadCardDetails(nextSelectedCardId, token, householdId);
        } else {
          setCreditCardTransactions([]);
          setCreditCardStatements([]);
        }
      } catch (exception) {
        setError(getErrorMessage(exception, "Falha ao carregar o módulo financeiro."));
      } finally {
        setLoading(false);
      }
    },
    [activeHouseholdId, activeMonth, activeYear, loadCardDetails, selectedCreditCardId, session?.accessToken],
  );

  useEffect(() => {
    if (!session || !activeHouseholdId) {
      return;
    }

    const timer = window.setTimeout(() => {
      void loadWorkspace(session.accessToken, activeHouseholdId, activeYear, activeMonth, selectedCreditCardId);
    }, 0);

    return () => window.clearTimeout(timer);
  }, [activeHouseholdId, activeMonth, activeYear, loadWorkspace, selectedCreditCardId, session]);

  const setSelectedCreditCardId = useCallback(
    (cardId: string) => {
      setSelectedCreditCardIdState(cardId);
      if (cardId && session?.accessToken && activeHouseholdId) {
        void loadCardDetails(cardId, session.accessToken, activeHouseholdId);
      }
    },
    [activeHouseholdId, loadCardDetails, session?.accessToken],
  );

  const handleAuthenticated = useCallback((auth: AuthResponse) => {
    storeSession(auth);
    toast.success("Sessão iniciada com sucesso.");
  }, []);

  const handleHouseholdChange = useCallback((householdId: string) => {
    setLoading(true);
    setMembers([]);
    setUniverses([]);
    setProjects([]);
    setCategories([]);
    setFinancePeriods([]);
    setPeriodDetail(null);
    setRecurringTemplates([]);
    setAssets([]);
    setAssetValuations({});
    setCreditCardAccounts([]);
    setSelectedCreditCardIdState("");
    setCreditCardTransactions([]);
    setCreditCardStatements([]);
    setCardDetailsLoading(false);
    setActiveHouseholdId(householdId);
    setError(null);
  }, []);

  const handleLogout = useCallback(() => {
    clearSession();
    toast.success("Sessão encerrada.");
  }, []);

  const openCreateHousehold = useCallback(() => {
    setEditingHousehold(null);
    setActiveCommonModal("household");
  }, []);

  const openEditHousehold = useCallback(() => {
    if (!activeHousehold) {
      return;
    }

    setEditingHousehold(activeHousehold);
    setActiveCommonModal("household");
  }, [activeHousehold]);

  const openShareHousehold = useCallback(() => {
    setActiveCommonModal("share");
  }, []);

  const closeCommonModal = useCallback(() => {
    setActiveCommonModal(null);
    setEditingHousehold(null);
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
      const nextHouseholds = await apiFetch<Household[]>("/api/households", { token: session.accessToken });
      updateSessionHouseholds(nextHouseholds);
      toast.success("Casas atualizadas.");
    } catch (exception) {
      setError(getErrorMessage(exception, "Falha ao carregar casas."));
      toast.error(getErrorMessage(exception, "Falha ao carregar casas."));
    } finally {
      setLoading(false);
    }
  }, [session, updateSessionHouseholds]);

  const refreshWorkspace = useCallback(async () => {
    await loadWorkspace();
  }, [loadWorkspace]);

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
      resetWorkspaceState();
      updateSessionHouseholds(nextHouseholds);
      toast.success("Casa excluida.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir a casa.");
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
      toast.success("Pessoa adicionada a casa.");
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

  async function generatePeriod(mode: "missingOnly" | "duplicateAll") {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      const detail = await apiFetch<FinancePeriodDetail>(`/api/finance/periods/${activeYear}/${activeMonth}/generate`, {
        method: "POST",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify({ mode }),
      });
      setPeriodDetail(detail);
      const periods = await apiFetch<FinancePeriodListItem[]>("/api/finance/periods", {
        token: session.accessToken,
        householdId: activeHouseholdId,
      });
      setFinancePeriods(periods);
      toast.success(mode === "missingOnly" ? "Lançamentos inseridos com itens faltantes." : "Recorrências duplicadas no mês.");
    } catch (exception) {
      reportError(exception, "Não foi possível inserir os lançamentos do mês.");
    }
  }

  async function createEntry(input: FinanceEntryFormInput) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<FinanceEntry>("/api/finance/entries", {
        method: "POST",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Lançamento criado.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar o lançamento.");
    }
  }

  async function updateEntry(entryId: string, input: FinanceEntryFormInput) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<FinanceEntry>(`/api/finance/entries/${entryId}`, {
        method: "PUT",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Lançamento atualizado.");
    } catch (exception) {
      reportError(exception, "Não foi possível atualizar o lançamento.");
    }
  }

  async function toggleEntryVerified(entry: FinanceEntry) {
    if (!entry.canEdit) {
      return;
    }

    await updateEntry(entry.id, {
      year: entry.year,
      month: entry.month,
      title: entry.title,
      notes: entry.notes ?? "",
      amount: entry.amount,
      type: entry.type,
      verified: !entry.verified,
      referenceDate: entry.referenceDate,
      recurringTemplateId: entry.recurringTemplateId ?? null,
      categoryId: entry.categoryId ?? null,
      universeId: entry.universeId ?? null,
      projectId: entry.projectId ?? null,
    });
  }

  async function createCategory(input: FinanceCategoryFormInput) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<FinanceCategory>("/api/finance/categories", {
        method: "POST",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Categoria criada.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar a categoria.");
    }
  }

  async function updateCategory(categoryId: string, input: FinanceCategoryFormInput) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<FinanceCategory>(`/api/finance/categories/${categoryId}`, {
        method: "PUT",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Categoria atualizada.");
    } catch (exception) {
      reportError(exception, "Não foi possível atualizar a categoria.");
    }
  }

  async function deleteCategory(categoryId: string) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<void>(`/api/finance/categories/${categoryId}`, {
        method: "DELETE",
        token: session.accessToken,
        householdId: activeHouseholdId,
      });
      await refreshWorkspace();
      toast.success("Categoria excluída.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir a categoria.");
    }
  }

  async function deleteEntry(entryId: string) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<void>(`/api/finance/entries/${entryId}`, {
        method: "DELETE",
        token: session.accessToken,
        householdId: activeHouseholdId,
      });
      await refreshWorkspace();
      toast.success("Lançamento excluído.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir o lançamento.");
    }
  }

  async function createRecurringTemplate(input: FinanceRecurringTemplateFormInput) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<FinanceRecurringTemplate>("/api/finance/recurring-templates", {
        method: "POST",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Recorrência criada.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar a recorrência.");
    }
  }

  async function updateRecurringTemplate(templateId: string, input: FinanceRecurringTemplateFormInput) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<FinanceRecurringTemplate>(`/api/finance/recurring-templates/${templateId}`, {
        method: "PUT",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Recorrência atualizada.");
    } catch (exception) {
      reportError(exception, "Não foi possível atualizar a recorrência.");
    }
  }

  async function deleteRecurringTemplate(templateId: string) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<void>(`/api/finance/recurring-templates/${templateId}`, {
        method: "DELETE",
        token: session.accessToken,
        householdId: activeHouseholdId,
      });
      await refreshWorkspace();
      toast.success("Recorrência excluída.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir a recorrência.");
    }
  }

  async function createAsset(input: AssetFormInput) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<Asset>("/api/finance/assets", {
        method: "POST",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Bem criado.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar o bem.");
    }
  }

  async function updateAsset(assetId: string, input: AssetFormInput) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<Asset>(`/api/finance/assets/${assetId}`, {
        method: "PUT",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Bem atualizado.");
    } catch (exception) {
      reportError(exception, "Não foi possível atualizar o bem.");
    }
  }

  async function deleteAsset(assetId: string) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<void>(`/api/finance/assets/${assetId}`, {
        method: "DELETE",
        token: session.accessToken,
        householdId: activeHouseholdId,
      });
      await refreshWorkspace();
      toast.success("Bem excluido.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir o bem.");
    }
  }

  async function loadAssetValuations(assetId: string) {
    if (!session || !activeHouseholdId) {
      return;
    }

    setAssetValuationsLoadingFor(assetId);
    try {
      const valuations = await apiFetch<AssetValuation[]>(`/api/finance/assets/${assetId}/valuations`, {
        token: session.accessToken,
        householdId: activeHouseholdId,
      });
      setAssetValuations((current) => ({
        ...current,
        [assetId]: valuations,
      }));
    } catch (exception) {
      reportError(exception, "Não foi possível carregar as referências anuais.");
    } finally {
      setAssetValuationsLoadingFor(null);
    }
  }

  async function createAssetValuation(assetId: string, input: AssetValuationFormInput) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<AssetValuation>(`/api/finance/assets/${assetId}/valuations`, {
        method: "POST",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify(input),
      });
      await loadAssetValuations(assetId);
      toast.success("Referência anual criada.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar a referência anual.");
    }
  }

  async function updateAssetValuation(assetId: string, valuationId: string, input: AssetValuationFormInput) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<AssetValuation>(`/api/finance/assets/${assetId}/valuations/${valuationId}`, {
        method: "PUT",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify(input),
      });
      await loadAssetValuations(assetId);
      toast.success("Referência anual atualizada.");
    } catch (exception) {
      reportError(exception, "Não foi possível atualizar a referência anual.");
    }
  }

  async function deleteAssetValuation(assetId: string, valuationId: string) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<void>(`/api/finance/assets/${assetId}/valuations/${valuationId}`, {
        method: "DELETE",
        token: session.accessToken,
        householdId: activeHouseholdId,
      });
      await loadAssetValuations(assetId);
      toast.success("Referência anual excluída.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir a referência anual.");
    }
  }

  async function createCreditCardAccount(input: CreditCardAccountFormInput) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<CreditCardAccount>("/api/finance/credit-cards", {
        method: "POST",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Cartão criado.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar o cartão.");
    }
  }

  async function updateCreditCardAccount(cardId: string, input: CreditCardAccountFormInput) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<CreditCardAccount>(`/api/finance/credit-cards/${cardId}`, {
        method: "PUT",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Cartão atualizado.");
    } catch (exception) {
      reportError(exception, "Não foi possível atualizar o cartão.");
    }
  }

  async function deleteCreditCardAccount(cardId: string) {
    if (!session || !activeHouseholdId) {
      return;
    }

    try {
      await apiFetch<void>(`/api/finance/credit-cards/${cardId}`, {
        method: "DELETE",
        token: session.accessToken,
        householdId: activeHouseholdId,
      });
      await refreshWorkspace();
      toast.success("Cartão excluído.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir o cartão.");
    }
  }

  async function createCreditCardTransaction(input: CreditCardTransactionFormInput) {
    if (!session || !activeHouseholdId || !selectedCreditCardId) {
      return;
    }

    try {
      await apiFetch<CreditCardTransaction>(`/api/finance/credit-cards/${selectedCreditCardId}/transactions`, {
        method: "POST",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Compra no cartão criada.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar a compra no cartão.");
    }
  }

  async function updateCreditCardTransaction(transactionId: string, input: CreditCardTransactionFormInput) {
    if (!session || !activeHouseholdId || !selectedCreditCardId) {
      return;
    }

    try {
      await apiFetch<CreditCardTransaction>(
        `/api/finance/credit-cards/${selectedCreditCardId}/transactions/${transactionId}`,
        {
          method: "PUT",
          token: session.accessToken,
          householdId: activeHouseholdId,
          body: JSON.stringify(input),
        },
      );
      await refreshWorkspace();
      toast.success("Compra no cartão atualizada.");
    } catch (exception) {
      reportError(exception, "Não foi possível atualizar a compra no cartão.");
    }
  }

  async function deleteCreditCardTransaction(transactionId: string) {
    if (!session || !activeHouseholdId || !selectedCreditCardId) {
      return;
    }

    try {
      await apiFetch<void>(`/api/finance/credit-cards/${selectedCreditCardId}/transactions/${transactionId}`, {
        method: "DELETE",
        token: session.accessToken,
        householdId: activeHouseholdId,
      });
      await refreshWorkspace();
      toast.success("Compra no cartão excluída.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir a compra no cartão.");
    }
  }

  async function createCreditCardStatement(input: CreditCardStatementFormInput) {
    if (!session || !activeHouseholdId || !selectedCreditCardId) {
      return;
    }

    try {
      await apiFetch<CreditCardStatement>(`/api/finance/credit-cards/${selectedCreditCardId}/statements`, {
        method: "POST",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Fatura criada.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar a fatura.");
    }
  }

  async function updateCreditCardStatement(statementId: string, input: CreditCardStatementFormInput) {
    if (!session || !activeHouseholdId || !selectedCreditCardId) {
      return;
    }

    try {
      await apiFetch<CreditCardStatement>(`/api/finance/credit-cards/${selectedCreditCardId}/statements/${statementId}`, {
        method: "PUT",
        token: session.accessToken,
        householdId: activeHouseholdId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Fatura atualizada.");
    } catch (exception) {
      reportError(exception, "Não foi possível atualizar a fatura.");
    }
  }

  async function deleteCreditCardStatement(statementId: string) {
    if (!session || !activeHouseholdId || !selectedCreditCardId) {
      return;
    }

    try {
      await apiFetch<void>(`/api/finance/credit-cards/${selectedCreditCardId}/statements/${statementId}`, {
        method: "DELETE",
        token: session.accessToken,
        householdId: activeHouseholdId,
      });
      await refreshWorkspace();
      toast.success("Fatura excluída.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir a fatura.");
    }
  }

  return {
    session,
    activeHouseholdId,
    activeHousehold,
    members,
    universes,
    projects,
    categories,
    financePeriods,
    activeYear,
    activeMonth,
    periodDetail,
    recurringTemplates,
    assets,
    assetValuations,
    assetValuationsLoadingFor,
    creditCardAccounts,
    selectedCreditCardId,
    creditCardTransactions,
    creditCardStatements,
    cardDetailsLoading,
    editingHousehold,
    isHouseholdDialogOpen: activeCommonModal === "household",
    isShareDialogOpen: activeCommonModal === "share",
    sidebarCollapsed,
    theme,
    loading,
    error,
    subtitle: "Fluxo mensal, recorrências, cartões e patrimônio da casa",
    canShareHousehold,
    canManageHousehold,
    setError,
    setSidebarCollapsed: (collapsed: boolean) => {
      setSidebarCollapsedState(collapsed);
      window.localStorage.setItem(uiStorageKeys.sidebarCollapsed, String(collapsed));
    },
    setTheme: (nextTheme: WorkspaceTheme) => {
      setThemeState(nextTheme);
      applyDocumentTheme(nextTheme);
      window.localStorage.setItem(uiStorageKeys.theme, nextTheme);
    },
    setActivePeriod: (year: number, month: number) => {
      setActiveYear(year);
      setActiveMonth(month);
    },
    setSelectedCreditCardId,
    handleAuthenticated,
    handleHouseholdChange,
    handleLogout,
    refreshHouseholds,
    refreshWorkspace,
    openCreateHousehold,
    openEditHousehold,
    openShareHousehold,
    closeCommonModal,
    createHousehold,
    updateHousehold,
    deleteHousehold,
    shareHousehold,
    updateProfile,
    generatePeriod,
    createCategory,
    updateCategory,
    deleteCategory,
    createEntry,
    updateEntry,
    toggleEntryVerified,
    deleteEntry,
    createRecurringTemplate,
    updateRecurringTemplate,
    deleteRecurringTemplate,
    createAsset,
    updateAsset,
    deleteAsset,
    loadAssetValuations,
    createAssetValuation,
    updateAssetValuation,
    deleteAssetValuation,
    createCreditCardAccount,
    updateCreditCardAccount,
    deleteCreditCardAccount,
    createCreditCardTransaction,
    updateCreditCardTransaction,
    deleteCreditCardTransaction,
    createCreditCardStatement,
    updateCreditCardStatement,
    deleteCreditCardStatement,
  };
}

export type FinanceDashboardController = ReturnType<typeof useFinanceDashboard>;

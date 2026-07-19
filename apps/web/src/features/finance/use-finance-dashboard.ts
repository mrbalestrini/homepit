"use client";

import { startTransition, useCallback, useEffect, useMemo, useRef, useState } from "react";
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
  Space,
  SpaceMember,
  ImportCreditCardTransactionItem,
  ImportCreditCardTransactionsResponse,
  Project,
  Core,
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
  clearStoredActiveSpaceId,
  readStoredActiveSpaceId,
  resolveActiveSpaceSelection,
  storeActiveSpaceId,
} from "@/lib/space-selection";
import { defaultAppTheme, uiStorageKeys } from "@/features/projects/project-dashboard.constants";
import type { AppTheme } from "@/features/projects/project-dashboard.types";
import { getErrorMessage } from "@/features/projects/project-dashboard.utils";
import { getCurrentPeriodParts, summarizeAnalyticalExpenses } from "./finance-dashboard.utils";

type WorkspaceTheme = AppTheme;
type FinanceSyncSection = "cash" | "categories" | "recurringTemplates" | "cardTransactions" | "cardStatements" | "assetValuations";
type FinanceMutationOptions = {
  silentSuccess?: boolean;
};

const initialSyncCounts: Record<FinanceSyncSection, number> = {
  cash: 0,
  categories: 0,
  recurringTemplates: 0,
  cardTransactions: 0,
  cardStatements: 0,
  assetValuations: 0,
};

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
  coreId?: string | null;
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
  coreId?: string | null;
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
  coreId?: string | null;
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

export type ImportedCreditCardTransactionDraftError = {
  field:
    | "title"
    | "amount"
    | "purchasedOn"
    | "categoryName"
    | "coreName"
    | "projectName"
    | "externalSource"
    | "externalReference"
    | "importedAt"
    | "json";
  message: string;
};

export type ImportedCreditCardTransactionDraft = {
  localId: string;
  title: string;
  merchant: string;
  amount: string;
  purchasedOn: string;
  notes: string;
  categoryName: string;
  coreName: string;
  projectName: string;
  externalSource: string;
  externalReference: string;
  importedAt: string;
  errors: ImportedCreditCardTransactionDraftError[];
};

export type CreditCardTransactionImportSummary = {
  totalCount: number;
  validCount: number;
  invalidCount: number;
  totalAmount: number;
  newCategoryCount: number;
};

function isAppTheme(value: string | null): value is WorkspaceTheme {
  return value === "light" || value === "system" || value === "dark";
}

function applyDocumentTheme(theme: WorkspaceTheme) {
  const resolved = theme === "system"
    ? window.matchMedia?.("(prefers-color-scheme: dark)").matches ? "dark" : "light"
    : theme;
  document.documentElement.dataset.themePreference = theme;
  document.documentElement.dataset.theme = resolved;
}

function sortFinancePeriods(periods: FinancePeriodListItem[]) {
  return [...periods].sort((left, right) => {
    if (left.year !== right.year) {
      return right.year - left.year;
    }

    return right.month - left.month;
  });
}

function sortFinanceEntries(entries: FinanceEntry[]) {
  return [...entries].sort((left, right) => {
    const dateComparison = left.referenceDate.localeCompare(right.referenceDate);
    if (dateComparison !== 0) {
      return dateComparison;
    }

    const typeComparison = left.type.localeCompare(right.type);
    if (typeComparison !== 0) {
      return typeComparison;
    }

    return left.title.localeCompare(right.title);
  });
}

function sortRecurringTemplates(templates: FinanceRecurringTemplate[]) {
  return [...templates].sort((left, right) => {
    const recurrenceWeight = (value: FinanceRecurringTemplate["recurrence"]) => (value === "Monthly" ? 0 : 1);
    const recurrenceComparison = recurrenceWeight(left.recurrence) - recurrenceWeight(right.recurrence);
    if (recurrenceComparison !== 0) {
      return recurrenceComparison;
    }

    return left.title.localeCompare(right.title);
  });
}

function sortCategories(categories: FinanceCategory[]) {
  return [...categories].sort((left, right) => {
    if (left.isDefault !== right.isDefault) {
      return left.isDefault ? -1 : 1;
    }

    if (left.isDefault && right.isDefault) {
      return left.sortOrder - right.sortOrder;
    }

    return left.name.localeCompare(right.name);
  });
}

function sortAssetValuations(valuations: AssetValuation[]) {
  return [...valuations].sort((left, right) => {
    if (left.referenceYear !== right.referenceYear) {
      return right.referenceYear - left.referenceYear;
    }

    return right.id.localeCompare(left.id);
  });
}

function sortCreditCardAccounts(accounts: CreditCardAccount[]) {
  return [...accounts].sort((left, right) => {
    if (left.isActive !== right.isActive) {
      return left.isActive ? -1 : 1;
    }

    return left.name.localeCompare(right.name);
  });
}

function sortCreditCardTransactions(transactions: CreditCardTransaction[]) {
  return [...transactions].sort((left, right) => {
    const dateComparison = right.purchasedOn.localeCompare(left.purchasedOn);
    if (dateComparison !== 0) {
      return dateComparison;
    }

    return right.id.localeCompare(left.id);
  });
}

function sortCreditCardStatements(statements: CreditCardStatement[]) {
  return [...statements].sort((left, right) => {
    const dateComparison = right.dueDate.localeCompare(left.dueDate);
    if (dateComparison !== 0) {
      return dateComparison;
    }

    return right.id.localeCompare(left.id);
  });
}

function buildFinancePeriodSummary(entries: FinanceEntry[], cardTransactions: CreditCardTransaction[]) {
  const totalIncome = entries
    .filter((entry) => entry.type === "Entrada")
    .reduce((total, entry) => total + entry.amount, 0);
  const totalExpense = entries
    .filter((entry) => entry.type === "Saida")
    .reduce((total, entry) => total + entry.amount, 0);

  return {
    totalIncome,
    totalExpense,
    cashBalance: totalIncome - totalExpense,
    analyticalExpenseTotal: summarizeAnalyticalExpenses(entries, cardTransactions),
    verifiedEntries: entries.filter((entry) => entry.verified).length,
    pendingVerificationEntries: entries.filter((entry) => !entry.verified).length,
    cardPurchaseCount: cardTransactions.length,
  };
}

function normalizePeriodDetail(detail: FinancePeriodDetail) {
  const entries = sortFinanceEntries(detail.entries);
  const cardTransactions = sortCreditCardTransactions(detail.cardTransactions);
  const statements = sortCreditCardStatements(detail.statements);

  return {
    ...detail,
    entries,
    cardTransactions,
    statements,
    summary: buildFinancePeriodSummary(entries, cardTransactions),
  };
}

function upsertFinancePeriodFromDetail(periods: FinancePeriodListItem[], detail: FinancePeriodDetail) {
  if (!detail.id) {
    return periods;
  }

  const nextItem: FinancePeriodListItem = {
    id: detail.id,
    year: detail.year,
    month: detail.month,
    totalIncome: detail.summary.totalIncome,
    totalExpense: detail.summary.totalExpense,
    cashBalance: detail.summary.cashBalance,
    entryCount: detail.entries.length,
  };

  const hasExisting = periods.some((period) => period.id === detail.id || (period.year === detail.year && period.month === detail.month));
  return sortFinancePeriods(
    hasExisting
      ? periods.map((period) =>
          period.id === detail.id || (period.year === detail.year && period.month === detail.month) ? nextItem : period,
        )
      : [...periods, nextItem],
  );
}

function summarizeOpenCardTransactions(transactions: CreditCardTransaction[]) {
  const openTransactions = transactions.filter((transaction) => !transaction.creditCardStatementId);
  return {
    openTransactionCount: openTransactions.length,
    openTransactionTotal: openTransactions.reduce((total, transaction) => total + transaction.amount, 0),
  };
}

export function useFinanceDashboard() {
  const currentPeriod = getCurrentPeriodParts();
  const [session, setSession] = useState<AuthResponse | null>(null);
  const [activeSpaceId, setActiveSpaceId] = useState("");
  const [members, setMembers] = useState<SpaceMember[]>([]);
  const [cores, setCores] = useState<Core[]>([]);
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
  const [activeCommonModal, setActiveCommonModal] = useState<"space" | "share" | null>(null);
  const [editingSpace, setEditingSpace] = useState<Space | null>(null);
  const [sidebarCollapsed, setSidebarCollapsedState] = useState(false);
  const [theme, setThemeState] = useState<WorkspaceTheme>(defaultAppTheme);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [syncCounts, setSyncCounts] = useState(initialSyncCounts);
  const sessionUserIdRef = useRef<string | null>(null);
  const activeSpaceIdRef = useRef("");
  const selectedCreditCardIdRef = useRef("");

  const resetWorkspaceState = useCallback(() => {
    setMembers([]);
    setCores([]);
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
    setEditingSpace(null);
    setLoading(false);
    setError(null);
    setSyncCounts(initialSyncCounts);
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
    selectedCreditCardIdRef.current = selectedCreditCardId;
  }, [selectedCreditCardId]);

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

  const reportReconcileError = useCallback((exception: unknown, fallback: string) => {
    const message = getErrorMessage(exception, fallback);
    setError(message);
    toast.error(message);
  }, []);

  const beginSync = useCallback((...sections: FinanceSyncSection[]) => {
    setSyncCounts((current) => {
      const next = { ...current };
      for (const section of sections) {
        next[section] += 1;
      }

      return next;
    });
  }, []);

  const endSync = useCallback((...sections: FinanceSyncSection[]) => {
    setSyncCounts((current) => {
      const next = { ...current };
      for (const section of sections) {
        next[section] = Math.max(0, next[section] - 1);
      }

      return next;
    });
  }, []);

  const syncingSections = useMemo(
    () => ({
      cash: syncCounts.cash > 0,
      categories: syncCounts.categories > 0,
      recurringTemplates: syncCounts.recurringTemplates > 0,
      cardTransactions: syncCounts.cardTransactions > 0,
      cardStatements: syncCounts.cardStatements > 0,
      assetValuations: syncCounts.assetValuations > 0,
    }),
    [syncCounts],
  );

  const syncPeriodListWithDetail = useCallback((detail: FinancePeriodDetail) => {
    setFinancePeriods((current) => upsertFinancePeriodFromDetail(current, detail));
  }, []);

  const loadCardDetails = useCallback(
    async (cardId: string, token = session?.accessToken, spaceId = activeSpaceId) => {
      if (!cardId || !token || !spaceId || (session?.user.accountState ?? "Active") !== "Active") {
        setCreditCardTransactions([]);
        setCreditCardStatements([]);
        return;
      }

      setCardDetailsLoading(true);
      try {
        const [transactions, statements] = await Promise.all([
          apiFetch<CreditCardTransaction[]>(`/api/finance/credit-cards/${cardId}/transactions`, { token, spaceId }),
          apiFetch<CreditCardStatement[]>(`/api/finance/credit-cards/${cardId}/statements`, { token, spaceId }),
        ]);
        startTransition(() => {
          setCreditCardTransactions(sortCreditCardTransactions(transactions));
          setCreditCardStatements(sortCreditCardStatements(statements));
        });
      } catch (exception) {
        setError(getErrorMessage(exception, "Falha ao carregar os detalhes do cartão."));
      } finally {
        setCardDetailsLoading(false);
      }
    },
    [activeSpaceId, session?.accessToken, session?.user],
  );

  const loadWorkspace = useCallback(
    async (
      token = session?.accessToken,
      spaceId = activeSpaceId,
      year = activeYear,
      month = activeMonth,
      preferredCardId = selectedCreditCardIdRef.current,
    ) => {
      if (!token || !spaceId || (session?.user.accountState ?? "Active") !== "Active") {
        return;
      }

      setLoading(true);
      setError(null);

      try {
        const [nextMembers, nextCores, nextProjects, nextCategories, nextPeriods, nextPeriodDetail, nextTemplates, nextAssets, nextCards] =
          await Promise.all([
            apiFetch<SpaceMember[]>("/api/spaces/members", { token, spaceId }),
            apiFetch<Core[]>("/api/cores", { token, spaceId }),
            apiFetch<Project[]>("/api/projects", { token, spaceId }),
            apiFetch<FinanceCategory[]>("/api/finance/categories", { token, spaceId }),
            apiFetch<FinancePeriodListItem[]>("/api/finance/periods", { token, spaceId }),
            apiFetch<FinancePeriodDetail>(`/api/finance/periods/${year}/${month}`, { token, spaceId }),
            apiFetch<FinanceRecurringTemplate[]>("/api/finance/recurring-templates", { token, spaceId }),
            apiFetch<Asset[]>("/api/finance/assets", { token, spaceId }),
            apiFetch<CreditCardAccount[]>("/api/finance/credit-cards", { token, spaceId }),
          ]);

        startTransition(() => {
          setMembers(nextMembers);
          setCores(nextCores);
          setProjects(nextProjects);
          setCategories(sortCategories(nextCategories));
          setFinancePeriods(sortFinancePeriods(nextPeriods));
          setPeriodDetail(normalizePeriodDetail(nextPeriodDetail));
          setRecurringTemplates(sortRecurringTemplates(nextTemplates));
          setAssets(nextAssets);
          setCreditCardAccounts(sortCreditCardAccounts(nextCards));
        });

        const nextSelectedCardId =
          nextCards.find((card) => card.id === preferredCardId)?.id ?? nextCards[0]?.id ?? "";
        startTransition(() => {
          setSelectedCreditCardIdState(nextSelectedCardId);
        });

        if (nextSelectedCardId) {
          await loadCardDetails(nextSelectedCardId, token, spaceId);
        } else {
          startTransition(() => {
            setCreditCardTransactions([]);
            setCreditCardStatements([]);
          });
        }
      } catch (exception) {
        setError(getErrorMessage(exception, "Falha ao carregar o módulo financeiro."));
      } finally {
        setLoading(false);
      }
    },
    [activeSpaceId, activeMonth, activeYear, loadCardDetails, session?.accessToken, session?.user],
  );

  useEffect(() => {
    if (!session || !activeSpaceId || !isAccountActive) {
      return;
    }

    const timer = window.setTimeout(() => {
      void loadWorkspace(session.accessToken, activeSpaceId, activeYear, activeMonth);
    }, 0);

    return () => window.clearTimeout(timer);
  }, [activeSpaceId, activeMonth, activeYear, isAccountActive, loadWorkspace, session]);

  const setSelectedCreditCardId = useCallback(
    (cardId: string) => {
      setSelectedCreditCardIdState(cardId);
      if (cardId && session?.accessToken && activeSpaceId) {
        void loadCardDetails(cardId, session.accessToken, activeSpaceId);
      }
    },
    [activeSpaceId, loadCardDetails, session],
  );

  const reconcileActivePeriod = useCallback(
    async (token = session?.accessToken, spaceId = activeSpaceId, year = activeYear, month = activeMonth) => {
      if (!token || !spaceId) {
        return;
      }

      const [nextPeriods, nextDetail] = await Promise.all([
        apiFetch<FinancePeriodListItem[]>("/api/finance/periods", { token, spaceId }),
        apiFetch<FinancePeriodDetail>(`/api/finance/periods/${year}/${month}`, { token, spaceId }),
      ]);

      startTransition(() => {
        setFinancePeriods(sortFinancePeriods(nextPeriods));
        setPeriodDetail(normalizePeriodDetail(nextDetail));
      });
    },
    [activeSpaceId, activeMonth, activeYear, session?.accessToken],
  );

  const reconcileSelectedCard = useCallback(
    async (preferredCardId = selectedCreditCardIdRef.current, token = session?.accessToken, spaceId = activeSpaceId) => {
      if (!token || !spaceId) {
        return;
      }

      const nextCards = sortCreditCardAccounts(
        await apiFetch<CreditCardAccount[]>("/api/finance/credit-cards", {
          token,
          spaceId,
        }),
      );
      const nextSelectedCardId = nextCards.find((card) => card.id === preferredCardId)?.id ?? nextCards[0]?.id ?? "";

      if (!nextSelectedCardId) {
        startTransition(() => {
          setCreditCardAccounts(nextCards);
          setSelectedCreditCardIdState("");
          setCreditCardTransactions([]);
          setCreditCardStatements([]);
        });
        return;
      }

      const [nextTransactions, nextStatements] = await Promise.all([
        apiFetch<CreditCardTransaction[]>(`/api/finance/credit-cards/${nextSelectedCardId}/transactions`, { token, spaceId }),
        apiFetch<CreditCardStatement[]>(`/api/finance/credit-cards/${nextSelectedCardId}/statements`, { token, spaceId }),
      ]);

      startTransition(() => {
        setCreditCardAccounts(nextCards);
        setSelectedCreditCardIdState(nextSelectedCardId);
        setCreditCardTransactions(sortCreditCardTransactions(nextTransactions));
        setCreditCardStatements(sortCreditCardStatements(nextStatements));
      });
    },
    [activeSpaceId, session?.accessToken],
  );

  const reconcileCardFinanceSections = useCallback(
    async (...sections: FinanceSyncSection[]) => {
      try {
        await Promise.all([reconcileActivePeriod(), reconcileSelectedCard()]);
      } catch (exception) {
        reportReconcileError(exception, "Os dados foram salvos, mas não foi possível sincronizar a seção financeira.");
      } finally {
        endSync(...sections);
      }
    },
    [endSync, reconcileActivePeriod, reconcileSelectedCard, reportReconcileError],
  );

  const handleAuthenticated = useCallback((auth: AuthResponse) => {
    storeSession(auth);
    toast.success("Sessão iniciada com sucesso.");
  }, []);

  const handleSpaceChange = useCallback((spaceId: string) => {
    setLoading(true);
    setMembers([]);
    setCores([]);
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
    setActiveSpaceId(spaceId);
    setError(null);
  }, []);

  const handleLogout = useCallback(() => {
    clearSession();
    toast.success("Sessão encerrada.");
  }, []);

  const openCreateSpace = useCallback(() => {
    setEditingSpace(null);
    setActiveCommonModal("space");
  }, []);

  const openEditSpace = useCallback(() => {
    if (!activeSpace) {
      return;
    }

    setEditingSpace(activeSpace);
    setActiveCommonModal("space");
  }, [activeSpace]);

  const openShareSpace = useCallback(() => {
    setActiveCommonModal("share");
  }, []);

  const closeCommonModal = useCallback(() => {
    setActiveCommonModal(null);
    setEditingSpace(null);
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
      const nextSpaces = await apiFetch<Space[]>("/api/spaces", { token: session.accessToken });
      updateSessionSpaces(nextSpaces);
      toast.success("Espaços atualizados.");
    } catch (exception) {
      setError(getErrorMessage(exception, "Falha ao carregar espaços."));
      toast.error(getErrorMessage(exception, "Falha ao carregar espaços."));
    } finally {
      setLoading(false);
    }
  }, [session, updateSessionSpaces]);

  const refreshWorkspace = useCallback(async () => {
    if (!isAccountActive) {
      return;
    }

    await loadWorkspace();
  }, [isAccountActive, loadWorkspace]);

  const getCategoryName = useCallback(
    (categoryId?: string | null) => categories.find((category) => category.id === categoryId)?.name ?? null,
    [categories],
  );

  const resolveClassification = useCallback(
    (coreId?: string | null, projectId?: string | null) => {
      const project = projectId ? projects.find((item) => item.id === projectId) ?? null : null;
      const resolvedCoreId = project?.coreId ?? coreId ?? null;
      const core = resolvedCoreId ? cores.find((item) => item.id === resolvedCoreId) ?? null : null;

      return {
        coreId: resolvedCoreId,
        coreName: project?.coreName ?? core?.name ?? null,
        projectId: project?.id ?? projectId ?? null,
        projectName: project?.name ?? null,
      };
    },
    [projects, cores],
  );

  function notifyMutationSuccess(message: string, options?: FinanceMutationOptions) {
    if (!options?.silentSuccess) {
      toast.success(message);
    }
  }

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
      resetWorkspaceState();
      updateSessionSpaces(nextSpaces);
      toast.success("Espaço excluida.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir o espaço.");
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
      toast.success("Pessoa adicionada o espaço.");
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

  async function generatePeriod(mode: "missingOnly" | "duplicateAll") {
    if (!session || !activeSpaceId) {
      return;
    }

    beginSync("cash");
    try {
      const detail = normalizePeriodDetail(
        await apiFetch<FinancePeriodDetail>(`/api/finance/periods/${activeYear}/${activeMonth}/generate`, {
          method: "POST",
          token: session.accessToken,
          spaceId: activeSpaceId,
          body: JSON.stringify({ mode }),
        }),
      );
      startTransition(() => {
        setPeriodDetail(detail);
        syncPeriodListWithDetail(detail);
      });
      void (async () => {
        try {
          const periods = await apiFetch<FinancePeriodListItem[]>("/api/finance/periods", {
            token: session.accessToken,
            spaceId: activeSpaceId,
          });
          startTransition(() => {
            setFinancePeriods(sortFinancePeriods(periods));
          });
        } catch (exception) {
          reportReconcileError(exception, "Os lançamentos foram inseridos, mas não foi possível sincronizar o resumo do mês.");
        } finally {
          endSync("cash");
        }
      })();
      toast.success(mode === "missingOnly" ? "Lançamentos inseridos com itens faltantes." : "Recorrências duplicadas no mês.");
    } catch (exception) {
      endSync("cash");
      reportError(exception, "Não foi possível inserir os lançamentos do mês.");
    }
  }

  async function createEntry(input: FinanceEntryFormInput) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      await apiFetch<FinanceEntry>("/api/finance/entries", {
        method: "POST",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Lançamento criado.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar o lançamento.");
    }
  }

  async function updateEntry(entryId: string, input: FinanceEntryFormInput, options?: FinanceMutationOptions) {
    if (!session || !activeSpaceId) {
      return;
    }

    const previousPeriodDetail = periodDetail;
    const previousPeriods = financePeriods;
    const currentEntry = previousPeriodDetail?.entries.find((entry) => entry.id === entryId) ?? null;
    const affectsActivePeriod = Boolean(
      previousPeriodDetail &&
        currentEntry &&
        previousPeriodDetail.year === input.year &&
        previousPeriodDetail.month === input.month &&
        currentEntry.year === previousPeriodDetail.year &&
        currentEntry.month === previousPeriodDetail.month,
    );
    const classification = resolveClassification(input.coreId, input.projectId);
    const optimisticEntry: FinanceEntry | null = currentEntry
      ? {
          ...currentEntry,
          title: input.title,
          notes: input.notes?.trim() ? input.notes.trim() : null,
          amount: input.amount,
          type: input.type,
          verified: input.verified,
          referenceDate: input.referenceDate,
          recurringTemplateId: input.recurringTemplateId ?? null,
          categoryId: input.categoryId ?? null,
          categoryName: getCategoryName(input.categoryId),
          origin: input.recurringTemplateId ? "RecurringTemplate" : "Manual",
          ...classification,
        }
      : null;
    const optimisticDetail =
      affectsActivePeriod && previousPeriodDetail && optimisticEntry
        ? normalizePeriodDetail({
            ...previousPeriodDetail,
            exists: true,
            entries: previousPeriodDetail.entries.map((entry) => (entry.id === entryId ? optimisticEntry : entry)),
          })
        : null;

    beginSync("cash");
    try {
      if (optimisticDetail) {
        startTransition(() => {
          setPeriodDetail(optimisticDetail);
          setFinancePeriods(upsertFinancePeriodFromDetail(previousPeriods, optimisticDetail));
        });
      }

      const updatedEntry = await apiFetch<FinanceEntry>(`/api/finance/entries/${entryId}`, {
        method: "PUT",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify(input),
      });

      if (!previousPeriodDetail || !affectsActivePeriod) {
        void (async () => {
          try {
            await reconcileActivePeriod();
          } catch (reconcileException) {
            reportReconcileError(reconcileException, "O lançamento foi salvo, mas não foi possível sincronizar o período ativo.");
          } finally {
            endSync("cash");
          }
        })();
      } else {
        const nextDetail = normalizePeriodDetail({
          ...(optimisticDetail ?? previousPeriodDetail),
          id: previousPeriodDetail.id ?? updatedEntry.periodId,
          exists: true,
          entries: (optimisticDetail ?? previousPeriodDetail).entries.map((entry) => (entry.id === entryId ? updatedEntry : entry)),
        });
        startTransition(() => {
          setPeriodDetail(nextDetail);
          setFinancePeriods((current) => upsertFinancePeriodFromDetail(current, nextDetail));
        });
        endSync("cash");
      }

      notifyMutationSuccess("Lançamento atualizado.", options);
    } catch (exception) {
      if (optimisticDetail) {
        startTransition(() => {
          setPeriodDetail(previousPeriodDetail);
          setFinancePeriods(previousPeriods);
        });
      }
      endSync("cash");
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
        coreId: entry.coreId ?? null,
        projectId: entry.projectId ?? null,
      },
      { silentSuccess: true },
    );
  }

  async function createCategory(input: FinanceCategoryFormInput) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      await apiFetch<FinanceCategory>("/api/finance/categories", {
        method: "POST",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Categoria criada.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar a categoria.");
    }
  }

  async function updateCategory(categoryId: string, input: FinanceCategoryFormInput, options?: FinanceMutationOptions) {
    if (!session || !activeSpaceId) {
      return;
    }

    const previousCategories = categories;
    const previousPeriodDetail = periodDetail;
    const previousRecurringTemplates = recurringTemplates;
    const previousCreditCardTransactions = creditCardTransactions;
    const trimmedName = input.name.trim();
    const optimisticCategories = sortCategories(
      categories.map((category) =>
        category.id === categoryId
          ? {
              ...category,
              name: trimmedName,
            }
          : category,
      ),
    );
    const optimisticRecurringTemplates = sortRecurringTemplates(
      recurringTemplates.map((template) =>
        template.categoryId === categoryId
          ? {
              ...template,
              categoryName: trimmedName,
            }
          : template,
      ),
    );
    const optimisticPeriodDetail = previousPeriodDetail
      ? normalizePeriodDetail({
          ...previousPeriodDetail,
          entries: previousPeriodDetail.entries.map((entry) =>
            entry.categoryId === categoryId
              ? {
                  ...entry,
                  categoryName: trimmedName,
                }
              : entry,
          ),
          cardTransactions: previousPeriodDetail.cardTransactions.map((transaction) =>
            transaction.categoryId === categoryId
              ? {
                  ...transaction,
                  categoryName: trimmedName,
                }
              : transaction,
          ),
        })
      : null;
    const optimisticCreditCardTransactions = sortCreditCardTransactions(
      creditCardTransactions.map((transaction) =>
        transaction.categoryId === categoryId
          ? {
              ...transaction,
              categoryName: trimmedName,
            }
          : transaction,
      ),
    );

    beginSync("categories");
    try {
      startTransition(() => {
        setCategories(optimisticCategories);
        setRecurringTemplates(optimisticRecurringTemplates);
        setPeriodDetail(optimisticPeriodDetail);
        setCreditCardTransactions(optimisticCreditCardTransactions);
      });

      const updatedCategory = await apiFetch<FinanceCategory>(`/api/finance/categories/${categoryId}`, {
        method: "PUT",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify(input),
      });
      startTransition(() => {
        setCategories((current) =>
          sortCategories(current.map((category) => (category.id === categoryId ? updatedCategory : category))),
        );
      });
      endSync("categories");
      notifyMutationSuccess("Categoria atualizada.", options);
    } catch (exception) {
      startTransition(() => {
        setCategories(previousCategories);
        setRecurringTemplates(previousRecurringTemplates);
        setPeriodDetail(previousPeriodDetail);
        setCreditCardTransactions(previousCreditCardTransactions);
      });
      endSync("categories");
      reportError(exception, "Não foi possível atualizar a categoria.");
    }
  }

  async function deleteCategory(categoryId: string) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      await apiFetch<void>(`/api/finance/categories/${categoryId}`, {
        method: "DELETE",
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      await refreshWorkspace();
      toast.success("Categoria excluída.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir a categoria.");
    }
  }

  async function deleteEntry(entryId: string) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      await apiFetch<void>(`/api/finance/entries/${entryId}`, {
        method: "DELETE",
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      await refreshWorkspace();
      toast.success("Lançamento excluído.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir o lançamento.");
    }
  }

  async function deleteEntries(entryIds: string[]) {
    if (!session || !activeSpaceId || entryIds.length === 0) {
      return;
    }

    try {
      await Promise.all(
        entryIds.map((entryId) =>
          apiFetch<void>(`/api/finance/entries/${entryId}`, {
            method: "DELETE",
            token: session.accessToken,
            spaceId: activeSpaceId,
          }),
        ),
      );
      await refreshWorkspace();
      toast.success(entryIds.length === 1 ? "Lançamento excluído." : `${entryIds.length} lançamentos excluídos.`);
    } catch (exception) {
      reportError(exception, "Não foi possível excluir os lançamentos selecionados.");
    }
  }

  async function createRecurringTemplate(input: FinanceRecurringTemplateFormInput) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      await apiFetch<FinanceRecurringTemplate>("/api/finance/recurring-templates", {
        method: "POST",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Recorrência criada.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar a recorrência.");
    }
  }

  async function updateRecurringTemplate(templateId: string, input: FinanceRecurringTemplateFormInput, options?: FinanceMutationOptions) {
    if (!session || !activeSpaceId) {
      return;
    }

    const previousTemplates = recurringTemplates;
    const classification = resolveClassification(input.coreId, input.projectId);
    const optimisticTemplates = sortRecurringTemplates(
      recurringTemplates.map((template) =>
        template.id === templateId
          ? {
              ...template,
              title: input.title,
              notes: input.notes?.trim() ? input.notes.trim() : null,
              type: input.type,
              defaultAmount: input.defaultAmount,
              recurrence: input.recurrence,
              dayOfMonth: input.dayOfMonth ?? null,
              monthOfYear: input.monthOfYear ?? null,
              isActive: input.isActive,
              categoryId: input.categoryId ?? null,
              categoryName: getCategoryName(input.categoryId),
              ...classification,
            }
          : template,
      ),
    );

    beginSync("recurringTemplates");
    try {
      startTransition(() => {
        setRecurringTemplates(optimisticTemplates);
      });

      const updatedTemplate = await apiFetch<FinanceRecurringTemplate>(`/api/finance/recurring-templates/${templateId}`, {
        method: "PUT",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify(input),
      });
      startTransition(() => {
        setRecurringTemplates((current) =>
          sortRecurringTemplates(current.map((template) => (template.id === templateId ? updatedTemplate : template))),
        );
      });
      endSync("recurringTemplates");
      notifyMutationSuccess("Recorrência atualizada.", options);
    } catch (exception) {
      startTransition(() => {
        setRecurringTemplates(previousTemplates);
      });
      endSync("recurringTemplates");
      reportError(exception, "Não foi possível atualizar a recorrência.");
    }
  }

  async function deleteRecurringTemplate(templateId: string) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      await apiFetch<void>(`/api/finance/recurring-templates/${templateId}`, {
        method: "DELETE",
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      await refreshWorkspace();
      toast.success("Recorrência excluída.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir a recorrência.");
    }
  }

  async function createAsset(input: AssetFormInput) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      await apiFetch<Asset>("/api/finance/assets", {
        method: "POST",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Bem criado.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar o bem.");
    }
  }

  async function updateAsset(assetId: string, input: AssetFormInput) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      await apiFetch<Asset>(`/api/finance/assets/${assetId}`, {
        method: "PUT",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Bem atualizado.");
    } catch (exception) {
      reportError(exception, "Não foi possível atualizar o bem.");
    }
  }

  async function deleteAsset(assetId: string) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      await apiFetch<void>(`/api/finance/assets/${assetId}`, {
        method: "DELETE",
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      await refreshWorkspace();
      toast.success("Bem excluido.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir o bem.");
    }
  }

  async function loadAssetValuations(assetId: string) {
    if (!session || !activeSpaceId) {
      return;
    }

    setAssetValuationsLoadingFor(assetId);
    try {
      const valuations = await apiFetch<AssetValuation[]>(`/api/finance/assets/${assetId}/valuations`, {
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      setAssetValuations((current) => ({
        ...current,
        [assetId]: sortAssetValuations(valuations),
      }));
    } catch (exception) {
      reportError(exception, "Não foi possível carregar as referências anuais.");
    } finally {
      setAssetValuationsLoadingFor(null);
    }
  }

  async function createAssetValuation(assetId: string, input: AssetValuationFormInput) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      await apiFetch<AssetValuation>(`/api/finance/assets/${assetId}/valuations`, {
        method: "POST",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify(input),
      });
      await loadAssetValuations(assetId);
      toast.success("Referência anual criada.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar a referência anual.");
    }
  }

  async function updateAssetValuation(assetId: string, valuationId: string, input: AssetValuationFormInput, options?: FinanceMutationOptions) {
    if (!session || !activeSpaceId) {
      return;
    }

    const previousValuations = assetValuations[assetId] ?? [];
    const optimisticValuations = sortAssetValuations(
      previousValuations.map((valuation) =>
        valuation.id === valuationId
          ? {
              ...valuation,
              referenceYear: input.referenceYear,
              label: input.label.trim(),
              amount: input.amount,
              notes: input.notes?.trim() ? input.notes.trim() : null,
            }
          : valuation,
      ),
    );

    beginSync("assetValuations");
    try {
      startTransition(() => {
        setAssetValuations((current) => ({
          ...current,
          [assetId]: optimisticValuations,
        }));
      });

      const updatedValuation = await apiFetch<AssetValuation>(`/api/finance/assets/${assetId}/valuations/${valuationId}`, {
        method: "PUT",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify(input),
      });
      startTransition(() => {
        setAssetValuations((current) => ({
          ...current,
          [assetId]: sortAssetValuations(
            (current[assetId] ?? []).map((valuation) => (valuation.id === valuationId ? updatedValuation : valuation)),
          ),
        }));
      });
      endSync("assetValuations");
      notifyMutationSuccess("Referência anual atualizada.", options);
    } catch (exception) {
      startTransition(() => {
        setAssetValuations((current) => ({
          ...current,
          [assetId]: previousValuations,
        }));
      });
      endSync("assetValuations");
      reportError(exception, "Não foi possível atualizar a referência anual.");
    }
  }

  async function deleteAssetValuation(assetId: string, valuationId: string) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      await apiFetch<void>(`/api/finance/assets/${assetId}/valuations/${valuationId}`, {
        method: "DELETE",
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      await loadAssetValuations(assetId);
      toast.success("Referência anual excluída.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir a referência anual.");
    }
  }

  async function createCreditCardAccount(input: CreditCardAccountFormInput) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      await apiFetch<CreditCardAccount>("/api/finance/credit-cards", {
        method: "POST",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Cartão criado.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar o cartão.");
    }
  }

  async function updateCreditCardAccount(cardId: string, input: CreditCardAccountFormInput) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      await apiFetch<CreditCardAccount>(`/api/finance/credit-cards/${cardId}`, {
        method: "PUT",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Cartão atualizado.");
    } catch (exception) {
      reportError(exception, "Não foi possível atualizar o cartão.");
    }
  }

  async function deleteCreditCardAccount(cardId: string) {
    if (!session || !activeSpaceId) {
      return;
    }

    try {
      await apiFetch<void>(`/api/finance/credit-cards/${cardId}`, {
        method: "DELETE",
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      await refreshWorkspace();
      toast.success("Cartão excluído.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir o cartão.");
    }
  }

  async function createCreditCardTransaction(input: CreditCardTransactionFormInput) {
    if (!session || !activeSpaceId || !selectedCreditCardId) {
      return;
    }

    try {
      await apiFetch<CreditCardTransaction>(`/api/finance/credit-cards/${selectedCreditCardId}/transactions`, {
        method: "POST",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Compra no cartão criada.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar a compra no cartão.");
    }
  }

  async function importCreditCardTransactions(items: ImportCreditCardTransactionItem[]) {
    if (!session || !activeSpaceId || !selectedCreditCardId || items.length === 0) {
      return null;
    }

    try {
      const response = await apiFetch<ImportCreditCardTransactionsResponse>(
        `/api/finance/credit-cards/${selectedCreditCardId}/transactions/import`,
        {
          method: "POST",
          token: session.accessToken,
          spaceId: activeSpaceId,
          body: JSON.stringify({ transactions: items }),
        },
      );
      await refreshWorkspace();
      toast.success(
        response.totalCount === 1
          ? "1 compra importada no cartão."
          : `${response.totalCount} compras importadas no cartão.`,
      );
      return response;
    } catch (exception) {
      reportError(exception, "Não foi possível importar as compras do cartão.");
      throw exception;
    }
  }

  async function updateCreditCardTransaction(transactionId: string, input: CreditCardTransactionFormInput, options?: FinanceMutationOptions) {
    if (!session || !activeSpaceId || !selectedCreditCardId) {
      return;
    }

    const previousPeriodDetail = periodDetail;
    const previousPeriods = financePeriods;
    const previousTransactions = creditCardTransactions;
    const previousStatements = creditCardStatements;
    const previousAccounts = creditCardAccounts;
    const previousTransaction =
      creditCardTransactions.find((transaction) => transaction.id === transactionId) ??
      previousPeriodDetail?.cardTransactions.find((transaction) => transaction.id === transactionId) ??
      null;
    const classification = resolveClassification(input.coreId, input.projectId);
    const optimisticTransaction: CreditCardTransaction | null = previousTransaction
      ? {
          ...previousTransaction,
          title: input.title,
          merchant: input.merchant?.trim() ? input.merchant.trim() : null,
          amount: input.amount,
          purchasedOn: input.purchasedOn,
          notes: input.notes?.trim() ? input.notes.trim() : null,
          categoryId: input.categoryId ?? null,
          categoryName: getCategoryName(input.categoryId),
          externalSource: input.externalSource?.trim() ? input.externalSource.trim() : null,
          externalReference: input.externalReference?.trim() ? input.externalReference.trim() : null,
          ...classification,
        }
      : null;
    const amountDelta = optimisticTransaction && previousTransaction ? optimisticTransaction.amount - previousTransaction.amount : 0;
    const wasVisibleInActivePeriod = Boolean(
      previousTransaction &&
        previousPeriodDetail &&
        previousTransaction.purchasedOn.startsWith(`${previousPeriodDetail.year}-${String(previousPeriodDetail.month).padStart(2, "0")}-`),
    );
    const isVisibleInActivePeriod = Boolean(
      optimisticTransaction &&
        previousPeriodDetail &&
        optimisticTransaction.purchasedOn.startsWith(`${previousPeriodDetail.year}-${String(previousPeriodDetail.month).padStart(2, "0")}-`),
    );

    beginSync("cash", "cardTransactions", "cardStatements");
    try {
      if (optimisticTransaction) {
        const optimisticTransactions = sortCreditCardTransactions(
          previousTransactions.map((transaction) => (transaction.id === transactionId ? optimisticTransaction : transaction)),
        );
        const optimisticStatements =
          amountDelta !== 0 && previousTransaction?.creditCardStatementId
            ? sortCreditCardStatements(
                previousStatements.map((statement) =>
                  statement.id === previousTransaction.creditCardStatementId
                    ? {
                        ...statement,
                        totalAmount: statement.totalAmount + amountDelta,
                      }
                    : statement,
                ),
              )
            : previousStatements;
        const optimisticDetail = previousPeriodDetail
          ? normalizePeriodDetail({
              ...previousPeriodDetail,
              entries:
                amountDelta !== 0 && previousTransaction?.creditCardStatementId
                  ? previousPeriodDetail.entries.map((entry) =>
                      optimisticStatements.some(
                        (statement) => statement.id === previousTransaction.creditCardStatementId && statement.financeEntryId === entry.id,
                      )
                        ? {
                            ...entry,
                            amount: entry.amount + amountDelta,
                          }
                        : entry,
                    )
                  : previousPeriodDetail.entries,
              cardTransactions:
                wasVisibleInActivePeriod || isVisibleInActivePeriod
                  ? previousPeriodDetail.cardTransactions
                      .filter((transaction) => transaction.id !== transactionId || isVisibleInActivePeriod)
                      .map((transaction) => (transaction.id === transactionId ? optimisticTransaction : transaction))
                      .concat(
                        !wasVisibleInActivePeriod && isVisibleInActivePeriod ? [optimisticTransaction] : [],
                      )
                  : previousPeriodDetail.cardTransactions,
              statements:
                amountDelta !== 0 && previousTransaction?.creditCardStatementId
                  ? previousPeriodDetail.statements.map((statement) =>
                      statement.id === previousTransaction.creditCardStatementId
                        ? {
                            ...statement,
                            totalAmount: statement.totalAmount + amountDelta,
                          }
                        : statement,
                    )
                  : previousPeriodDetail.statements,
            })
          : null;
        const nextAccounts =
          optimisticTransaction.creditCardStatementId == null
            ? sortCreditCardAccounts(
                previousAccounts.map((account) =>
                  account.id === selectedCreditCardId
                    ? {
                        ...account,
                        ...summarizeOpenCardTransactions(optimisticTransactions),
                      }
                    : account,
                ),
              )
            : previousAccounts;

        startTransition(() => {
          setCreditCardTransactions(optimisticTransactions);
          setCreditCardStatements(optimisticStatements);
          setCreditCardAccounts(nextAccounts);
          if (optimisticDetail) {
            setPeriodDetail(optimisticDetail);
            setFinancePeriods(upsertFinancePeriodFromDetail(previousPeriods, optimisticDetail));
          }
        });
      }

      const updatedTransaction = await apiFetch<CreditCardTransaction>(
        `/api/finance/credit-cards/${selectedCreditCardId}/transactions/${transactionId}`,
        {
          method: "PUT",
          token: session.accessToken,
          spaceId: activeSpaceId,
          body: JSON.stringify(input),
        },
      );

      if (optimisticTransaction) {
        const nextTransactions = sortCreditCardTransactions(
          previousTransactions.map((transaction) => (transaction.id === transactionId ? updatedTransaction : transaction)),
        );
        startTransition(() => {
          setCreditCardTransactions(nextTransactions);
          if (updatedTransaction.creditCardStatementId == null) {
            setCreditCardAccounts((current) =>
              sortCreditCardAccounts(
                current.map((account) =>
                  account.id === selectedCreditCardId
                    ? {
                        ...account,
                        ...summarizeOpenCardTransactions(nextTransactions),
                      }
                    : account,
                ),
              ),
            );
          }
        });
      }

      void reconcileCardFinanceSections("cash", "cardTransactions", "cardStatements");
      notifyMutationSuccess("Compra no cartão atualizada.", options);
    } catch (exception) {
      startTransition(() => {
        setPeriodDetail(previousPeriodDetail);
        setFinancePeriods(previousPeriods);
        setCreditCardTransactions(previousTransactions);
        setCreditCardStatements(previousStatements);
        setCreditCardAccounts(previousAccounts);
      });
      endSync("cash", "cardTransactions", "cardStatements");
      reportError(exception, "Não foi possível atualizar a compra no cartão.");
    }
  }

  async function deleteCreditCardTransaction(transactionId: string) {
    if (!session || !activeSpaceId || !selectedCreditCardId) {
      return;
    }

    try {
      await apiFetch<void>(`/api/finance/credit-cards/${selectedCreditCardId}/transactions/${transactionId}`, {
        method: "DELETE",
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      await refreshWorkspace();
      toast.success("Compra no cartão excluída.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir a compra no cartão.");
    }
  }

  async function deleteCreditCardTransactions(transactionIds: string[]) {
    if (!session || !activeSpaceId || !selectedCreditCardId || transactionIds.length === 0) {
      return;
    }

    try {
      await Promise.all(
        transactionIds.map((transactionId) =>
          apiFetch<void>(`/api/finance/credit-cards/${selectedCreditCardId}/transactions/${transactionId}`, {
            method: "DELETE",
            token: session.accessToken,
            spaceId: activeSpaceId,
          }),
        ),
      );
      await refreshWorkspace();
      toast.success(transactionIds.length === 1 ? "Compra no cartão excluída." : `${transactionIds.length} compras no cartão excluídas.`);
    } catch (exception) {
      reportError(exception, "Não foi possível excluir as compras no cartão selecionadas.");
    }
  }

  async function createCreditCardStatement(input: CreditCardStatementFormInput) {
    if (!session || !activeSpaceId || !selectedCreditCardId) {
      return;
    }

    try {
      await apiFetch<CreditCardStatement>(`/api/finance/credit-cards/${selectedCreditCardId}/statements`, {
        method: "POST",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify(input),
      });
      await refreshWorkspace();
      toast.success("Fatura criada.");
    } catch (exception) {
      reportError(exception, "Não foi possível criar a fatura.");
    }
  }

  async function updateCreditCardStatement(statementId: string, input: CreditCardStatementFormInput, options?: FinanceMutationOptions) {
    if (!session || !activeSpaceId || !selectedCreditCardId) {
      return;
    }

    const previousPeriodDetail = periodDetail;
    const previousPeriods = financePeriods;
    const previousStatements = creditCardStatements;
    const previousStatement = creditCardStatements.find((statement) => statement.id === statementId) ?? null;
    const optimisticStatement: CreditCardStatement | null = previousStatement
      ? {
          ...previousStatement,
          closingDate: input.closingDate,
          dueDate: input.dueDate,
          notes: input.notes?.trim() ? input.notes.trim() : null,
          transactionCount: input.transactionIds.length,
          externalSource: input.externalSource?.trim() ? input.externalSource.trim() : null,
          externalReference: input.externalReference?.trim() ? input.externalReference.trim() : null,
        }
      : null;
    const optimisticStatements = optimisticStatement
      ? sortCreditCardStatements(
          previousStatements.map((statement) => (statement.id === statementId ? optimisticStatement : statement)),
        )
      : previousStatements;
    const optimisticPeriodDetail =
      previousPeriodDetail && optimisticStatement
        ? normalizePeriodDetail({
            ...previousPeriodDetail,
            entries: previousStatement?.financeEntryId
              ? previousPeriodDetail.entries.map((entry) =>
                  entry.id === previousStatement.financeEntryId
                    ? {
                        ...entry,
                        title: `Fatura ${optimisticStatement.creditCardAccountName} - ${optimisticStatement.dueDate.slice(5, 7)}/${optimisticStatement.dueDate.slice(0, 4)}`,
                        notes: optimisticStatement.notes ?? null,
                        referenceDate: optimisticStatement.dueDate,
                      }
                    : entry,
                )
              : previousPeriodDetail.entries,
            statements: previousPeriodDetail.statements.map((statement) =>
              statement.id === statementId ? optimisticStatement : statement,
            ),
          })
        : null;

    beginSync("cash", "cardTransactions", "cardStatements");
    try {
      startTransition(() => {
        setCreditCardStatements(optimisticStatements);
        if (optimisticPeriodDetail) {
          setPeriodDetail(optimisticPeriodDetail);
          setFinancePeriods(upsertFinancePeriodFromDetail(previousPeriods, optimisticPeriodDetail));
        }
      });

      const updatedStatement = await apiFetch<CreditCardStatement>(`/api/finance/credit-cards/${selectedCreditCardId}/statements/${statementId}`, {
        method: "PUT",
        token: session.accessToken,
        spaceId: activeSpaceId,
        body: JSON.stringify(input),
      });
      startTransition(() => {
        setCreditCardStatements((current) =>
          sortCreditCardStatements(current.map((statement) => (statement.id === statementId ? updatedStatement : statement))),
        );
      });
      void reconcileCardFinanceSections("cash", "cardTransactions", "cardStatements");
      notifyMutationSuccess("Fatura atualizada.", options);
    } catch (exception) {
      startTransition(() => {
        setPeriodDetail(previousPeriodDetail);
        setFinancePeriods(previousPeriods);
        setCreditCardStatements(previousStatements);
      });
      endSync("cash", "cardTransactions", "cardStatements");
      reportError(exception, "Não foi possível atualizar a fatura.");
    }
  }

  async function deleteCreditCardStatement(statementId: string) {
    if (!session || !activeSpaceId || !selectedCreditCardId) {
      return;
    }

    try {
      await apiFetch<void>(`/api/finance/credit-cards/${selectedCreditCardId}/statements/${statementId}`, {
        method: "DELETE",
        token: session.accessToken,
        spaceId: activeSpaceId,
      });
      await refreshWorkspace();
      toast.success("Fatura excluída.");
    } catch (exception) {
      reportError(exception, "Não foi possível excluir a fatura.");
    }
  }

  return {
    session,
    activeSpaceId,
    activeSpace,
    members,
    cores,
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
    editingSpace,
    isSpaceDialogOpen: activeCommonModal === "space",
    isShareDialogOpen: activeCommonModal === "share",
    sidebarCollapsed,
    theme,
    loading,
    error,
    syncingSections,
    subtitle: "Fluxo mensal, recorrências, cartões e patrimônio do espaço",
    canShareSpace,
    canManageSpace,
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
    handleSpaceChange,
    handleLogout,
    refreshSpaces,
    refreshWorkspace,
    openCreateSpace,
    openEditSpace,
    openShareSpace,
    closeCommonModal,
    createSpace,
    updateSpace,
    deleteSpace,
    shareSpace,
    updateProfile,
    generatePeriod,
    createCategory,
    updateCategory,
    deleteCategory,
    createEntry,
    updateEntry,
    toggleEntryVerified,
    deleteEntry,
    deleteEntries,
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
    importCreditCardTransactions,
    updateCreditCardTransaction,
    deleteCreditCardTransaction,
    deleteCreditCardTransactions,
    createCreditCardStatement,
    updateCreditCardStatement,
    deleteCreditCardStatement,
  };
}

export type FinanceDashboardController = ReturnType<typeof useFinanceDashboard>;

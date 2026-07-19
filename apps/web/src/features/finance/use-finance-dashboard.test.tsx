import { act, renderHook } from "@testing-library/react";
import { toast } from "sonner";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type {
  AuthResponse,
  CreditCardAccount,
  CreditCardStatement,
  CreditCardTransaction,
  FinanceCategory,
  FinanceEntry,
  FinancePeriodDetail,
  FinancePeriodListItem,
} from "@/lib/api";
import * as api from "@/lib/api";
import { useFinanceDashboard } from "./use-finance-dashboard";

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

function createDeferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });

  return { promise, resolve, reject };
}

async function flushDashboardEffects() {
  await act(async () => {
    await Promise.resolve();
    vi.runAllTimers();
    await Promise.resolve();
    vi.runAllTimers();
    await Promise.resolve();
  });
}

function buildSession(): AuthResponse {
  return {
    accessToken: "token-1",
    refreshToken: "refresh-1",
    expiresAt: "2026-07-06T12:00:00.000Z",
    user: {
      id: "user-1",
      email: "owner@organiza.club",
      displayName: "Owner",
      systemRole: "User",
      phoneNumber: null,
      hasProfilePhoto: false,
      profilePhotoUpdatedAt: null,
    },
    spaces: [{ id: "space-1", name: "Espaço Financeira", role: "Owner" }],
  };
}

function buildEntry(overrides: Partial<FinanceEntry> & Pick<FinanceEntry, "id" | "title">): FinanceEntry {
  return {
    id: overrides.id,
    periodId: "period-1",
    year: 2026,
    month: 7,
    title: overrides.title,
    notes: null,
    amount: 0,
    type: "Saida",
    verified: false,
    referenceDate: "2026-07-06",
    origin: "Manual",
    recurringTemplateId: null,
    creditCardStatementId: null,
    categoryId: null,
    categoryName: null,
    coreId: null,
    coreName: null,
    projectId: null,
    projectName: null,
    createdByMemberId: "member-1",
    createdAt: "2026-07-06T12:00:00.000Z",
    updatedAt: "2026-07-06T12:00:00.000Z",
    canEdit: true,
    canDelete: true,
    ...overrides,
  };
}

function buildPeriodDetail(overrides: Partial<FinancePeriodDetail> = {}): FinancePeriodDetail {
  return {
    id: "period-1",
    year: 2026,
    month: 7,
    exists: true,
    summary: {
      totalIncome: 5000,
      totalExpense: 700,
      cashBalance: 4300,
      analyticalExpenseTotal: 820.9,
      verifiedEntries: 2,
      pendingVerificationEntries: 0,
      cardPurchaseCount: 1,
    },
    entries: [
      buildEntry({ id: "entry-1", title: "Condominio", amount: 700, verified: true }),
      buildEntry({ id: "entry-2", title: "Fatura Nubank", amount: 220.9, origin: "CreditCardStatement", verified: true }),
    ],
    cardTransactions: [],
    statements: [],
    ...overrides,
  };
}

function buildCard(overrides: Partial<CreditCardAccount> = {}): CreditCardAccount {
  return {
    id: "card-1",
    name: "Nubank",
    brand: "Mastercard",
    lastFourDigits: "1234",
    closingDay: 20,
    dueDay: 25,
    notes: null,
    isActive: true,
    openTransactionCount: 1,
    openTransactionTotal: 220.9,
    createdByMemberId: "member-1",
    createdAt: "2026-07-01T12:00:00.000Z",
    updatedAt: "2026-07-01T12:00:00.000Z",
    canEdit: true,
    canDelete: true,
    ...overrides,
  };
}

function buildTransaction(overrides: Partial<CreditCardTransaction> = {}): CreditCardTransaction {
  return {
    id: "tx-1",
    creditCardAccountId: "card-1",
    creditCardAccountName: "Nubank",
    creditCardStatementId: null,
    title: "Supermercado",
    merchant: "Mercado",
    amount: 220.9,
    purchasedOn: "2026-07-06",
    notes: null,
    categoryId: "category-1",
    categoryName: "Mercado",
    coreId: "core-1",
    coreName: "Espaço",
    projectId: "project-1",
    projectName: "Moradia",
    externalSource: "SMS",
    externalReference: "sms-1",
    importedAt: null,
    createdByMemberId: "member-1",
    createdAt: "2026-07-06T12:00:00.000Z",
    updatedAt: "2026-07-06T12:00:00.000Z",
    canEdit: true,
    canDelete: true,
    ...overrides,
  };
}

function buildStatement(): CreditCardStatement {
  return {
    id: "statement-1",
    creditCardAccountId: "card-1",
    creditCardAccountName: "Nubank",
    closingDate: "2026-07-20",
    dueDate: "2026-07-25",
    totalAmount: 220.9,
    notes: "Fatura de julho",
    transactionCount: 1,
    financeEntryId: "entry-2",
    externalSource: "XLS",
    externalReference: "fatura-1",
    importedAt: null,
    createdByMemberId: "member-1",
    createdAt: "2026-07-20T12:00:00.000Z",
    updatedAt: "2026-07-20T12:00:00.000Z",
    canEdit: true,
    canDelete: true,
  };
}

function buildCategories(): FinanceCategory[] {
  return [
    {
      id: "category-1",
      name: "Mercado",
      isDefault: true,
      sortOrder: 2,
      createdByMemberId: "member-1",
      usageCount: 2,
      canEdit: false,
      canDelete: false,
    },
  ];
}

describe("useFinanceDashboard", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-07-06T12:00:00.000Z"));
    window.localStorage.clear();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("loads the current month by default", async () => {
    const session = buildSession();
    mockedReadSession.mockReturnValue(session);
    mockedSubscribeToSessionChanges.mockReturnValue(() => undefined);

    const currentPeriodDetail = buildPeriodDetail();
    const currentPeriods: FinancePeriodListItem[] = [
      {
        id: "period-1",
        year: 2026,
        month: 7,
        totalIncome: 5000,
        totalExpense: 700,
        cashBalance: 4300,
        entryCount: 1,
      },
    ];

    mockedApiFetch.mockImplementation(async (path: string) => {
      if (path === "/api/spaces/members" || path === "/api/cores" || path === "/api/projects") {
        return [];
      }

      if (path === "/api/finance/categories") {
        return buildCategories();
      }

      if (path === "/api/finance/periods") {
        return currentPeriods;
      }

      if (path === "/api/finance/periods/2026/7") {
        return currentPeriodDetail;
      }

      if (path === "/api/finance/recurring-templates" || path === "/api/finance/assets" || path === "/api/finance/credit-cards") {
        return [];
      }

      throw new Error(`Unexpected API path: ${path}`);
    });

    const { result, unmount } = renderHook(() => useFinanceDashboard());

    await flushDashboardEffects();
    await flushDashboardEffects();

    expect(result.current.activeYear).toBe(2026);
    expect(result.current.activeMonth).toBe(7);
    expect(result.current.periodDetail?.year).toBe(2026);
    expect(result.current.categories).toEqual(buildCategories());
    expect(mockedApiFetch).toHaveBeenCalledWith(
      "/api/finance/periods/2026/7",
      expect.objectContaining({ spaceId: "space-1", token: "token-1" }),
    );

    unmount();
  });

  it("refreshes the monthly cash view and the selected card after creating a statement", async () => {
    const session = buildSession();
    mockedReadSession.mockReturnValue(session);
    mockedSubscribeToSessionChanges.mockReturnValue(() => undefined);

    let statementCreated = false;
    mockedApiFetch.mockImplementation(async (path: string, options?: RequestInit & { spaceId?: string }) => {
      if (path === "/api/spaces/members" || path === "/api/cores" || path === "/api/projects") {
        return [];
      }

      if (path === "/api/finance/categories") {
        return buildCategories();
      }

      if (path === "/api/finance/periods") {
        return [{ id: "period-1", year: 2026, month: 7, totalIncome: 5000, totalExpense: statementCreated ? 920.9 : 700, cashBalance: statementCreated ? 4079.1 : 4300, entryCount: statementCreated ? 2 : 1 }];
      }

      if (path === "/api/finance/periods/2026/7") {
        return buildPeriodDetail(
          statementCreated
            ? {
                summary: {
                  totalIncome: 5000,
                  totalExpense: 920.9,
                  cashBalance: 4079.1,
                  analyticalExpenseTotal: 920.9,
                  verifiedEntries: 2,
                  pendingVerificationEntries: 0,
                  cardPurchaseCount: 1,
                },
                entries: [
                  buildEntry({ id: "entry-1", title: "Condominio", amount: 700, verified: true }),
                  buildEntry({
                    id: "entry-2",
                    title: "Fatura Nubank - 07/2026",
                    amount: 220.9,
                    origin: "CreditCardStatement",
                    creditCardStatementId: "statement-1",
                    verified: true,
                  }),
                ],
                cardTransactions: [buildTransaction({ creditCardStatementId: "statement-1" })],
                statements: [buildStatement()],
              }
            : {
                cardTransactions: [],
                statements: [],
              },
        );
      }

      if (path === "/api/finance/recurring-templates" || path === "/api/finance/assets") {
        return [];
      }

      if (path === "/api/finance/credit-cards") {
        return [buildCard()];
      }

      if (path === "/api/finance/credit-cards/card-1/transactions") {
        return statementCreated ? [buildTransaction({ creditCardStatementId: "statement-1" })] : [buildTransaction()];
      }

      if (path === "/api/finance/credit-cards/card-1/statements" && options?.method === "POST") {
        statementCreated = true;
        return buildStatement();
      }

      if (path === "/api/finance/credit-cards/card-1/statements") {
        return statementCreated ? [buildStatement()] : [];
      }

      throw new Error(`Unexpected API path: ${path}`);
    });

    const { result, unmount } = renderHook(() => useFinanceDashboard());

    await flushDashboardEffects();
    await flushDashboardEffects();

    expect(result.current.creditCardAccounts).toHaveLength(1);
    expect(result.current.creditCardTransactions).toHaveLength(1);

    await act(async () => {
      await result.current.createCreditCardStatement({
        closingDate: "2026-07-20",
        dueDate: "2026-07-25",
        notes: "Fatura de julho",
        transactionIds: ["tx-1"],
        externalSource: "XLS",
        externalReference: "fatura-1",
      });
    });

    expect(result.current.periodDetail?.entries).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          title: "Fatura Nubank - 07/2026",
          origin: "CreditCardStatement",
          creditCardStatementId: "statement-1",
          verified: true,
        }),
      ]),
    );
    expect(result.current.periodDetail?.summary.verifiedEntries).toBe(2);
    expect(result.current.periodDetail?.summary.pendingVerificationEntries).toBe(0);
    expect(result.current.creditCardStatements).toEqual([
      expect.objectContaining({ id: "statement-1", totalAmount: 220.9 }),
    ]);
    expect(mockedToast.success).toHaveBeenCalledWith("Fatura criada.");

    unmount();
  });

  it("applies optimistic entry updates immediately and rolls them back on request failure", async () => {
    const session = buildSession();
    mockedReadSession.mockReturnValue(session);
    mockedSubscribeToSessionChanges.mockReturnValue(() => undefined);

    const updateDeferred = createDeferred<FinanceEntry>();

    mockedApiFetch.mockImplementation(async (path: string, options?: RequestInit & { spaceId?: string }) => {
      if (path === "/api/spaces/members" || path === "/api/cores" || path === "/api/projects") {
        return [];
      }

      if (path === "/api/finance/categories") {
        return buildCategories();
      }

      if (path === "/api/finance/periods") {
        return [{ id: "period-1", year: 2026, month: 7, totalIncome: 5000, totalExpense: 700, cashBalance: 4300, entryCount: 1 }];
      }

      if (path === "/api/finance/periods/2026/7") {
        return buildPeriodDetail();
      }

      if (path === "/api/finance/recurring-templates" || path === "/api/finance/assets" || path === "/api/finance/credit-cards") {
        return [];
      }

      if (path === "/api/finance/entries/entry-1" && options?.method === "PUT") {
        return await updateDeferred.promise;
      }

      throw new Error(`Unexpected API path: ${path}`);
    });

    const { result, unmount } = renderHook(() => useFinanceDashboard());

    await flushDashboardEffects();
    await flushDashboardEffects();

    let updatePromise: Promise<void> | undefined;
    await act(async () => {
      updatePromise = result.current.updateEntry(
        "entry-1",
        {
          year: 2026,
          month: 7,
          title: "Condominio atualizado",
          notes: "",
          amount: 850,
          type: "Saida",
          verified: false,
          referenceDate: "2026-07-06",
          recurringTemplateId: null,
          categoryId: null,
          coreId: null,
          projectId: null,
        },
        { silentSuccess: true },
      );
      await Promise.resolve();
    });

    expect(result.current.periodDetail?.entries).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          id: "entry-1",
          title: "Condominio atualizado",
          amount: 850,
          verified: false,
        }),
        expect.objectContaining({
          id: "entry-2",
          title: "Fatura Nubank",
          amount: 220.9,
          verified: true,
          origin: "CreditCardStatement",
        }),
      ]),
    );
    expect(result.current.periodDetail?.summary.totalExpense).toBe(1070.9);
    expect(result.current.periodDetail?.summary.cashBalance).toBe(-1070.9);
    expect(result.current.periodDetail?.summary.pendingVerificationEntries).toBe(1);

    await act(async () => {
      updateDeferred.reject(new Error("Falha simulada"));
      await expect(updatePromise).rejects.toThrow("Falha simulada");
    });

    expect(result.current.periodDetail?.entries).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          id: "entry-1",
          title: "Condominio",
          amount: 700,
          verified: true,
        }),
        expect.objectContaining({
          id: "entry-2",
          title: "Fatura Nubank",
          amount: 220.9,
          verified: true,
          origin: "CreditCardStatement",
        }),
      ]),
    );
    expect(result.current.periodDetail?.summary.totalExpense).toBe(920.9);
    expect(result.current.periodDetail?.summary.cashBalance).toBe(-920.9);
    expect(mockedToast.error).toHaveBeenCalledWith("Falha simulada");

    unmount();
  });

  it("updates card transactions optimistically and reconciles the card section in background", async () => {
    const session = buildSession();
    mockedReadSession.mockReturnValue(session);
    mockedSubscribeToSessionChanges.mockReturnValue(() => undefined);

    const updateDeferred = createDeferred<CreditCardTransaction>();
    let transactionUpdated = false;

    mockedApiFetch.mockImplementation(async (path: string, options?: RequestInit & { spaceId?: string }) => {
      if (path === "/api/spaces/members" || path === "/api/cores" || path === "/api/projects") {
        return [];
      }

      if (path === "/api/finance/categories") {
        return buildCategories();
      }

      if (path === "/api/finance/periods") {
        return [{ id: "period-1", year: 2026, month: 7, totalIncome: 5000, totalExpense: 700, cashBalance: 4300, entryCount: 1 }];
      }

      if (path === "/api/finance/periods/2026/7") {
        return buildPeriodDetail(
          transactionUpdated
            ? {
                summary: {
                  totalIncome: 5000,
                  totalExpense: 700,
                  cashBalance: 4300,
                  analyticalExpenseTotal: 1000,
                  verifiedEntries: 1,
                  pendingVerificationEntries: 0,
                  cardPurchaseCount: 1,
                },
                cardTransactions: [buildTransaction({ amount: 300, title: "Mercado do bairro" })],
              }
            : {
                summary: {
                  totalIncome: 5000,
                  totalExpense: 700,
                  cashBalance: 4300,
                  analyticalExpenseTotal: 920.9,
                  verifiedEntries: 1,
                  pendingVerificationEntries: 0,
                  cardPurchaseCount: 1,
                },
                cardTransactions: [buildTransaction()],
              },
        );
      }

      if (path === "/api/finance/recurring-templates" || path === "/api/finance/assets") {
        return [];
      }

      if (path === "/api/finance/credit-cards") {
        return [buildCard({ openTransactionTotal: transactionUpdated ? 300 : 220.9 })];
      }

      if (path === "/api/finance/credit-cards/card-1/transactions/tx-1" && options?.method === "PUT") {
        return await updateDeferred.promise;
      }

      if (path === "/api/finance/credit-cards/card-1/transactions") {
        return transactionUpdated ? [buildTransaction({ amount: 300, title: "Mercado do bairro" })] : [buildTransaction()];
      }

      if (path === "/api/finance/credit-cards/card-1/statements") {
        return [];
      }

      throw new Error(`Unexpected API path: ${path}`);
    });

    const { result, unmount } = renderHook(() => useFinanceDashboard());

    await flushDashboardEffects();
    await flushDashboardEffects();

    let updatePromise: Promise<void> | undefined;
    await act(async () => {
      updatePromise = result.current.updateCreditCardTransaction(
        "tx-1",
        {
          title: "Mercado do bairro",
          merchant: "Mercado",
          amount: 300,
          purchasedOn: "2026-07-06",
          notes: "",
          categoryId: "category-1",
          coreId: "core-1",
          projectId: "project-1",
          externalSource: "SMS",
          externalReference: "sms-1",
        },
        { silentSuccess: true },
      );
      await Promise.resolve();
    });

    expect(result.current.creditCardTransactions).toEqual([
      expect.objectContaining({ id: "tx-1", amount: 300, title: "Mercado do bairro" }),
    ]);
    expect(result.current.creditCardAccounts).toEqual([
      expect.objectContaining({ id: "card-1", openTransactionTotal: 300 }),
    ]);
    expect(result.current.periodDetail?.summary.analyticalExpenseTotal).toBe(1000);
    expect(result.current.syncingSections.cardTransactions).toBe(true);

    await act(async () => {
      transactionUpdated = true;
      updateDeferred.resolve(buildTransaction({ amount: 300, title: "Mercado do bairro" }));
      await updatePromise;
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(result.current.creditCardTransactions).toEqual([
      expect.objectContaining({ id: "tx-1", amount: 300, title: "Mercado do bairro" }),
    ]);
    expect(result.current.creditCardAccounts).toEqual([
      expect.objectContaining({ id: "card-1", openTransactionTotal: 300 }),
    ]);
    expect(result.current.syncingSections.cardTransactions).toBe(false);

    unmount();
  });

  it("imports card transactions through the batch endpoint and refreshes the workspace", async () => {
    const session = buildSession();
    mockedReadSession.mockReturnValue(session);
    mockedSubscribeToSessionChanges.mockReturnValue(() => undefined);

    mockedApiFetch.mockImplementation(async (path: string, options?: RequestInit & { spaceId?: string }) => {
      if (path === "/api/spaces/members") {
        return [];
      }

      if (path === "/api/cores") {
        return [{ id: "core-1", name: "Espaço" }];
      }

      if (path === "/api/projects") {
        return [{ id: "project-1", coreId: "core-1", coreName: "Espaço", name: "Moradia" }];
      }

      if (path === "/api/finance/categories") {
        return buildCategories();
      }

      if (path === "/api/finance/periods") {
        return [{ id: "period-1", year: 2026, month: 7, totalIncome: 5000, totalExpense: 700, cashBalance: 4300, entryCount: 1 }];
      }

      if (path === "/api/finance/periods/2026/7") {
        return buildPeriodDetail({ cardTransactions: [buildTransaction()] });
      }

      if (path === "/api/finance/recurring-templates" || path === "/api/finance/assets") {
        return [];
      }

      if (path === "/api/finance/credit-cards") {
        return [buildCard()];
      }

      if (path === "/api/finance/credit-cards/card-1/transactions") {
        return [buildTransaction()];
      }

      if (path === "/api/finance/credit-cards/card-1/statements") {
        return [];
      }

      if (path === "/api/finance/credit-cards/card-1/transactions/import" && options?.method === "POST") {
        return {
          totalCount: 1,
          totalAmount: 220.9,
          createdCategoryCount: 1,
          createdTransactions: [buildTransaction()],
        };
      }

      throw new Error(`Unexpected API path: ${path}`);
    });

    const { result } = renderHook(() => useFinanceDashboard());

    await flushDashboardEffects();
    await flushDashboardEffects();

    await act(async () => {
      await result.current.importCreditCardTransactions([
        {
          title: "Supermercado",
          merchant: "Mercado",
          amount: 220.9,
          purchasedOn: "2026-07-06",
          notes: null,
          categoryName: "Mercado",
          coreName: "Espaço",
          projectName: "Moradia",
          externalSource: "JSON",
          externalReference: "json-1",
          importedAt: null,
        },
      ]);
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(mockedApiFetch).toHaveBeenCalledWith(
      "/api/finance/credit-cards/card-1/transactions/import",
      expect.objectContaining({
        method: "POST",
        spaceId: "space-1",
      }),
    );
    expect(mockedToast.success).toHaveBeenCalledWith("1 compra importada no cartão.");
    expect(result.current.creditCardTransactions).toEqual([expect.objectContaining({ id: "tx-1", title: "Supermercado" })]);
  });
});

import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type {
  Asset,
  CreditCardAccount,
  CreditCardStatement,
  CreditCardTransaction,
  FinanceCategory,
  FinanceEntry,
  FinancePeriodDetail,
  FinanceRecurringTemplate,
} from "@/lib/api";
import type { FinanceDashboardController } from "./use-finance-dashboard";
import { FinanceDashboardWorkspace } from "./finance-dashboard-workspace";

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
    universeId: null,
    universeName: null,
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
      totalExpense: 900,
      cashBalance: 4100,
      analyticalExpenseTotal: 1120.9,
      verifiedEntries: 1,
      pendingVerificationEntries: 1,
      cardPurchaseCount: 1,
    },
    entries: [
      buildEntry({
        id: "entry-1",
        title: "Condomínio",
        amount: 700,
        verified: true,
        categoryId: "category-1",
        categoryName: "Casa",
        universeId: "universe-1",
        universeName: "Casa",
        projectId: "project-1",
        projectName: "Moradia",
      }),
      buildEntry({
        id: "entry-2",
        title: "Fatura Nubank",
        amount: 220.9,
        origin: "CreditCardStatement",
        projectId: "project-2",
        projectName: "Viagem",
      }),
    ],
    cardTransactions: [
      {
        id: "tx-1",
        creditCardAccountId: "card-1",
        creditCardAccountName: "Nubank",
        creditCardStatementId: "statement-1",
        title: "Supermercado",
        merchant: "Mercado",
        amount: 220.9,
        purchasedOn: "2026-07-06",
        notes: null,
        categoryId: "category-2",
        categoryName: "Mercado",
        universeId: "universe-1",
        universeName: "Casa",
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
      },
    ],
    statements: [
      {
        id: "statement-1",
        creditCardAccountId: "card-1",
        creditCardAccountName: "Nubank",
        closingDate: "2026-07-20",
        dueDate: "2026-07-25",
        totalAmount: 220.9,
        notes: "Fatura do mês",
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
      },
    ],
    ...overrides,
  };
}

function createDashboard(overrides: Partial<FinanceDashboardController> = {}): FinanceDashboardController {
  const periodDetail = overrides.periodDetail ?? buildPeriodDetail();
  const entries = periodDetail?.entries ?? [];
  const cardTransactions = periodDetail?.cardTransactions ?? [];
  const creditCardStatements = periodDetail?.statements ?? [];
  const creditCardAccounts: CreditCardAccount[] =
    overrides.creditCardAccounts ?? [
      {
        id: "card-1",
        name: "Nubank",
        brand: "Mastercard",
        lastFourDigits: "1234",
        closingDay: 20,
        dueDay: 25,
        notes: null,
        isActive: true,
        openTransactionCount: 0,
        openTransactionTotal: 0,
        createdByMemberId: "member-1",
        createdAt: "2026-07-01T12:00:00.000Z",
        updatedAt: "2026-07-01T12:00:00.000Z",
        canEdit: true,
        canDelete: true,
      },
    ];

  const categories: FinanceCategory[] =
    overrides.categories ?? [
      {
        id: "category-1",
        name: "Casa",
        isDefault: true,
        sortOrder: 1,
        createdByMemberId: "member-1",
        usageCount: 1,
        canEdit: false,
        canDelete: false,
      },
      {
        id: "category-2",
        name: "Mercado",
        isDefault: false,
        sortOrder: 20,
        createdByMemberId: "member-1",
        usageCount: 2,
        canEdit: true,
        canDelete: true,
      },
    ];

  return {
    session: {
      accessToken: "token",
      refreshToken: "refresh",
      expiresAt: "2026-07-06T12:00:00.000Z",
      user: {
        id: "user-1",
        email: "owner@homepit.dev",
        displayName: "Owner",
        systemRole: "User",
        hasProfilePhoto: false,
        profilePhotoUpdatedAt: null,
        phoneNumber: null,
      },
      households: [{ id: "house-1", name: "Casa Financeira", role: "Owner" }],
    },
    activeHouseholdId: "house-1",
    activeHousehold: { id: "house-1", name: "Casa Financeira", role: "Owner" },
    members: [],
    universes: [{ id: "universe-1", name: "Casa", imageUrl: null, hasImage: false, imageUpdatedAt: null, createdByMemberId: "member-1", projectCount: 1, canEdit: true, canDelete: true }],
    projects: [{ id: "project-1", universeId: "universe-1", universeName: "Casa", universeImageUrl: null, universeHasImage: false, universeImageUpdatedAt: null, name: "Moradia", createdByMemberId: "member-1", activityCount: 0, canEdit: true, canDelete: true }, { id: "project-2", universeId: "universe-1", universeName: "Casa", universeImageUrl: null, universeHasImage: false, universeImageUpdatedAt: null, name: "Viagem", createdByMemberId: "member-1", activityCount: 0, canEdit: true, canDelete: true }],
    categories,
    financePeriods: [{ id: "period-1", year: 2026, month: 7, totalIncome: 5000, totalExpense: 900, cashBalance: 4100, entryCount: entries.length }],
    activeYear: 2026,
    activeMonth: 7,
    periodDetail,
    recurringTemplates: overrides.recurringTemplates ?? [],
    assets: overrides.assets ?? [],
    assetValuations: {},
    assetValuationsLoadingFor: null,
    creditCardAccounts,
    selectedCreditCardId: creditCardAccounts[0]?.id ?? "",
    creditCardTransactions: overrides.creditCardTransactions ?? cardTransactions,
    creditCardStatements: overrides.creditCardStatements ?? creditCardStatements,
    cardDetailsLoading: false,
    editingHousehold: null,
    isHouseholdDialogOpen: false,
    isShareDialogOpen: false,
    sidebarCollapsed: false,
    theme: "earthy",
    loading: false,
    error: null,
    subtitle: "Fluxo mensal, recorrências, cartões e patrimônio da casa",
    canShareHousehold: true,
    canManageHousehold: true,
    setError: () => undefined,
    setSidebarCollapsed: () => undefined,
    setTheme: () => undefined,
    setActivePeriod: () => undefined,
    setSelectedCreditCardId: () => undefined,
    handleAuthenticated: () => undefined,
    handleHouseholdChange: () => undefined,
    handleLogout: () => undefined,
    refreshHouseholds: async () => undefined,
    refreshWorkspace: async () => undefined,
    openCreateHousehold: () => undefined,
    openEditHousehold: () => undefined,
    openShareHousehold: () => undefined,
    closeCommonModal: () => undefined,
    createHousehold: async () => undefined,
    updateHousehold: async () => undefined,
    deleteHousehold: async () => undefined,
    shareHousehold: async () => undefined,
    updateProfile: async () => undefined,
    generatePeriod: vi.fn(async () => undefined),
    createCategory: vi.fn(async () => undefined),
    updateCategory: vi.fn(async () => undefined),
    deleteCategory: vi.fn(async () => undefined),
    createEntry: vi.fn(async () => undefined),
    updateEntry: vi.fn(async () => undefined),
    toggleEntryVerified: vi.fn(async () => undefined),
    deleteEntry: vi.fn(async () => undefined),
    createRecurringTemplate: vi.fn(async () => undefined),
    updateRecurringTemplate: vi.fn(async () => undefined),
    deleteRecurringTemplate: vi.fn(async () => undefined),
    createAsset: vi.fn(async () => undefined),
    updateAsset: vi.fn(async () => undefined),
    deleteAsset: vi.fn(async () => undefined),
    loadAssetValuations: vi.fn(async () => undefined),
    createAssetValuation: vi.fn(async () => undefined),
    updateAssetValuation: vi.fn(async () => undefined),
    deleteAssetValuation: vi.fn(async () => undefined),
    createCreditCardAccount: vi.fn(async () => undefined),
    updateCreditCardAccount: vi.fn(async () => undefined),
    deleteCreditCardAccount: vi.fn(async () => undefined),
    createCreditCardTransaction: vi.fn(async () => undefined),
    updateCreditCardTransaction: vi.fn(async () => undefined),
    deleteCreditCardTransaction: vi.fn(async () => undefined),
    createCreditCardStatement: vi.fn(async () => undefined),
    updateCreditCardStatement: vi.fn(async () => undefined),
    deleteCreditCardStatement: vi.fn(async () => undefined),
    ...overrides,
  };
}

describe("FinanceDashboardWorkspace", () => {
  afterEach(() => {
    cleanup();
  });

  it("shows the generate dialog for an existing period and adds missing recurring entries", async () => {
    const dashboard = createDashboard();

    render(<FinanceDashboardWorkspace dashboard={dashboard} />);

    fireEvent.click(screen.getByRole("button", { name: "Inserir Recorrências" }));
    fireEvent.click(await screen.findByRole("button", { name: "Adicionar faltantes" }));

    expect(dashboard.generatePeriod).toHaveBeenCalledWith("missingOnly");
  });

  it("shows the generate dialog for an existing period and duplicates all recurring entries", async () => {
    const dashboard = createDashboard();

    render(<FinanceDashboardWorkspace dashboard={dashboard} />);

    fireEvent.click(screen.getAllByRole("button", { name: "Inserir Recorrências" })[0]!);
    fireEvent.click(await screen.findByRole("button", { name: "Duplicar todos" }));

    await waitFor(() => {
      expect(dashboard.generatePeriod).toHaveBeenCalledWith("duplicateAll");
    });
  });

  it("renders the categories section and opens the creation dialog", async () => {
    const dashboard = createDashboard();

    render(<FinanceDashboardWorkspace dashboard={dashboard} />);

    expect(screen.getByText("Categorias")).toBeInTheDocument();
    expect(screen.getAllByText("Casa").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Mercado").length).toBeGreaterThan(0);

    fireEvent.click(screen.getByRole("button", { name: "Nova categoria" }));

    expect(await screen.findByRole("dialog", { name: "Nova categoria" })).toBeInTheDocument();
  });

  it("opens the recurring templates modal from the toolbar", async () => {
    const dashboard = createDashboard({
      recurringTemplates: [
        {
          id: "template-1",
          title: "Aluguel",
          notes: "Recorrência mensal",
          type: "Saida",
          defaultAmount: 1500,
          recurrence: "Monthly",
          dayOfMonth: 5,
          monthOfYear: null,
          isActive: true,
          universeId: null,
          universeName: null,
          projectId: null,
          projectName: null,
          createdByMemberId: "member-1",
          createdAt: "2026-07-06T12:00:00.000Z",
          updatedAt: "2026-07-06T12:00:00.000Z",
          canEdit: true,
          canDelete: true,
        },
      ],
    });

    render(<FinanceDashboardWorkspace dashboard={dashboard} />);

    fireEvent.click(screen.getByRole("button", { name: "Recorrências" }));

    expect(await screen.findByRole("dialog", { name: "Recorrências" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Nova recorrência" })).toBeInTheDocument();
    expect(screen.getByText("Aluguel")).toBeInTheDocument();
  });

  it("keeps the recurring templates modal open when creating a recurring template", async () => {
    const dashboard = createDashboard({
      recurringTemplates: [
        {
          id: "template-1",
          title: "Aluguel",
          notes: "Recorrência mensal",
          type: "Saida",
          defaultAmount: 1500,
          recurrence: "Monthly",
          dayOfMonth: 5,
          monthOfYear: null,
          isActive: true,
          universeId: null,
          universeName: null,
          projectId: null,
          projectName: null,
          createdByMemberId: "member-1",
          createdAt: "2026-07-06T12:00:00.000Z",
          updatedAt: "2026-07-06T12:00:00.000Z",
          canEdit: true,
          canDelete: true,
        },
      ],
    });

    render(<FinanceDashboardWorkspace dashboard={dashboard} />);

    fireEvent.click(screen.getByRole("button", { name: "Recorrências" }));
    fireEvent.click(await screen.findByRole("button", { name: "Nova recorrência" }));

    expect(await screen.findByRole("dialog", { name: "Recorrências" })).toBeInTheDocument();
    expect(await screen.findByRole("dialog", { name: "Nova recorrência" })).toBeInTheDocument();
  });

  it("toggles verification and applies filters/grouping in the cash section", async () => {
    const dashboard = createDashboard();

    render(<FinanceDashboardWorkspace dashboard={dashboard} />);

    const condominiumRow = screen.getAllByRole("row").find((row) => row.textContent?.includes("Condomínio"));
    expect(condominiumRow).not.toBeNull();
    fireEvent.click(within(condominiumRow!).getByRole("checkbox"));
    await waitFor(() => {
      expect(dashboard.toggleEntryVerified).toHaveBeenCalledWith(
        expect.objectContaining({ id: "entry-1", title: "Condomínio" }),
      );
    });

    fireEvent.change(screen.getByDisplayValue("Tipo"), { target: { value: "project" } });
    expect(screen.getByText("Moradia", { selector: ".text-base" })).toBeInTheDocument();
    expect(screen.getByText("Viagem", { selector: ".text-base" })).toBeInTheDocument();

    fireEvent.change(screen.getByPlaceholderText("Buscar lançamento"), { target: { value: "nubank" } });
    expect(screen.queryByText("Condomínio")).not.toBeInTheDocument();
    expect(screen.getByText("Fatura Nubank")).toBeInTheDocument();
  });

  it("switches between typed asset forms and submits property details", async () => {
    const dashboard = createDashboard();

    render(<FinanceDashboardWorkspace dashboard={dashboard} />);

    fireEvent.click(screen.getAllByRole("button", { name: "Novo bem" })[0]!);
    fireEvent.change(screen.getByLabelText("Título"), { target: { value: "Blue Moon Apto 405" } });

    fireEvent.change(screen.getByLabelText("Tipo"), { target: { value: "Vehicle" } });
    expect(screen.getByLabelText("Marca")).toBeInTheDocument();
    expect(screen.queryByLabelText("Matrícula")).not.toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Tipo"), { target: { value: "Property" } });
    expect(screen.getByLabelText("Matrícula")).toBeInTheDocument();
    expect(screen.queryByLabelText("Marca")).not.toBeInTheDocument();

    const currencyInputs = screen.getAllByPlaceholderText("R$ 0,00");
    fireEvent.change(currencyInputs[0]!, { target: { value: "R$ 480.000,00" } });
    fireEvent.change(currencyInputs[1]!, { target: { value: "R$ 55.474,71" } });
    fireEvent.change(screen.getByLabelText("Matrícula"), { target: { value: "282.144" } });
    fireEvent.change(screen.getByLabelText("Inscrição"), { target: { value: "50760572" } });
    fireEvent.change(screen.getByLabelText("Área privativa (m²)"), { target: { value: "55.61" } });
    fireEvent.change(screen.getByLabelText("Pesquisa débito"), { target: { value: "2023-12-29" } });
    fireEvent.click(screen.getByRole("button", { name: "Salvar bem" }));

    await waitFor(() => {
      expect(dashboard.createAsset).toHaveBeenCalledWith({
        title: "Blue Moon Apto 405",
        type: "Property",
        currentValue: 480000,
        remainingDebt: 55474.71,
        isPaidOff: false,
        notes: "",
        propertyDetails: {
          registryNumber: "282.144",
          propertyInscription: "50760572",
          privateAreaSquareMeters: 55.61,
          debtCheckOn: "2023-12-29",
        },
        vehicleDetails: null,
      });
    });
  });
});

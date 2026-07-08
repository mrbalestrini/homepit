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
    syncingSections: {
      cash: false,
      categories: false,
      recurringTemplates: false,
      cardTransactions: false,
      cardStatements: false,
      assetValuations: false,
    },
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
    deleteEntries: vi.fn(async () => undefined),
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
    importCreditCardTransactions: vi.fn(async () => ({
      totalCount: 1,
      totalAmount: 220.9,
      createdCategoryCount: 0,
      createdTransactions: [],
    })),
    updateCreditCardTransaction: vi.fn(async () => undefined),
    deleteCreditCardTransaction: vi.fn(async () => undefined),
    deleteCreditCardTransactions: vi.fn(async () => undefined),
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

  it("opens the categories modal from the toolbar and allows starting a new category", async () => {
    const dashboard = createDashboard();

    render(<FinanceDashboardWorkspace dashboard={dashboard} />);

    fireEvent.click(screen.getByRole("button", { name: "Categorias" }));

    expect(await screen.findByRole("dialog", { name: "Categorias" })).toBeInTheDocument();
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

    expect(await screen.findByRole("dialog", { name: "Nova recorrência" })).toBeInTheDocument();
    expect(screen.getByText("Gerencie as recorrências mensais e anuais em uma janela dedicada quase em tela cheia.")).toBeInTheDocument();
  });

  it("keeps both recurring dialogs open when interacting with the nested editor", async () => {
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
    const recurringDialog = await screen.findByRole("dialog", { name: "Recorrências" });
    fireEvent.click(within(recurringDialog).getByRole("button", { name: "Editar" }));

    const editorDialog = await screen.findByRole("dialog", { name: "Editar recorrência" });
    const titleInput = within(editorDialog).getByRole("textbox", { name: "Título" });

    fireEvent.pointerDown(titleInput);
    fireEvent.click(titleInput);

    expect(screen.getByRole("dialog", { name: "Editar recorrência" })).toBeInTheDocument();
    expect(screen.getByText("Gerencie as recorrências mensais e anuais em uma janela dedicada quase em tela cheia.")).toBeInTheDocument();
  });

  it("opens on Caixa, switches to Cartões and preserves the cash panel state", async () => {
    const dashboard = createDashboard();

    render(<FinanceDashboardWorkspace dashboard={dashboard} />);

    const cashTab = screen.getByRole("tab", { name: "Caixa" });
    const cardsTab = screen.getByRole("tab", { name: "Cartões" });

    expect(cashTab).toHaveAttribute("aria-selected", "true");
    expect(cardsTab).toHaveAttribute("aria-selected", "false");
    expect(screen.getByRole("tabpanel", { name: "Caixa" })).toBeInTheDocument();
    expect(screen.queryByRole("tabpanel", { name: "Cartões" })).toBeNull();

    fireEvent.change(screen.getByPlaceholderText("Buscar lançamento"), { target: { value: "mercado" } });
    fireEvent.click(cardsTab);

    expect(screen.getByRole("tabpanel", { name: "Cartões" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Novo cartão" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Nova entrada" })).toBeNull();
    expect(screen.getByText("Patrimônio")).toBeInTheDocument();

    fireEvent.click(cashTab);

    expect(screen.getByRole("tabpanel", { name: "Caixa" })).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Buscar lançamento")).toHaveValue("mercado");
  });

  it("toggles verification and applies filters in the cash section", async () => {
    const dashboard = createDashboard();

    render(<FinanceDashboardWorkspace dashboard={dashboard} />);

    const condominiumRow = screen.getAllByRole("row").find((row) => row.textContent?.includes("Condomínio"));
    expect(condominiumRow).not.toBeNull();
    fireEvent.click(within(condominiumRow!).getByRole("button", { name: "Alternar verificação do lançamento Condomínio" }));
    await waitFor(() => {
      expect(dashboard.updateEntry).toHaveBeenCalledWith(
        "entry-1",
        expect.objectContaining({ verified: false }),
        { silentSuccess: true },
      );
    });

    fireEvent.change(screen.getByPlaceholderText("Buscar lançamento"), { target: { value: "nubank" } });
    await waitFor(() => {
      expect(screen.getByText("Fatura Nubank")).toBeInTheDocument();
    });
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

  it("edits the cash title inline and still keeps the modal edit path", async () => {
    const dashboard = createDashboard();

    render(<FinanceDashboardWorkspace dashboard={dashboard} />);

    const condominiumRow = screen.getAllByRole("row").find((row) => row.textContent?.includes("Condomínio"));
    expect(condominiumRow).not.toBeNull();

    fireEvent.click(within(condominiumRow!).getByRole("button", { name: "Editar título do lançamento Condomínio" }));
    const titleInput = within(condominiumRow!).getByRole("textbox", { name: "Editar título do lançamento Condomínio" });
    fireEvent.change(titleInput, { target: { value: "Condomínio atualizado" } });
    fireEvent.keyDown(titleInput, { key: "Enter" });

    await waitFor(() => {
      expect(dashboard.updateEntry).toHaveBeenCalledWith(
        "entry-1",
        expect.objectContaining({ title: "Condomínio atualizado" }),
        { silentSuccess: true },
      );
    });

    await waitFor(() => {
      expect(within(condominiumRow!).getByRole("button", { name: "Editar" })).toBeEnabled();
    });
    fireEvent.click(within(condominiumRow!).getByRole("button", { name: "Editar" }));
    expect(await screen.findByRole("dialog", { name: "Editar lançamento" })).toBeInTheDocument();
  });

  it("edits a custom category inline and keeps default categories read-only", async () => {
    const dashboard = createDashboard();

    render(<FinanceDashboardWorkspace dashboard={dashboard} />);

    fireEvent.click(screen.getByRole("button", { name: "Categorias" }));
    const dialog = await screen.findByRole("dialog", { name: "Categorias" });

    const customRow = within(dialog)
      .getAllByRole("row")
      .find((row) => row.textContent?.includes("Mercado"));
    expect(customRow).not.toBeNull();

    fireEvent.click(within(customRow!).getByRole("button", { name: "Editar nome da categoria Mercado" }));
    const customInput = within(customRow!).getByRole("textbox", { name: "Editar nome da categoria Mercado" });
    fireEvent.change(customInput, { target: { value: "Feira" } });
    fireEvent.keyDown(customInput, { key: "Enter" });

    await waitFor(() => {
      expect(dashboard.updateCategory).toHaveBeenCalledWith("category-2", { name: "Feira" }, { silentSuccess: true });
    });

    const defaultRow = within(dialog)
      .getAllByRole("row")
      .find((row) => row.textContent?.includes("Casa"));
    expect(defaultRow).not.toBeNull();
    expect(within(defaultRow!).queryByRole("button", { name: "Editar nome da categoria Casa" })).toBeNull();
  });

  it("confirms entry deletion without requiring typed confirmation", async () => {
    const dashboard = createDashboard();

    render(<FinanceDashboardWorkspace dashboard={dashboard} />);

    const condominiumRow = screen.getAllByRole("row").find((row) => row.textContent?.includes("Condomínio"));
    expect(condominiumRow).not.toBeNull();

    fireEvent.click(within(condominiumRow!).getByRole("button", { name: "Excluir" }));

    const dialog = await screen.findByRole("dialog", { name: "Excluir registro" });
    expect(within(dialog).queryByLabelText(/Digite/i)).toBeNull();

    fireEvent.click(within(dialog).getByRole("button", { name: "Excluir" }));

    await waitFor(() => {
      expect(dashboard.deleteEntry).toHaveBeenCalledWith("entry-1");
    });
  });

  it("allows bulk deletion of selected cash entries", async () => {
    const dashboard = createDashboard({
      periodDetail: buildPeriodDetail({
        entries: [
          buildEntry({ id: "entry-1", title: "Condomínio", amount: 700 }),
          buildEntry({ id: "entry-2", title: "Mercado", amount: 250 }),
        ],
        cardTransactions: [],
        statements: [],
      }),
    });

    render(<FinanceDashboardWorkspace dashboard={dashboard} />);

    fireEvent.click(screen.getByRole("checkbox", { name: "Selecionar lançamento Condomínio" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "Selecionar lançamento Mercado" }));
    fireEvent.click(screen.getByRole("button", { name: "Excluir selecionados (2)" }));

    const dialog = await screen.findByRole("dialog", { name: "Excluir registros" });
    fireEvent.click(within(dialog).getByRole("button", { name: "Excluir" }));

    await waitFor(() => {
      expect(dashboard.deleteEntries).toHaveBeenCalledWith(["entry-1", "entry-2"]);
    });
  });

  it("allows bulk deletion of selected card purchases", async () => {
    const dashboard = createDashboard({
      periodDetail: buildPeriodDetail({
        cardTransactions: [
          {
            id: "tx-1",
            creditCardAccountId: "card-1",
            creditCardAccountName: "Nubank",
            creditCardStatementId: null,
            title: "Supermercado",
            merchant: "Mercado",
            amount: 220.9,
            purchasedOn: "2026-07-06",
            notes: null,
            categoryId: null,
            categoryName: null,
            universeId: null,
            universeName: null,
            projectId: null,
            projectName: null,
            externalSource: null,
            externalReference: null,
            importedAt: null,
            createdByMemberId: "member-1",
            createdAt: "2026-07-06T12:00:00.000Z",
            updatedAt: "2026-07-06T12:00:00.000Z",
            canEdit: true,
            canDelete: true,
          },
          {
            id: "tx-2",
            creditCardAccountId: "card-1",
            creditCardAccountName: "Nubank",
            creditCardStatementId: null,
            title: "Farmácia",
            merchant: "Drogaria",
            amount: 80,
            purchasedOn: "2026-07-07",
            notes: null,
            categoryId: null,
            categoryName: null,
            universeId: null,
            universeName: null,
            projectId: null,
            projectName: null,
            externalSource: null,
            externalReference: null,
            importedAt: null,
            createdByMemberId: "member-1",
            createdAt: "2026-07-07T12:00:00.000Z",
            updatedAt: "2026-07-07T12:00:00.000Z",
            canEdit: true,
            canDelete: true,
          },
        ],
      }),
      creditCardTransactions: [
        {
          id: "tx-1",
          creditCardAccountId: "card-1",
          creditCardAccountName: "Nubank",
          creditCardStatementId: null,
          title: "Supermercado",
          merchant: "Mercado",
          amount: 220.9,
          purchasedOn: "2026-07-06",
          notes: null,
          categoryId: null,
          categoryName: null,
          universeId: null,
          universeName: null,
          projectId: null,
          projectName: null,
          externalSource: null,
          externalReference: null,
          importedAt: null,
          createdByMemberId: "member-1",
          createdAt: "2026-07-06T12:00:00.000Z",
          updatedAt: "2026-07-06T12:00:00.000Z",
          canEdit: true,
          canDelete: true,
        },
        {
          id: "tx-2",
          creditCardAccountId: "card-1",
          creditCardAccountName: "Nubank",
          creditCardStatementId: null,
          title: "Farmácia",
          merchant: "Drogaria",
          amount: 80,
          purchasedOn: "2026-07-07",
          notes: null,
          categoryId: null,
          categoryName: null,
          universeId: null,
          universeName: null,
          projectId: null,
          projectName: null,
          externalSource: null,
          externalReference: null,
          importedAt: null,
          createdByMemberId: "member-1",
          createdAt: "2026-07-07T12:00:00.000Z",
          updatedAt: "2026-07-07T12:00:00.000Z",
          canEdit: true,
          canDelete: true,
        },
      ],
    });

    render(<FinanceDashboardWorkspace dashboard={dashboard} />);

    fireEvent.click(screen.getByRole("tab", { name: "Cartões" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "Selecionar compra Supermercado" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "Selecionar compra Farmácia" }));
    fireEvent.click(screen.getByRole("button", { name: "Excluir selecionadas (2)" }));

    const dialog = await screen.findByRole("dialog", { name: "Excluir registros" });
    fireEvent.click(within(dialog).getByRole("button", { name: "Excluir" }));

    await waitFor(() => {
      expect(dashboard.deleteCreditCardTransactions).toHaveBeenCalledWith(["tx-1", "tx-2"]);
    });
  });

  it("rejects malformed JSON in the import dialog", async () => {
    const dashboard = createDashboard();

    render(<FinanceDashboardWorkspace dashboard={dashboard} />);

    fireEvent.click(screen.getByRole("tab", { name: "Cartões" }));
    fireEvent.click(screen.getByRole("button", { name: "Importar JSON" }));

    const dialog = await screen.findByRole("dialog", { name: "Importar compras por JSON" });
    const fileInput = within(dialog).getByLabelText("Arquivo JSON");
    fireEvent.change(fileInput, {
      target: {
        files: [new File(["{ invalid"], "compras.json", { type: "application/json" })],
      },
    });

    fireEvent.click(within(dialog).getByRole("button", { name: "Revisar importação" }));

    expect(await within(dialog).findByText("O arquivo não contém um JSON válido.")).toBeInTheDocument();
  });

  it("recalculates the import review summary in real time and confirms the batch", async () => {
    const dashboard = createDashboard();

    render(<FinanceDashboardWorkspace dashboard={dashboard} />);

    fireEvent.click(screen.getByRole("tab", { name: "Cartões" }));
    fireEvent.click(screen.getByRole("button", { name: "Importar JSON" }));

    const importDialog = await screen.findByRole("dialog", { name: "Importar compras por JSON" });
    const fileInput = within(importDialog).getByLabelText("Arquivo JSON");
    fireEvent.change(fileInput, {
      target: {
        files: [
          new File(
            [
              JSON.stringify({
                transactions: [
                  {
                    title: "Supermercado",
                    merchant: "Mercado",
                    amount: 10,
                    purchasedOn: "2026-07-06",
                    categoryName: "Mercado",
                    universeName: "Casa",
                    projectName: "Moradia",
                    externalSource: "JSON",
                    externalReference: "json-1",
                  },
                  {
                    title: "Farmácia",
                    merchant: "Drogaria",
                    amount: 20,
                    purchasedOn: "2026-07-07",
                    externalSource: "JSON",
                    externalReference: "json-2",
                  },
                ],
              }),
            ],
            "compras.json",
            { type: "application/json" },
          ),
        ],
      },
    });

    fireEvent.click(within(importDialog).getByRole("button", { name: "Revisar importação" }));

    const reviewDialog = await screen.findByRole("dialog", { name: "Revisar importação" });
    const confirmButton = within(reviewDialog).getByRole("button", { name: "Confirmar importação" });
    expect(within(reviewDialog).getByText("R$ 30,00")).toBeInTheDocument();

    fireEvent.change(within(reviewDialog).getByLabelText("Valor da linha 1"), { target: { value: "25" } });
    await waitFor(() => {
      expect(within(reviewDialog).getByText("R$ 45,00")).toBeInTheDocument();
    });

    fireEvent.click(within(reviewDialog).getByRole("button", { name: "Adicionar linha" }));
    fireEvent.change(within(reviewDialog).getByLabelText("Título da linha 3"), { target: { value: "Streaming" } });
    fireEvent.change(within(reviewDialog).getByLabelText("Valor da linha 3"), { target: { value: "15" } });
    fireEvent.change(within(reviewDialog).getByLabelText("Data da compra da linha 3"), { target: { value: "2026-07-08" } });
    await waitFor(() => {
      expect(within(reviewDialog).getByText("R$ 60,00")).toBeInTheDocument();
    });

    fireEvent.click(within(reviewDialog).getAllByRole("button", { name: "Remover" })[1]!);
    await waitFor(() => {
      expect(within(reviewDialog).getByText("R$ 40,00")).toBeInTheDocument();
    });

    fireEvent.click(within(reviewDialog).getByRole("button", { name: "Confirmar importação" }));

    await waitFor(() => {
      expect(dashboard.importCreditCardTransactions).toHaveBeenCalledWith([
        {
          title: "Supermercado",
          merchant: "Mercado",
          amount: 25,
          purchasedOn: "2026-07-06",
          notes: null,
          categoryName: "Mercado",
          universeName: "Casa",
          projectName: "Moradia",
          externalSource: "JSON",
          externalReference: "json-1",
          importedAt: null,
        },
        {
          title: "Streaming",
          merchant: null,
          amount: 15,
          purchasedOn: "2026-07-08",
          notes: null,
          categoryName: null,
          universeName: null,
          projectName: null,
          externalSource: null,
          externalReference: null,
          importedAt: null,
        },
      ]);
    });
  });
});

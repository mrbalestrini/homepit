import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { GsmDashboardWorkspace, GsmNumberDialog } from "./gsm-dashboard-workspace";

describe("GsmNumberDialog", () => {
  it("masks the number field and submits the gsm payload", async () => {
    const onSave = vi.fn(async () => undefined);

    render(
      <GsmNumberDialog
        open
        gsmNumber={null}
        onOpenChange={() => undefined}
        onSave={onSave}
      />,
    );

    fireEvent.change(screen.getByLabelText("Título"), { target: { value: "Chip do alarme" } });
    fireEvent.change(screen.getByPlaceholderText("(11) 91234-5678 ou +55 (11) 91234-5678"), {
      target: { value: "11912345678" },
    });
    fireEvent.change(screen.getByLabelText("Plano"), { target: { value: "PosPago" } });
    fireEvent.change(screen.getByPlaceholderText("Ex.: R$ 59,90"), { target: { value: "R$ 59,90" } });
    fireEvent.change(screen.getByPlaceholderText("Ex.: 30"), { target: { value: "30" } });
    fireEvent.change(screen.getByLabelText("Data de aquisição"), { target: { value: "2026-01-10" } });
    fireEvent.click(screen.getByRole("button", { name: "Cadastrar número" }));

    expect(
      screen.getByPlaceholderText("(11) 91234-5678 ou +55 (11) 91234-5678"),
    ).toHaveValue("(11) 91234-5678");
    expect(onSave).toHaveBeenCalledWith({
      title: "Chip do alarme",
      number: "(11) 91234-5678",
      description: "",
      plan: "PosPago",
      monthlyCost: 59.9,
      daysWithoutRecharge: 30,
      acquiredOn: "2026-01-10",
      status: "Ativo",
    });
  });
});

describe("GsmDashboardWorkspace", () => {
  function createDashboard(overrides: Record<string, unknown> = {}) {
    return {
      session: {
        accessToken: "token",
        refreshToken: "refresh",
        expiresAt: "2026-06-23T12:00:00Z",
        user: {
          id: "user-1",
          email: "owner@homepit.dev",
          displayName: "Owner",
          systemRole: "User",
          hasProfilePhoto: false,
        },
        households: [{ id: "house-1", name: "Casa GSM", role: "Owner" }],
      },
      activeHouseholdId: "house-1",
      activeHousehold: { id: "house-1", name: "Casa GSM", role: "Owner" },
      members: [],
      gsmNumbers: [],
      gsmRecharges: [],
      sidebarCollapsed: false,
      theme: "earthy",
      activeModal: null,
      editingHousehold: null,
      editingGsmNumber: null,
      editingGsmRecharge: null,
      selectedRechargeGsmNumber: null,
      gsmRechargesLoading: false,
      gsmRechargesError: null,
      loading: false,
      error: null,
      subtitle: "Gestão GSM",
      canShareHousehold: true,
      canManageHousehold: true,
      setError: () => undefined,
      setSidebarCollapsed: () => undefined,
      setTheme: () => undefined,
      handleAuthenticated: () => undefined,
      handleHouseholdChange: () => undefined,
      handleLogout: () => undefined,
      refreshHouseholds: async () => undefined,
      refreshWorkspace: async () => undefined,
      createHousehold: async () => undefined,
      updateHousehold: async () => undefined,
      deleteHousehold: async () => undefined,
      openCreateHousehold: () => undefined,
      openEditHousehold: () => undefined,
      openShareHousehold: () => undefined,
      closeCommonModal: () => undefined,
      shareHousehold: async () => undefined,
      openCreateGsmNumber: vi.fn(),
      openEditGsmNumber: vi.fn(),
      closeModuleModal: () => undefined,
      openRechargeHistory: vi.fn(),
      closeRechargeHistory: () => undefined,
      openCreateRecharge: vi.fn(),
      openEditRecharge: vi.fn(),
      closeRechargeModal: () => undefined,
      refreshRechargeHistory: vi.fn(async () => undefined),
      createGsmNumber: async () => undefined,
      updateGsmNumber: async () => undefined,
      deleteGsmNumber: vi.fn(async () => undefined),
      createRecharge: async () => undefined,
      updateRecharge: async () => undefined,
      deleteRecharge: vi.fn(async () => undefined),
      ...overrides,
    };
  }

  it("shows the empty state action for the first gsm number", () => {
    const dashboard = createDashboard();

    render(<GsmDashboardWorkspace dashboard={dashboard as never} />);

    fireEvent.click(screen.getByRole("button", { name: "Cadastrar primeiro número" }));
    expect(dashboard.openCreateGsmNumber).toHaveBeenCalled();
  });

  it("renders gsm cards and opens the recharge action", () => {
    const dashboard = createDashboard({
      gsmNumbers: [
        {
          id: "gsm-1",
          title: "Linha da portaria",
          number: "5511912345678",
          description: "Uso comum",
          plan: "PrePago",
          monthlyCost: 42.5,
          acquiredOn: "2026-01-10",
          lastRechargeOn: "2026-06-20",
          daysWithoutRecharge: 30,
          status: "Ativo",
          createdByMemberId: "member-1",
          createdAt: "2026-01-10T00:00:00Z",
          updatedAt: "2026-06-20T00:00:00Z",
          canEdit: true,
          canDelete: true,
        },
      ],
    });

    render(<GsmDashboardWorkspace dashboard={dashboard as never} />);

    expect(screen.getAllByText("Próxima recarga").length).toBeGreaterThan(0);
    fireEvent.click(screen.getAllByRole("button", { name: "Informar recarga" })[0]);
    expect(dashboard.openCreateRecharge).toHaveBeenCalledWith(expect.objectContaining({ id: "gsm-1" }));
  });

  it("shows recharge history with edit and delete actions", async () => {
    const dashboard = createDashboard({
      activeModal: "recharge-history",
      selectedRechargeGsmNumber: {
        id: "gsm-1",
        title: "Linha da portaria",
        number: "5511912345678",
        description: "Uso comum",
        plan: "PrePago",
        monthlyCost: 42.5,
        daysWithoutRecharge: 30,
        acquiredOn: "2026-01-10",
        lastRechargeOn: "2026-06-20",
        status: "Ativo",
        createdByMemberId: "member-1",
        createdAt: "2026-01-10T00:00:00Z",
        updatedAt: "2026-06-20T00:00:00Z",
        canEdit: true,
        canDelete: true,
      },
      gsmRecharges: [
        {
          id: "recharge-1",
          gsmNumberId: "gsm-1",
          rechargedOn: "2026-06-20",
          amount: 50,
          note: "Recarga do mês",
          createdByMemberId: "member-1",
          createdAt: "2026-06-20T00:00:00Z",
          updatedAt: "2026-06-20T00:00:00Z",
          canEdit: true,
          canDelete: true,
        },
      ],
      openEditRecharge: vi.fn(),
      deleteRecharge: vi.fn(async () => undefined),
    });

    render(<GsmDashboardWorkspace dashboard={dashboard as never} />);

    expect(screen.getByText("Histórico de recargas")).toBeInTheDocument();
    expect(screen.getAllByText("20/06/2026").length).toBeGreaterThan(0);
    expect(screen.getByText("R$ 50,00")).toBeInTheDocument();
    expect(screen.getByText("Recarga do mês")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Editar" }));
    expect(dashboard.openEditRecharge).toHaveBeenCalledWith(
      expect.objectContaining({ id: "gsm-1" }),
      expect.objectContaining({ id: "recharge-1" }),
    );

    fireEvent.click(screen.getByRole("button", { name: "Excluir" }));
    const confirmButton = await screen.findByRole("button", { name: "Excluir recarga" });
    fireEvent.click(confirmButton);
    expect(dashboard.deleteRecharge).toHaveBeenCalledWith(expect.objectContaining({ id: "recharge-1" }));
  });

  it("renders gsm numbers in a card-based layout", () => {
    const dashboard = createDashboard({
      gsmNumbers: [
        {
          id: "gsm-1",
          title: "Linha da portaria",
          number: "5511912345678",
          description: "Uso comum",
          plan: "PrePago",
          monthlyCost: 42.5,
          acquiredOn: "2026-01-10",
          lastRechargeOn: "2026-06-20",
          daysWithoutRecharge: 30,
          status: "Ativo",
          createdByMemberId: "member-1",
          createdAt: "2026-01-10T00:00:00Z",
          updatedAt: "2026-06-20T00:00:00Z",
          canEdit: true,
          canDelete: true,
        },
      ],
    });

    render(<GsmDashboardWorkspace dashboard={dashboard as never} />);

    expect(screen.getAllByRole("table").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Linha da portaria").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Pré-pago").length).toBeGreaterThan(0);
    expect(screen.getAllByText("R$ 42,50").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Próxima recarga").length).toBeGreaterThan(0);
    expect(screen.getAllByRole("button", { name: "Histórico" }).length).toBeGreaterThan(0);
  });
});

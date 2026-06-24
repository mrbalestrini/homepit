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
    fireEvent.change(screen.getByLabelText("Número GSM"), { target: { value: "11912345678" } });
    fireEvent.change(screen.getByLabelText("Plano"), { target: { value: "PosPago" } });
    fireEvent.change(screen.getByLabelText("Custo mensal"), { target: { value: "R$ 59,90" } });
    fireEvent.change(screen.getByLabelText("Data de aquisição"), { target: { value: "2026-01-10" } });
    fireEvent.click(screen.getByRole("button", { name: "Cadastrar número" }));

    expect(screen.getByLabelText("Número GSM")).toHaveValue("(11) 91234-5678");
    expect(onSave).toHaveBeenCalledWith({
      title: "Chip do alarme",
      number: "(11) 91234-5678",
      description: "",
      plan: "PosPago",
      monthlyCost: 59.9,
      acquiredOn: "2026-01-10",
      lastRechargeOn: "",
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
      sidebarCollapsed: false,
      theme: "earthy",
      activeModal: null,
      editingHousehold: null,
      editingGsmNumber: null,
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
      updateProfile: async () => undefined,
      openCreateGsmNumber: vi.fn(),
      openEditGsmNumber: vi.fn(),
      closeModuleModal: () => undefined,
      createGsmNumber: async () => undefined,
      updateGsmNumber: async () => undefined,
      deleteGsmNumber: vi.fn(async () => undefined),
      ...overrides,
    };
  }

  it("shows the empty state action for the first gsm number", () => {
    const dashboard = createDashboard();

    render(<GsmDashboardWorkspace dashboard={dashboard as never} />);

    fireEvent.click(screen.getByRole("button", { name: "Cadastrar primeiro número" }));
    expect(dashboard.openCreateGsmNumber).toHaveBeenCalled();
  });

  it("requires typed confirmation before deleting a gsm number", async () => {
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

    fireEvent.click(screen.getAllByRole("button", { name: "Excluir" })[0]);
    const confirmButton = await screen.findByRole("button", { name: "Excluir número" });
    expect(confirmButton).toBeDisabled();

    fireEvent.change(screen.getByLabelText(/Digite o título/), { target: { value: "Linha da portaria" } });
    expect(confirmButton).not.toBeDisabled();

    fireEvent.click(confirmButton);
    expect(dashboard.deleteGsmNumber).toHaveBeenCalled();
  });

  it("renders gsm numbers in a table", () => {
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

    expect(screen.getByRole("table")).toBeInTheDocument();
    expect(screen.getByText("Linha da portaria")).toBeInTheDocument();
    expect(screen.getByText("Pré-pago")).toBeInTheDocument();
    expect(screen.getByText("R$ 42,50")).toBeInTheDocument();
  });
});

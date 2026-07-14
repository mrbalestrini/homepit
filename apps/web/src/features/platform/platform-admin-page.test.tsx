import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as api from "@/lib/api";
import { useProjectDashboard } from "@/features/projects/use-project-dashboard";
import { PlatformAdminPage } from "./platform-admin-page";

vi.mock("sonner", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api")>();
  return {
    ...actual,
    apiFetch: vi.fn(),
    clearSession: vi.fn(),
  };
});

vi.mock("@/features/projects/use-project-dashboard", () => ({
  useProjectDashboard: vi.fn(),
}));

vi.mock("@/features/workspace/account-state-gate", () => ({
  AccountStateGate: ({ children }: { children: ReactNode }) => <>{children}</>,
}));

vi.mock("@/features/workspace/homepit-auth", () => ({
  HomePitAuth: () => <div>auth</div>,
}));

vi.mock("@/features/workspace/homepit-workspace-shell", () => ({
  HomePitWorkspaceShell: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  Notice: ({ children }: { children: ReactNode }) => <div>{children}</div>,
}));

vi.mock("@/features/workspace/delete-confirmation-dialog", () => ({
  DeleteConfirmationDialog: () => null,
}));

const mockedApiFetch = vi.mocked(api.apiFetch);
const mockedUseProjectDashboard = vi.mocked(useProjectDashboard);

describe("PlatformAdminPage", () => {
  beforeEach(() => {
    cleanup();
    vi.clearAllMocks();
    window.localStorage.clear();
  });

  it("blocks users who are not SuperAdmin", async () => {
    mockedUseProjectDashboard.mockReturnValue(buildDashboard("Admin"));

    render(<PlatformAdminPage />);

    expect(await screen.findByText("Acesso restrito")).toBeInTheDocument();
    expect(mockedApiFetch).not.toHaveBeenCalled();
  });

  it("renders the hub tabs and persists plan edits", async () => {
    mockedUseProjectDashboard.mockReturnValue(buildDashboard("SuperAdmin"));
    mockedApiFetch.mockImplementation(async (path) => {
      if (path === "/api/admin/users") {
        return [
          {
            id: "user-1",
            email: "user@homepit.dev",
            displayName: "User",
            phoneNumber: null,
            systemRole: "User",
            accountState: "Active",
            scheduledDeletionAt: null,
            deactivatedAt: null,
            ownedHouseholdCount: 0,
            membershipCount: 1,
            isProtected: false,
            effectivePlanSlug: "free",
            effectivePlanName: "Free",
            activeSubscriptionStartsAt: null,
            activeSubscriptionEndsAt: null,
            activeSubscriptionBillingCycle: null,
            activeSubscriptionAmountPaid: null,
            activeSubscriptionCurrencyCode: null,
            activeSubscriptionStatus: null,
          },
        ];
      }

      if (path === "/api/admin/platform/plans") {
        return [
          {
            id: "plan-standard",
            slug: "standard",
            name: "Standard",
            currencyCode: "BRL",
            monthlyPrice: 9.9,
            annualPrice: 99,
            maxOwnedHouseholds: 1,
            maxUniverses: 3,
            maxProjects: 3,
            maxInvitedMembers: null,
            maxOriginalImages: 30,
            isPopular: false,
            imagePolicyDescription:
              "Mantém até 30 imagem(ns) privada(s) recente(s) em qualidade original; a partir da imagem 31, a mais antiga é substituída por WEBP com até 300 px e qualidade 30%.",
          },
        ];
      }

      if (path === "/api/admin/platform/subscriptions") {
        return [];
      }

      if (path === "/api/admin/platform/tool-improvement-suggestions") {
        return [];
      }

      if (path === "/api/admin/platform/settings") {
        return {
          adminName: "",
          contactEmail: "",
          contactPhone: "",
          managementPhone: "",
          instagram: "",
          addressLine1: "",
          addressLine2: "",
          city: "",
          state: "",
          postalCode: "",
          canShowAddressOnLanding: false,
        };
      }

      if (path === "/api/admin/platform/plans/plan-standard") {
        return {
          id: "plan-standard",
          slug: "standard",
          name: "Standard",
          currencyCode: "BRL",
          monthlyPrice: 11.9,
          annualPrice: 119,
          maxOwnedHouseholds: 1,
          maxUniverses: 3,
          maxProjects: 3,
          maxInvitedMembers: 5,
          maxOriginalImages: 30,
          isPopular: true,
          imagePolicyDescription:
            "Mantém até 30 imagem(ns) privada(s) recente(s) em qualidade original; a partir da imagem 31, a mais antiga é substituída por WEBP com até 300 px e qualidade 30%.",
        };
      }

      throw new Error(`Unexpected path: ${path}`);
    });

    render(<PlatformAdminPage />);

    expect(await screen.findByRole("tab", { name: "Usuários" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Planos" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Assinaturas" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Sugestões" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Configurações" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: "Planos" }));
    fireEvent.change(await screen.findByLabelText("Preço mensal"), { target: { value: "11.90" } });
    fireEvent.change(screen.getByLabelText("Membros convidados"), { target: { value: "5" } });
    fireEvent.click(screen.getByRole("button", { name: "Marcar Standard como popular" }));
    fireEvent.click(screen.getByRole("button", { name: "Salvar plano" }));

    await waitFor(() => {
      expect(mockedApiFetch).toHaveBeenCalledWith(
        "/api/admin/platform/plans/plan-standard",
        expect.objectContaining({
          method: "PUT",
          token: "access-token",
          body: expect.stringMatching(/"maxInvitedMembers":5.*"isPopular":true|"isPopular":true.*"maxInvitedMembers":5/),
        }),
      );
    });
  });

  it("renders and saves platform settings", async () => {
    mockedUseProjectDashboard.mockReturnValue(buildDashboard("SuperAdmin"));
    mockedApiFetch.mockImplementation(async (path, options) => {
      if (path === "/api/admin/users") {
        return [];
      }

      if (path === "/api/admin/platform/plans") {
        return [];
      }

      if (path === "/api/admin/platform/subscriptions") {
        return [];
      }

      if (path === "/api/admin/platform/tool-improvement-suggestions") {
        return [];
      }

      if (path === "/api/admin/platform/settings" && (!options || options.method === "GET")) {
        return {
          adminName: "",
          contactEmail: "",
          contactPhone: "",
          managementPhone: "",
          instagram: "",
          addressLine1: "",
          addressLine2: "",
          city: "",
          state: "",
          postalCode: "",
          canShowAddressOnLanding: false,
        };
      }

      if (path === "/api/admin/platform/settings" && options?.method === "PUT") {
        const body = JSON.parse(options.body as string) as Record<string, string>;
        return {
          ...body,
          canShowAddressOnLanding: true,
        };
      }

      throw new Error(`Unexpected path: ${path}`);
    });

    render(<PlatformAdminPage />);

    fireEvent.click(await screen.findByRole("tab", { name: "Configurações" }));

    fireEvent.change(await screen.findByLabelText("Nome administrador"), {
      target: { value: "Equipe HomePit" },
    });
    fireEvent.change(screen.getByLabelText("E-mail contato"), {
      target: { value: "contato@homepit.dev" },
    });
    fireEvent.change(screen.getByLabelText("Telefone contato"), {
      target: { value: "(11) 99999-0000" },
    });
    fireEvent.change(screen.getByLabelText("Telefone gestão"), {
      target: { value: "(11) 98888-7777" },
    });
    fireEvent.change(screen.getByLabelText("Instagram"), {
      target: { value: "@homepit" },
    });
    fireEvent.change(screen.getByLabelText("Endereço linha 1"), {
      target: { value: "Rua das Flores, 123" },
    });
    fireEvent.change(screen.getByLabelText("Endereço linha 2"), {
      target: { value: "Sala 21" },
    });
    fireEvent.change(screen.getByLabelText("Cidade"), {
      target: { value: "São Paulo" },
    });
    fireEvent.change(screen.getByLabelText("Estado"), {
      target: { value: "SP" },
    });
    fireEvent.change(screen.getByLabelText("CEP"), {
      target: { value: "01310-000" },
    });

    expect(screen.getByText("Endereço pronto para a landing")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Salvar configurações" }));

    await waitFor(() => {
      expect(mockedApiFetch).toHaveBeenCalledWith(
        "/api/admin/platform/settings",
        expect.objectContaining({
          method: "PUT",
          token: "access-token",
          body: expect.stringContaining('"adminName":"Equipe HomePit"'),
        }),
      );
    });
  });

  it("filters suggestions, restores the filter from localStorage and applies bulk updates", async () => {
    mockedUseProjectDashboard.mockReturnValue(buildDashboard("SuperAdmin"));
    window.localStorage.setItem(
      "homepit.platform.suggestion-filters",
      JSON.stringify({ search: "projetos", status: "NaoLido", priority: "Alta" }),
    );
    mockedApiFetch
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([
        {
          id: "suggestion-1",
          userId: "user-1",
          userDisplayName: "Paula",
          userEmail: "paula@homepit.dev",
          submittedAt: "2026-07-10T12:00:00Z",
          suggestionText: "Melhorar filtros de Projetos.",
          status: "NaoLido",
          priority: "Alta",
          internalComment: null,
          lastReviewedAt: null,
          lastReviewedByUserId: null,
          lastReviewedByDisplayName: null,
        },
        {
          id: "suggestion-2",
          userId: "user-2",
          userDisplayName: "Marcos",
          userEmail: "marcos@homepit.dev",
          submittedAt: "2026-07-10T13:00:00Z",
          suggestionText: "Revisar o módulo Financeiro.",
          status: "Feito",
          priority: "Media",
          internalComment: "Concluído",
          lastReviewedAt: "2026-07-10T14:00:00Z",
          lastReviewedByUserId: "superadmin-1",
          lastReviewedByDisplayName: "SuperAdmin",
        },
      ])
      .mockResolvedValueOnce({
        adminName: "",
        contactEmail: "",
        contactPhone: "",
        managementPhone: "",
        instagram: "",
        addressLine1: "",
        addressLine2: "",
        city: "",
        state: "",
        postalCode: "",
        canShowAddressOnLanding: false,
      })
      .mockResolvedValueOnce([
        {
          id: "suggestion-1",
          userId: "user-1",
          userDisplayName: "Paula",
          userEmail: "paula@homepit.dev",
          submittedAt: "2026-07-10T12:00:00Z",
          suggestionText: "Melhorar filtros de Projetos.",
          status: "EmExecucao",
          priority: "Urgente",
          internalComment: null,
          lastReviewedAt: "2026-07-10T15:00:00Z",
          lastReviewedByUserId: "superadmin-1",
          lastReviewedByDisplayName: "SuperAdmin",
        },
      ]);

    render(<PlatformAdminPage />);

    fireEvent.click(await screen.findByRole("tab", { name: "Sugestões" }));

    expect(await screen.findByDisplayValue("projetos")).toBeInTheDocument();
    expect(screen.getByText("Melhorar filtros de Projetos.")).toBeInTheDocument();
    expect(screen.queryByText("Revisar o módulo Financeiro.")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("checkbox", { name: "Selecionar sugestões visíveis" }));
    fireEvent.change(screen.getByLabelText("Status em massa"), { target: { value: "EmExecucao" } });
    fireEvent.change(screen.getByLabelText("Prioridade em massa"), { target: { value: "Urgente" } });
    fireEvent.click(screen.getByRole("button", { name: "Aplicar em massa" }));

    await waitFor(() => {
      expect(mockedApiFetch).toHaveBeenCalledWith(
        "/api/admin/platform/tool-improvement-suggestions/bulk-update",
        expect.objectContaining({
          method: "POST",
          token: "access-token",
          body: expect.stringContaining('"status":"EmExecucao"'),
        }),
      );
    });

    expect(window.localStorage.getItem("homepit.platform.suggestion-filters")).toContain('"search":"projetos"');
  });
});

function buildDashboard(systemRole: "User" | "Admin" | "SuperAdmin") {
  return {
    session: {
      accessToken: "access-token",
      refreshToken: "refresh-token",
      expiresAt: "2026-07-09T18:00:00Z",
      user: {
        id: "user-1",
        email: "user@homepit.dev",
        displayName: "User",
        systemRole,
        hasProfilePhoto: false,
      },
      households: [],
    },
    activeHouseholdId: "",
    activeHousehold: null,
    members: [],
    theme: "cozy" as const,
    sidebarCollapsed: false,
    loading: false,
    error: null,
    canShareHousehold: false,
    canManageHousehold: false,
    editingHousehold: null,
    activeModal: null,
    setError: vi.fn(),
    setSidebarCollapsed: vi.fn(),
    setTheme: vi.fn(),
    handleHouseholdChange: vi.fn(),
    handleLogout: vi.fn(),
    refreshHouseholds: vi.fn(async () => undefined),
    loadWorkspace: vi.fn(async () => undefined),
    openCreateHousehold: vi.fn(),
    openEditHousehold: vi.fn(),
    openShareHousehold: vi.fn(),
    closeModal: vi.fn(),
    createHousehold: vi.fn(async () => undefined),
    updateHousehold: vi.fn(async () => undefined),
    deleteHousehold: vi.fn(async () => undefined),
    shareHousehold: vi.fn(async () => undefined),
    handleAuthenticated: vi.fn(),
  };
}

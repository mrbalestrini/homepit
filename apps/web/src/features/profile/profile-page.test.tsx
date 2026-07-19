import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import * as api from "@/lib/api";
import type { CurrentUserPlanSummary, IntegrationConnection, PlanDefinition } from "@/lib/api";
import { useProjectDashboard } from "@/features/projects/use-project-dashboard";
import { ProfilePage } from "./profile-page";

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
    updateStoredSession: vi.fn(),
  };
});

vi.mock("@/features/projects/use-project-dashboard", () => ({
  useProjectDashboard: vi.fn(),
}));

vi.mock("@/features/workspace/account-state-gate", () => ({
  AccountStateGate: ({ children }: { children: ReactNode }) => <>{children}</>,
}));

vi.mock("@/features/workspace/organiza-club-auth", () => ({
  OrganizaClubAuth: () => <div>auth</div>,
}));

vi.mock("@/features/workspace/organiza-club-workspace-shell", () => ({
  OrganizaClubWorkspaceShell: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  Notice: ({ children }: { children: ReactNode }) => <div>{children}</div>,
}));

vi.mock("@/features/workspace/protected-user-avatar", () => ({
  ProtectedUserAvatar: () => <div>avatar</div>,
}));

vi.mock("@/features/workspace/delete-confirmation-dialog", () => ({
  DeleteConfirmationDialog: () => null,
}));

vi.mock("@/features/profile/profile-photo-crop-dialog", () => ({
  ProfilePhotoCropDialog: () => null,
}));

const mockedApiFetch = vi.mocked(api.apiFetch);
const mockedUseProjectDashboard = vi.mocked(useProjectDashboard);

describe("ProfilePage", () => {
  afterEach(() => {
    cleanup();
    document.body.removeAttribute("data-scroll-locked");
    document.body.removeAttribute("style");
  });

  beforeEach(() => {
    vi.clearAllMocks();
    cleanup();
    window.history.replaceState({}, "", "/profile");
  });

  it("navigates to the connection tab and creates a connection with a revealed token", async () => {
    window.history.replaceState({}, "", "/profile?tab=connection");
    mockedUseProjectDashboard.mockReturnValue(buildDashboard());
    const writeText = vi.fn(async () => undefined);
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: { writeText },
    });
    const createdConnection = buildIntegrationConnection();
    mockedApiFetch.mockImplementation(async (path, options) => {
      if (path === "/api/users/me/integration-connections" && !options?.method) {
        return [];
      }

      if (path === "/api/users/me/integration-connections" && options?.method === "POST") {
        return {
          connection: createdConnection,
          token: "hp_int_secret_once",
          restApiUrl: "https://api.organiza.club/api/integrations/v1",
          mcpUrl: "https://api.organiza.club/mcp",
        };
      }

      throw new Error(`Unexpected path: ${path}`);
    });

    render(<ProfilePage />);

    expect(await screen.findByText("Conexões")).toBeInTheDocument();
    expect(screen.getByText("Nenhuma conexão criada")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Nova conexão" }));
    fireEvent.change(screen.getByRole("textbox", { name: /nome/i }), { target: { value: "Automação financeira" } });
    fireEvent.change(screen.getByRole("combobox", { name: /permissão/i }), { target: { value: "ReadWrite" } });
    fireEvent.click(screen.getByRole("button", { name: "Criar conexão" }));

    await waitFor(() => {
      expect(mockedApiFetch).toHaveBeenCalledWith(
        "/api/users/me/integration-connections",
        expect.objectContaining({
          method: "POST",
          token: "access-token",
          body: expect.stringContaining('"accessMode":"ReadWrite"'),
        }),
      );
    });

    const revealDialog = await screen.findByRole("dialog", { name: "Guarde sua chave" });
    expect(revealDialog).toBeInTheDocument();
    expect(screen.getByDisplayValue("hp_int_secret_once")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Copiar chave" }));

    await waitFor(() => {
      expect(writeText).toHaveBeenCalledWith("hp_int_secret_once");
    });
    expect(screen.getByText("Automação financeira")).toBeInTheDocument();
    fireEvent.click(within(revealDialog).getAllByRole("button", { name: "Fechar" })[0]);
    await waitFor(() => {
      expect(screen.queryByRole("dialog", { name: "Guarde sua chave" })).not.toBeInTheDocument();
    });
  });

  it("revokes an active connection from the connection tab", async () => {
    window.history.replaceState({}, "", "/profile?tab=connection");
    mockedUseProjectDashboard.mockReturnValue(buildDashboard());
    const connection = buildIntegrationConnection();
    mockedApiFetch.mockImplementation(async (path, options) => {
      if (path === "/api/users/me/integration-connections" && !options?.method) {
        return [connection];
      }

      if (path === `/api/users/me/integration-connections/${connection.id}/revoke` && options?.method === "POST") {
        return undefined;
      }

      throw new Error(`Unexpected path: ${path}`);
    });

    render(<ProfilePage />);

    expect(await screen.findByText("Automação financeira")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Revogar" }));
    expect(await screen.findByRole("dialog", { name: "Revogar conexão" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Revogar conexão" }));

    await waitFor(() => {
      expect(mockedApiFetch).toHaveBeenCalledWith(
        "/api/users/me/integration-connections/connection-1/revoke",
        expect.objectContaining({ method: "POST", token: "access-token" }),
      );
    });
    await waitFor(() => {
      expect(screen.queryByRole("dialog", { name: "Revogar conexão" })).not.toBeInTheDocument();
    });
    expect(screen.getByText("Revogada")).toBeInTheDocument();
    expect(screen.getByText(/Expirou em/)).toBeInTheDocument();
  });

  it("hides connections revoked more than thirty days ago", async () => {
    window.history.replaceState({}, "", "/profile?tab=connection");
    mockedUseProjectDashboard.mockReturnValue(buildDashboard());
    mockedApiFetch.mockImplementation(async (path) => {
      if (path === "/api/users/me/integration-connections") {
        return [
          buildIntegrationConnection({
            name: "Conexão antiga",
            revokedAt: "2020-01-01T12:00:00Z",
            isActive: false,
          }),
        ];
      }

      throw new Error(`Unexpected path: ${path}`);
    });

    render(<ProfilePage />);

    expect(await screen.findByText("Nenhuma conexão criada")).toBeInTheDocument();
    expect(screen.queryByText("Conexão antiga")).not.toBeInTheDocument();
  });

  it("shows the current plan, usage and quota states", async () => {
    mockedUseProjectDashboard.mockReturnValue(buildDashboard());
    mockedApiFetch.mockImplementation(async (path) => {
      if (path === "/api/users/me/plan") {
        return buildCurrentUserPlanSummary({
          plan: buildPlanDefinition(),
          activeSubscription: {
            id: "subscription-1",
            userId: "user-1",
            userDisplayName: "User",
            userEmail: "user@organiza.club",
            planDefinitionId: "plan-standard",
            planSlug: "standard",
            planName: "Standard",
            billingCycle: "Monthly",
            startsAt: "2026-07-01T00:00:00Z",
            endsAt: "2026-07-31T23:59:59Z",
            amountPaid: 9.9,
            currencyCode: "BRL",
            status: "Active",
            adminNote: null,
          },
        });
      }

      if (path === "/api/plans") {
        return [
          buildPlanDefinition({ imagePolicyDescription: "Plano de entrada." }),
          buildPlanDefinition({
            id: "plan-gold",
            slug: "gold",
            name: "Gold",
            monthlyPrice: 39.9,
            annualPrice: 399,
            maxOwnedSpaces: 7,
            maxCores: 15,
            maxProjects: 15,
            maxInvitedMembers: null,
            maxOriginalImages: 300,
            isPopular: true,
            imagePolicyDescription: "Plano destaque.",
          }),
        ];
      }

      if (path === "/api/platform-settings") {
        return {
          contactEmail: "contato@organiza.club",
          contactPhone: "+55 (11) 91234-5678",
          instagram: "@organizaclub",
          addressLine1: "Rua Principal, 100",
          addressLine2: "Sala 2",
          city: "São Paulo",
          state: "SP",
          postalCode: "01000-000",
          canShowAddressOnLanding: false,
        };
      }

      throw new Error(`Unexpected path: ${path}`);
    });

    render(<ProfilePage />);

    expect(await screen.findByText("Plano")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Assinatura" })).toBeInTheDocument();
    expect(screen.queryByText("Solicitar assinatura")).not.toBeInTheDocument();
    expect(screen.queryByText(/R\$ 9,90\/mês/)).not.toBeInTheDocument();
    expect(screen.getAllByText("Espaços").length).toBeGreaterThan(0);
    expect(await screen.findByText("Núcleos")).toBeInTheDocument();
    expect(screen.getByText("Projetos")).toBeInTheDocument();
    expect(screen.getByText("Membros convidados")).toBeInTheDocument();
    expect(screen.getByText("Imagens originais")).toBeInTheDocument();
    expect(await screen.findByText("1 usados")).toBeInTheDocument();
    expect(await screen.findByText("2 usados")).toBeInTheDocument();
    expect(await screen.findByText("4 usados")).toBeInTheDocument();
    expect(await screen.findByText("Restante ilimitado")).toBeInTheDocument();
    expect(await screen.findByText(/novas criações ficam bloqueadas/i)).toBeInTheDocument();
    fireEvent.click(screen.getAllByRole("button", { name: "Assinatura" })[0]);

    const dialog = await screen.findByRole("dialog", { name: "Assinatura" });
    expect(dialog).toBeInTheDocument();
    expect(screen.getByText("Plano em uso")).toBeInTheDocument();
    expect(screen.getByText("Popular")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Plano atual" })).toBeDisabled();
    expect(screen.getByText("Gold")).toBeInTheDocument();
    expect(screen.queryByText(/Destino automático/i)).not.toBeInTheDocument();

    fireEvent.keyDown(document.body, { key: "Escape", code: "Escape" });

    await waitFor(() => {
      expect(screen.queryByRole("dialog", { name: "Assinatura" })).not.toBeInTheDocument();
    });
  });

  it("keeps the current hidden plan available in the subscription dialog", async () => {
    mockedUseProjectDashboard.mockReturnValue(buildDashboard());
    mockedApiFetch.mockImplementation(async (path) => {
      if (path === "/api/users/me/plan") {
        return buildCurrentUserPlanSummary({
          plan: buildPlanDefinition({
            id: "plan-hidden",
            slug: "hidden",
            name: "Plano Oculto",
            showInCatalog: false,
            imagePolicyDescription: "Plano fora do catálogo público.",
          }),
          activeSubscription: {
            id: "subscription-hidden",
            userId: "user-1",
            userDisplayName: "User",
            userEmail: "user@organiza.club",
            planDefinitionId: "plan-hidden",
            planSlug: "hidden",
            planName: "Plano Oculto",
            billingCycle: "Monthly",
            startsAt: "2026-07-01T00:00:00Z",
            endsAt: "2026-07-31T23:59:59Z",
            amountPaid: 0,
            currencyCode: "BRL",
            status: "Active",
            adminNote: null,
          },
        });
      }

      if (path === "/api/plans") {
        return [
          buildPlanDefinition({
            imagePolicyDescription: "Plano de entrada.",
          }),
          buildPlanDefinition({
            id: "plan-gold",
            slug: "gold",
            name: "Gold",
            monthlyPrice: 39.9,
            annualPrice: 399,
            maxOwnedSpaces: 7,
            maxCores: 15,
            maxProjects: 15,
            maxInvitedMembers: null,
            maxOriginalImages: 300,
            showInCatalog: true,
            imagePolicyDescription: "Plano intermediário.",
          }),
        ];
      }

      if (path === "/api/platform-settings") {
        return {
          contactEmail: "contato@organiza.club",
          contactPhone: "",
          instagram: "@organizaclub",
          addressLine1: "Rua Principal, 100",
          addressLine2: "Sala 2",
          city: "São Paulo",
          state: "SP",
          postalCode: "01000-000",
          canShowAddressOnLanding: false,
        };
      }

      throw new Error(`Unexpected path: ${path}`);
    });

    render(<ProfilePage />);

    await waitFor(() => {
      expect(screen.getByText("Plano Oculto")).toBeInTheDocument();
    });

    fireEvent.click(screen.getAllByRole("button", { name: "Assinatura" })[0]);

    const dialog = await screen.findByRole("dialog", { name: "Assinatura" });
    expect(within(dialog).getByText("Plano Oculto", { selector: "p.text-2xl" })).toBeInTheDocument();
    expect(within(dialog).getByText("Plano Oculto", { selector: "p.text-sm" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Plano atual" })).toBeDisabled();
  });

  it("opens the cancel account modal and then the confirmation dialog independently", async () => {
    mockedUseProjectDashboard.mockReturnValue(buildDashboard());
    mockedApiFetch.mockImplementation(async (path) => {
      if (path === "/api/users/me/plan") {
        return buildCurrentUserPlanSummary({
          plan: buildPlanDefinition({
            id: "plan-gold",
            slug: "gold",
            name: "Gold",
            monthlyPrice: 39.9,
            annualPrice: 399,
            maxOwnedSpaces: 7,
            maxCores: 15,
            maxProjects: 15,
            maxInvitedMembers: null,
            maxOriginalImages: 300,
            isPopular: true,
            imagePolicyDescription: "Plano destaque.",
          }),
          activeSubscription: null,
        });
      }

      if (path === "/api/plans") {
        return [
          buildPlanDefinition({
            imagePolicyDescription: "Plano de entrada.",
          }),
        ];
      }

      if (path === "/api/platform-settings") {
        return {
          contactEmail: "contato@organiza.club",
          contactPhone: "",
          instagram: "@organizaclub",
          addressLine1: "Rua Principal, 100",
          addressLine2: "Sala 2",
          city: "São Paulo",
          state: "SP",
          postalCode: "01000-000",
          canShowAddressOnLanding: false,
        };
      }

      throw new Error(`Unexpected path: ${path}`);
    });

    render(<ProfilePage />);

    fireEvent.click(screen.getAllByRole("button", { name: "Excluir conta" })[0]);

    const cancelDialog = await screen.findByRole("dialog", { name: "Cancelar conta" });
    expect(cancelDialog).toBeInTheDocument();
    expect(screen.queryByRole("dialog", { name: "Assinatura" })).not.toBeInTheDocument();
    expect(screen.getByText(/desativada agora e apagada automaticamente em 30 dias/i)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Desativar conta" }));

    await waitFor(() => {
      expect(screen.queryByRole("dialog", { name: "Cancelar conta" })).not.toBeInTheDocument();
    });

    const dangerDialog = await screen.findByRole("dialog", { name: "Desativar conta" });
    expect(dangerDialog).toBeInTheDocument();
    expect(screen.getByText(/Ao confirmar, o próximo login mostrará o aviso de conta desativada/i)).toBeInTheDocument();
  });

  it("opens the cores modal with the user's creations", async () => {
    mockedUseProjectDashboard.mockReturnValue(buildDashboard());
    mockedApiFetch.mockImplementation(async (path) => {
      if (path === "/api/users/me/plan") {
        return buildCurrentUserPlanSummary({
          plan: buildPlanDefinition({
            maxInvitedMembers: 6,
            imagePolicyDescription: "Não usado nesta tela.",
          }),
          activeSubscription: null,
        });
      }

      if (path === "/api/plans") {
        return [
          buildPlanDefinition({
            maxInvitedMembers: 6,
            imagePolicyDescription: "Plano de entrada.",
          }),
        ];
      }

      if (path === "/api/platform-settings") {
        return {
          contactEmail: "contato@organiza.club",
          contactPhone: "",
          instagram: "@organizaclub",
          addressLine1: "Rua Principal, 100",
          addressLine2: "Sala 2",
          city: "São Paulo",
          state: "SP",
          postalCode: "01000-000",
          canShowAddressOnLanding: false,
        };
      }

      if (path === "/api/users/me/plan/creations/cores") {
        return [
          {
            id: "core-1",
            name: "Núcleo Alfa",
            createdAt: "2026-07-03T10:00:00Z",
            spaceId: "space-2",
            spaceName: "Espaço Compartilhado",
            canDelete: true,
            coreId: null,
            coreName: null,
          },
        ];
      }

      throw new Error(`Unexpected path: ${path}`);
    });

    render(<ProfilePage />);

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /núcleos/i })).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: /núcleos/i }));

    expect(await screen.findByText("Núcleos criados por você")).toBeInTheDocument();
    expect(await screen.findByText("Núcleo Alfa")).toBeInTheDocument();
    expect(await screen.findByText("Espaço: Espaço Compartilhado")).toBeInTheDocument();

    await waitFor(() => {
      expect(mockedApiFetch).toHaveBeenCalledWith(
        "/api/users/me/plan/creations/cores",
        expect.objectContaining({
          token: "access-token",
        }),
      );
    });
  });
});

function buildDashboard(options?: { selectedCoreId?: string }) {
  return buildDashboardWithOptions(options);
}

function buildDashboardWithOptions({ selectedCoreId = "core-1" }: { selectedCoreId?: string } = {}) {
  return {
    session: {
      accessToken: "access-token",
      refreshToken: "refresh-token",
      expiresAt: "2026-07-09T18:00:00Z",
      user: {
        id: "user-1",
        email: "user@organiza.club",
        displayName: "User",
        phoneNumber: null,
        systemRole: "User" as const,
        hasProfilePhoto: false,
      },
      spaces: [
        {
          id: "space-1",
          name: "Espaço",
          role: "Owner" as const,
          createdAt: "2026-07-01T00:00:00Z",
          isOwnedByCurrentUser: true,
        },
      ],
    },
    activeSpaceId: "space-1",
    activeSpace: {
      id: "space-1",
      name: "Espaço",
      role: "Owner" as const,
      isOwnedByCurrentUser: true,
    },
    members: [],
    theme: "light" as const,
    sidebarCollapsed: false,
    loading: false,
    error: null,
    canShareSpace: false,
    canManageSpace: true,
    editingSpace: null,
    activeModal: null,
    selectedCoreId,
    selectedProjectId: "",
    projects: [
      {
        id: "project-1",
        coreId: "core-1",
        coreName: "Núcleo",
        coreImageUrl: null,
        coreHasImage: false,
        coreImageUpdatedAt: null,
        name: "Projeto 1",
        createdByMemberId: "member-1",
        activityCount: 1,
        isOutOfPlan: false,
        canEdit: true,
        canDelete: true,
      },
      {
        id: "project-2",
        coreId: "core-1",
        coreName: "Núcleo",
        coreImageUrl: null,
        coreHasImage: false,
        coreImageUpdatedAt: null,
        name: "Projeto 2",
        createdByMemberId: "member-1",
        activityCount: 0,
        isOutOfPlan: false,
        canEdit: true,
        canDelete: true,
      },
      {
        id: "project-3",
        coreId: "core-1",
        coreName: "Núcleo",
        coreImageUrl: null,
        coreHasImage: false,
        coreImageUpdatedAt: null,
        name: "Projeto 3",
        createdByMemberId: "member-1",
        activityCount: 0,
        isOutOfPlan: false,
        canEdit: true,
        canDelete: true,
      },
      {
        id: "project-4",
        coreId: "core-1",
        coreName: "Núcleo",
        coreImageUrl: null,
        coreHasImage: false,
        coreImageUpdatedAt: null,
        name: "Projeto 4",
        createdByMemberId: "member-1",
        activityCount: 0,
        isOutOfPlan: false,
        canEdit: false,
        canDelete: true,
      },
    ],
    setError: vi.fn(),
    setSidebarCollapsed: vi.fn(),
    setTheme: vi.fn(),
    handleSpaceChange: vi.fn(),
    handleLogout: vi.fn(),
    refreshSpaces: vi.fn(async () => undefined),
    loadWorkspace: vi.fn(async () => undefined),
    openCreateSpace: vi.fn(),
    openEditSpace: vi.fn(),
    openShareSpace: vi.fn(),
    closeModal: vi.fn(),
    createSpace: vi.fn(async () => undefined),
    updateSpace: vi.fn(async () => undefined),
    deleteSpace: vi.fn(async () => undefined),
    shareSpace: vi.fn(async () => undefined),
    handleAuthenticated: vi.fn(),
  };
}

function buildPlanDefinition(overrides: Partial<PlanDefinition> = {}): PlanDefinition {
  return {
    id: "plan-standard",
    slug: "standard",
    name: "Standard",
    currencyCode: "BRL",
    monthlyPrice: 9.9,
    annualPrice: 99,
    maxOwnedSpaces: 1,
    maxCores: 3,
    maxProjects: 5,
    maxInvitedMembers: null,
    maxOriginalImages: 30,
    showInCatalog: true,
    isPopular: false,
    imagePolicyDescription:
      "Mantém até 30 imagem(ns) privada(s) recente(s) em qualidade original; a partir da imagem 31, a mais antiga é substituída por WEBP com até 300 px e qualidade 30%.",
    ...overrides,
  };
}

function buildIntegrationConnection(overrides: Partial<IntegrationConnection> = {}): IntegrationConnection {
  return {
    id: "connection-1",
    name: "Automação financeira",
    credentialKind: "ManualToken",
    accessMode: "ReadWrite",
    spaceId: "space-1",
    spaceName: "Espaço",
    tokenPrefix: "hp_int_abcd",
    expiresAt: "2026-10-12T23:59:59Z",
    revokedAt: null,
    lastUsedAt: null,
    createdAt: "2026-07-14T12:00:00Z",
    isActive: true,
    ...overrides,
  };
}

function buildCurrentUserPlanSummary(
  overrides: Partial<CurrentUserPlanSummary> = {},
): CurrentUserPlanSummary {
  return {
    plan: buildPlanDefinition(),
    activeSubscription: null,
    usage: {
      ownedSpaceCount: 1,
      coreCount: 2,
      projectCount: 4,
      invitedMemberCount: 3,
      managedOriginalImageCount: 12,
    },
    ...overrides,
  };
}

describe("ProfilePage subscription CTA", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    cleanup();
    window.history.replaceState({}, "", "/profile");
  });

  it("falls back to e-mail when no WhatsApp number is available", async () => {
    mockedUseProjectDashboard.mockReturnValue(buildDashboard());
    mockedApiFetch.mockImplementation(async (path) => {
      if (path === "/api/users/me/plan") {
        return buildCurrentUserPlanSummary({
          plan: buildPlanDefinition({
            imagePolicyDescription: "Plano de entrada.",
          }),
          activeSubscription: null,
        });
      }

      if (path === "/api/plans") {
        return [
          buildPlanDefinition({
            imagePolicyDescription: "Plano de entrada.",
          }),
        ];
      }

      if (path === "/api/platform-settings") {
        return {
          contactEmail: "contato@organiza.club",
          contactPhone: "",
          instagram: "@organizaclub",
          addressLine1: "Rua Principal, 100",
          addressLine2: "Sala 2",
          city: "São Paulo",
          state: "SP",
          postalCode: "01000-000",
          canShowAddressOnLanding: false,
        };
      }

      throw new Error(`Unexpected path: ${path}`);
    });

    render(<ProfilePage />);

    await screen.findByText("Standard");

    fireEvent.click(screen.getAllByRole("button", { name: "Assinatura" })[0]);

    const dialog = await screen.findByRole("dialog", { name: "Assinatura" });
    expect(within(dialog).getByText("E-mail")).toBeInTheDocument();
    expect(within(dialog).queryByText("WhatsApp")).not.toBeInTheDocument();
  });
});

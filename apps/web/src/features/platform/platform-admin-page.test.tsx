import { fireEvent, render, screen, waitFor } from "@testing-library/react";
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
    vi.clearAllMocks();
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
            maxUniversesPerHousehold: 3,
            maxProjectsPerUniverse: 3,
            maxOriginalImages: 30,
            imagePolicyDescription:
              "Mantém até 30 imagem(ns) privada(s) recente(s) em qualidade original; a partir da imagem 31, a mais antiga é substituída por WEBP com até 300 px e qualidade 30%.",
          },
        ];
      }

      if (path === "/api/admin/platform/subscriptions") {
        return [];
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
          maxUniversesPerHousehold: 3,
          maxProjectsPerUniverse: 3,
          maxOriginalImages: 30,
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

    fireEvent.click(screen.getByRole("tab", { name: "Planos" }));
    fireEvent.change(await screen.findByLabelText("Preço mensal"), { target: { value: "11.90" } });
    fireEvent.click(screen.getByRole("button", { name: "Salvar plano" }));

    await waitFor(() => {
      expect(mockedApiFetch).toHaveBeenCalledWith(
        "/api/admin/platform/plans/plan-standard",
        expect.objectContaining({
          method: "PUT",
          token: "access-token",
          body: expect.stringContaining('"monthlyPrice":11.9'),
        }),
      );
    });
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

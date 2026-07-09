import { render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as api from "@/lib/api";
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

vi.mock("@/features/workspace/homepit-auth", () => ({
  HomePitAuth: () => <div>auth</div>,
}));

vi.mock("@/features/workspace/homepit-workspace-shell", () => ({
  HomePitWorkspaceShell: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  Notice: ({ children }: { children: ReactNode }) => <div>{children}</div>,
}));

vi.mock("@/features/workspace/protected-user-avatar", () => ({
  ProtectedUserAvatar: () => <div>avatar</div>,
}));

vi.mock("@/features/profile/profile-photo-crop-dialog", () => ({
  ProfilePhotoCropDialog: () => null,
}));

const mockedApiFetch = vi.mocked(api.apiFetch);
const mockedUseProjectDashboard = vi.mocked(useProjectDashboard);

describe("ProfilePage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("shows the current plan, usage and image policy", async () => {
    mockedUseProjectDashboard.mockReturnValue(buildDashboard());
    mockedApiFetch.mockImplementation(async (path) => {
      if (path === "/api/users/me/plan") {
        return {
          plan: {
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
          activeSubscription: {
            id: "subscription-1",
            userId: "user-1",
            userDisplayName: "User",
            userEmail: "user@homepit.dev",
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
          usage: {
            ownedHouseholdCount: 1,
            managedOriginalImageCount: 12,
            activeHouseholdUniverseCount: 2,
          },
        };
      }

      throw new Error(`Unexpected path: ${path}`);
    });

    render(<ProfilePage />);

    expect(await screen.findByText("Plano")).toBeInTheDocument();
    expect(screen.getByText("Standard")).toBeInTheDocument();
    expect(screen.getByText(/R\$ 9,90\/mês/)).toBeInTheDocument();
    expect(screen.getByText(/Mantém até 30 imagem\(ns\) privada\(s\) recente\(s\)/)).toBeInTheDocument();
    expect(screen.getByText("Casas em uso")).toBeInTheDocument();
    expect(screen.getByText("Imagens em uso")).toBeInTheDocument();

    await waitFor(() => {
      expect(mockedApiFetch).toHaveBeenCalledWith(
        "/api/users/me/plan",
        expect.objectContaining({
          token: "access-token",
        }),
      );
    });
  });
});

function buildDashboard() {
  return {
    session: {
      accessToken: "access-token",
      refreshToken: "refresh-token",
      expiresAt: "2026-07-09T18:00:00Z",
      user: {
        id: "user-1",
        email: "user@homepit.dev",
        displayName: "User",
        phoneNumber: null,
        systemRole: "User" as const,
        hasProfilePhoto: false,
      },
      households: [
        {
          id: "household-1",
          name: "Casa",
          role: "Owner" as const,
          createdAt: "2026-07-01T00:00:00Z",
        },
      ],
    },
    activeHouseholdId: "household-1",
    activeHousehold: {
      id: "household-1",
      name: "Casa",
      role: "Owner" as const,
    },
    members: [],
    theme: "cozy" as const,
    sidebarCollapsed: false,
    loading: false,
    error: null,
    canShareHousehold: false,
    canManageHousehold: true,
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

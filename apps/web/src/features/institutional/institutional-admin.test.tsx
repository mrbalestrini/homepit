import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { AuthResponse, InstitutionalPageContent } from "@/lib/api";
import * as api from "@/lib/api";
import { InstitutionalAdmin } from "./institutional-admin";

vi.mock("sonner", () => ({
  toast: {
    success: vi.fn(),
  },
}));

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api")>();
  return {
    ...actual,
    apiFetch: vi.fn(),
    clearSession: vi.fn(),
    readSession: vi.fn(),
    storeSession: vi.fn(),
    subscribeToSessionChanges: vi.fn(() => () => undefined),
  };
});

const mockedApiFetch = vi.mocked(api.apiFetch);
const mockedReadSession = vi.mocked(api.readSession);

describe("InstitutionalAdmin", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("blocks authenticated users who are not SuperAdmin", async () => {
    mockedReadSession.mockReturnValue(buildSession("Admin"));

    render(<InstitutionalAdmin />);

    expect(await screen.findByText("Acesso restrito")).toBeInTheDocument();
    expect(mockedApiFetch).not.toHaveBeenCalled();
  });

  it("loads the CMS for SuperAdmin and publishes the edited content", async () => {
    const page = buildPage();
    mockedReadSession.mockReturnValue(buildSession("SuperAdmin"));
    mockedApiFetch
      .mockResolvedValueOnce(page)
      .mockResolvedValueOnce({ ...page, heroTitle: "Título alterado" });

    render(<InstitutionalAdmin />);

    const heroTitle = await screen.findByDisplayValue("Título inicial");
    fireEvent.change(heroTitle, { target: { value: "Título alterado" } });
    fireEvent.click(screen.getByRole("button", { name: "Salvar e publicar" }));

    await waitFor(() => {
      expect(mockedApiFetch).toHaveBeenLastCalledWith(
        "/api/admin/institutional-page",
        expect.objectContaining({
          method: "PUT",
          token: "access-token",
          body: expect.stringContaining('"heroTitle":"Título alterado"'),
        }),
      );
    });
  });
});

function buildSession(systemRole: AuthResponse["user"]["systemRole"]): AuthResponse {
  return {
    accessToken: "access-token",
    refreshToken: "refresh-token",
    expiresAt: "2026-06-15T18:00:00Z",
    user: {
      id: "user-1",
      email: "admin@homepit.dev",
      displayName: "Admin",
      systemRole,
      hasProfilePhoto: false,
    },
    households: [],
  };
}

function buildPage(): InstitutionalPageContent {
  return {
    slug: "home",
    seoTitle: "HomePit",
    seoDescription: "Descrição",
    brandName: "HomePit",
    brandTagline: "Casa organizada",
    heroEyebrow: "Destaque",
    heroTitle: "Título inicial",
    heroDescription: "Descrição principal",
    primaryCtaLabel: "Contato",
    primaryCtaUrl: "https://example.com/contact",
    benefitsTitle: "Benefícios",
    benefitsDescription: "Descrição dos benefícios",
    benefits: [{ position: 0, title: "Benefício", description: "Descrição" }],
    stepsTitle: "Como funciona",
    stepsDescription: "Descrição das etapas",
    steps: [{ position: 0, title: "Etapa", description: "Descrição" }],
    highlightEyebrow: "Produto",
    highlightTitle: "Destaque do produto",
    highlightDescription: "Descrição do produto",
    finalCtaTitle: "Chamada final",
    finalCtaDescription: "Descrição final",
    footerText: "Rodapé",
    heroImageAlt: "Imagem principal",
    hasHeroImage: false,
    heroImageUpdatedAt: null,
    highlightImageAlt: "Imagem de destaque",
    hasHighlightImage: false,
    highlightImageUpdatedAt: null,
    hasSeoImage: false,
    seoImageUpdatedAt: null,
    updatedAt: null,
  };
}

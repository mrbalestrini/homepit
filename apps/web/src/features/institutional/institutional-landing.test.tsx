import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { InstitutionalPageContent } from "@/lib/api";
import { InstitutionalLanding } from "./institutional-landing";

describe("InstitutionalLanding", () => {
  it("renders managed sections, external CTA and image alternative text", () => {
    render(<InstitutionalLanding page={buildPage()} />);

    expect(screen.getByRole("heading", { name: "Um espaço que avança" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Benefício editável" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Etapa editável" })).toBeInTheDocument();
    expect(screen.getByAltText("Imagem principal acessível")).toBeInTheDocument();
    expect(screen.getByAltText("Imagem de destaque acessível")).toBeInTheDocument();

    const contactLinks = screen.getAllByRole("link", { name: "Falar com a Organiza Club" });
    expect(contactLinks.length).toBeGreaterThan(1);
    expect(contactLinks[0]).toHaveAttribute("href", "https://example.com/contact");
    expect(contactLinks[0]).toHaveAttribute("target", "_blank");
    expect(screen.getAllByRole("link", { name: /Entrar/ }).some((link) => link.getAttribute("href") === "/projects")).toBe(true);
  });

  it("keeps an internal managed CTA inside the app", () => {
    render(<InstitutionalLanding page={buildPage({ primaryCtaLabel: "Entrar no clube", primaryCtaUrl: "/projects" })} />);

    const links = screen.getAllByRole("link", { name: "Entrar no clube" });
    expect(links.some((link) => link.getAttribute("href") === "/projects" && !link.hasAttribute("target"))).toBe(true);
  });
});

function buildPage(overrides: Partial<InstitutionalPageContent> = {}): InstitutionalPageContent {
  return {
    slug: "home",
    seoTitle: "Organiza Club",
    seoDescription: "Descrição",
    brandName: "Organiza Club",
    brandTagline: "Espaço organizado",
    heroEyebrow: "Destaque",
    heroTitle: "Um espaço que avança",
    heroDescription: "Descrição principal",
    primaryCtaLabel: "Falar com a Organiza Club",
    primaryCtaUrl: "https://example.com/contact",
    benefitsTitle: "Benefícios",
    benefitsDescription: "Descrição dos benefícios",
    benefits: [{ position: 0, title: "Benefício editável", description: "Descrição" }],
    stepsTitle: "Como funciona",
    stepsDescription: "Descrição das etapas",
    steps: [{ position: 0, title: "Etapa editável", description: "Descrição" }],
    highlightEyebrow: "Produto",
    highlightTitle: "Destaque do produto",
    highlightDescription: "Descrição do produto",
    finalCtaTitle: "Chamada final",
    finalCtaDescription: "Descrição final",
    footerText: "Rodapé",
    heroImageAlt: "Imagem principal acessível",
    hasHeroImage: true,
    heroImageUpdatedAt: "2026-06-15T12:00:00Z",
    highlightImageAlt: "Imagem de destaque acessível",
    hasHighlightImage: true,
    highlightImageUpdatedAt: "2026-06-15T12:00:00Z",
    hasSeoImage: false,
    seoImageUpdatedAt: null,
    updatedAt: "2026-06-15T12:00:00Z",
    ...overrides,
  };
}

import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { InstitutionalPageContent } from "@/lib/api";
import { InstitutionalLanding } from "./institutional-landing";

describe("InstitutionalLanding", () => {
  it("renders managed sections, external CTA and image alternative text", () => {
    render(<InstitutionalLanding page={buildPage()} />);

    expect(screen.getByRole("heading", { name: "Uma casa que avança" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Benefício editável" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Etapa editável" })).toBeInTheDocument();
    expect(screen.getByAltText("Imagem principal acessível")).toBeInTheDocument();
    expect(screen.getByAltText("Imagem de destaque acessível")).toBeInTheDocument();

    const contactLinks = screen.getAllByRole("link", { name: "Falar com a HomePit" });
    expect(contactLinks.length).toBeGreaterThan(1);
    expect(contactLinks[0]).toHaveAttribute("href", "https://example.com/contact");
    expect(contactLinks[0]).toHaveAttribute("target", "_blank");
    expect(screen.getAllByRole("link", { name: /Entrar/ }).some((link) => link.getAttribute("href") === "/projects")).toBe(true);
  });
});

function buildPage(): InstitutionalPageContent {
  return {
    slug: "home",
    seoTitle: "HomePit",
    seoDescription: "Descrição",
    brandName: "HomePit",
    brandTagline: "Casa organizada",
    heroEyebrow: "Destaque",
    heroTitle: "Uma casa que avança",
    heroDescription: "Descrição principal",
    primaryCtaLabel: "Falar com a HomePit",
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
  };
}

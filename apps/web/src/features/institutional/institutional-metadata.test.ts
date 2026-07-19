import { describe, expect, it } from "vitest";
import type { InstitutionalPageContent } from "@/lib/api";
import { buildInstitutionalMetadata } from "./institutional-metadata";

describe("buildInstitutionalMetadata", () => {
  it("prefers the SEO image for social sharing", () => {
    const metadata = buildInstitutionalMetadata(buildPage({ hasSeoImage: true, seoImageUpdatedAt: "2026-06-18T12:00:00Z" }));

    expect(metadata.openGraph?.images).toEqual([
      {
        url: "http://localhost:8080/api/institutional-page/images/seo?v=2026-06-18T12%3A00%3A00Z",
        alt: "Organiza Club",
      },
    ]);
    expect(metadata.twitter?.images).toEqual([
      "http://localhost:8080/api/institutional-page/images/seo?v=2026-06-18T12%3A00%3A00Z",
    ]);
  });

  it("falls back to the hero image when the SEO image is missing", () => {
    const metadata = buildInstitutionalMetadata(buildPage({ hasHeroImage: true, heroImageUpdatedAt: "2026-06-18T12:00:00Z" }));

    expect(metadata.openGraph?.images).toEqual([
      {
        url: "http://localhost:8080/api/institutional-page/images/hero?v=2026-06-18T12%3A00%3A00Z",
        alt: "Organiza Club",
      },
    ]);
  });
});

function buildPage(overrides: Partial<InstitutionalPageContent>): InstitutionalPageContent {
  return {
    slug: "home",
    seoTitle: "Organiza Club",
    seoDescription: "Descrição",
    brandName: "Organiza Club",
    brandTagline: "Espaço organizado",
    heroEyebrow: "Destaque",
    heroTitle: "Título",
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
    highlightTitle: "Destaque",
    highlightDescription: "Descrição",
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
    ...overrides,
  };
}

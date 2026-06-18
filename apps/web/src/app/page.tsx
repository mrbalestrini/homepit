import type { Metadata } from "next";
import { API_BASE_URL, type InstitutionalPageContent } from "@/lib/api";
import { InstitutionalLanding } from "@/features/institutional/institutional-landing";
import { buildInstitutionalMetadata } from "@/features/institutional/institutional-metadata";

export const dynamic = "force-dynamic";

async function loadInstitutionalPage(): Promise<InstitutionalPageContent> {
  const response = await fetch(`${API_BASE_URL}/api/institutional-page`, {
    cache: "no-store",
  });

  if (!response.ok) {
    throw new Error("Não foi possível carregar a página institucional.");
  }

  return (await response.json()) as InstitutionalPageContent;
}

export async function generateMetadata(): Promise<Metadata> {
  return buildInstitutionalMetadata(await loadInstitutionalPage());
}

export default async function Home() {
  return <InstitutionalLanding page={await loadInstitutionalPage()} />;
}

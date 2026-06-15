import Image from "next/image";
import Link from "next/link";
import { ArrowRight, CheckCircle2, Home, Layers3, Sparkles, UsersRound } from "lucide-react";
import { Button } from "@/components/ui/button";
import { API_BASE_URL, type InstitutionalPageContent } from "@/lib/api";

export function InstitutionalLanding({ page }: { page: InstitutionalPageContent }) {
  const heroImageUrl = buildImageUrl("hero", page.heroImageUpdatedAt);
  const highlightImageUrl = buildImageUrl("highlight", page.highlightImageUpdatedAt);

  return (
    <main className="min-h-screen overflow-hidden bg-background text-foreground">
      <header className="sticky top-0 z-20 border-b border-border/70 bg-surface-strong/95 backdrop-blur-xl">
        <div className="mx-auto flex max-w-7xl items-center gap-4 px-4 py-3 sm:px-6 lg:px-8">
          <Link className="flex min-w-0 items-center gap-3" href="/">
            <span className="grid size-10 shrink-0 place-items-center rounded-[14px] bg-primary text-primary-foreground">
              <Home className="size-4" />
            </span>
            <span className="min-w-0">
              <span className="block font-display text-2xl leading-none">{page.brandName}</span>
              <span className="mt-1 hidden truncate text-xs text-muted-foreground sm:block">{page.brandTagline}</span>
            </span>
          </Link>

          <nav className="ml-auto hidden items-center gap-6 text-sm font-semibold text-muted-foreground md:flex">
            <a href="#beneficios">Benefícios</a>
            <a href="#como-funciona">Como funciona</a>
            <a href="#produto">Produto</a>
          </nav>

          <div className="ml-auto flex items-center gap-2 md:ml-6">
            <Button asChild variant="ghost">
              <Link href="/projects">Entrar</Link>
            </Button>
            <Button asChild className="hidden sm:inline-flex">
              <a href={page.primaryCtaUrl} target="_blank" rel="noreferrer">
                {page.primaryCtaLabel}
              </a>
            </Button>
          </div>
        </div>
      </header>

      <section className="relative px-4 pb-20 pt-14 sm:px-6 sm:pt-20 lg:px-8 lg:pb-28">
        <div className="absolute inset-x-0 top-0 -z-0 h-[540px] bg-[radial-gradient(circle_at_15%_15%,rgba(31,143,120,0.18),transparent_42%),radial-gradient(circle_at_88%_15%,rgba(138,106,84,0.18),transparent_38%)]" />
        <div className="relative mx-auto grid max-w-7xl items-center gap-12 lg:grid-cols-[1.02fr_0.98fr]">
          <div className="max-w-3xl">
            <p className="text-xs font-bold uppercase tracking-[0.2em] text-primary">{page.heroEyebrow}</p>
            <h1 className="mt-5 font-display text-5xl leading-[1.02] sm:text-6xl lg:text-7xl">{page.heroTitle}</h1>
            <p className="mt-6 max-w-2xl text-lg leading-8 text-muted-foreground sm:text-xl">{page.heroDescription}</p>

            <div className="mt-8 flex flex-col gap-3 sm:flex-row">
              <Button asChild size="lg">
                <a href={page.primaryCtaUrl} target="_blank" rel="noreferrer">
                  {page.primaryCtaLabel}
                  <ArrowRight />
                </a>
              </Button>
              <Button asChild size="lg" variant="secondary">
                <Link href="/projects">Entrar no sistema</Link>
              </Button>
            </div>

            <div className="mt-10 grid gap-3 text-sm text-muted-foreground sm:grid-cols-3">
              {["Projetos com contexto", "Permissões por casa", "Conhecimento reutilizável"].map((label) => (
                <div className="flex items-center gap-2" key={label}>
                  <CheckCircle2 className="size-4 text-primary" />
                  <span>{label}</span>
                </div>
              ))}
            </div>
          </div>

          <div className="relative">
            <div className="absolute -inset-5 rounded-[40px] bg-gradient-to-br from-primary/20 via-transparent to-accent/40 blur-2xl" />
            <div className="relative overflow-hidden rounded-[32px] border border-border/70 bg-surface-elevated p-3 shadow-lg">
              {page.hasHeroImage && heroImageUrl ? (
                <Image
                  className="aspect-[4/3] h-auto w-full rounded-[24px] object-cover"
                  src={heroImageUrl}
                  alt={page.heroImageAlt}
                  width={1200}
                  height={900}
                  priority
                />
              ) : (
                <ProductIllustration />
              )}
            </div>
          </div>
        </div>
      </section>

      <section className="border-y border-border/70 bg-surface/65 px-4 py-20 sm:px-6 lg:px-8" id="beneficios">
        <div className="mx-auto max-w-7xl">
          <SectionHeading title={page.benefitsTitle} description={page.benefitsDescription} />
          <div className="mt-10 grid gap-4 md:grid-cols-3">
            {page.benefits.map((benefit, index) => {
              const Icon = [Layers3, UsersRound, Sparkles][index % 3];
              return (
                <article className="rounded-[26px] border border-border/70 bg-surface-strong p-6 shadow-sm" key={`${benefit.position}-${benefit.title}`}>
                  <span className="grid size-12 place-items-center rounded-[18px] bg-highlight text-primary">
                    <Icon className="size-5" />
                  </span>
                  <h3 className="mt-5 text-xl font-semibold">{benefit.title}</h3>
                  <p className="mt-3 leading-7 text-muted-foreground">{benefit.description}</p>
                </article>
              );
            })}
          </div>
        </div>
      </section>

      <section className="px-4 py-20 sm:px-6 lg:px-8" id="como-funciona">
        <div className="mx-auto max-w-7xl">
          <SectionHeading title={page.stepsTitle} description={page.stepsDescription} />
          <ol className="mt-12 grid gap-6 lg:grid-cols-3">
            {page.steps.map((step, index) => (
              <li className="relative rounded-[26px] border border-border/70 bg-surface p-6" key={`${step.position}-${step.title}`}>
                <span className="font-display text-5xl text-primary/35">{String(index + 1).padStart(2, "0")}</span>
                <h3 className="mt-6 text-xl font-semibold">{step.title}</h3>
                <p className="mt-3 leading-7 text-muted-foreground">{step.description}</p>
              </li>
            ))}
          </ol>
        </div>
      </section>

      <section className="px-4 py-20 sm:px-6 lg:px-8" id="produto">
        <div className="mx-auto grid max-w-7xl items-center gap-10 overflow-hidden rounded-[34px] border border-border/70 bg-surface-strong p-6 shadow-md sm:p-9 lg:grid-cols-2 lg:p-12">
          <div>
            <p className="text-xs font-bold uppercase tracking-[0.2em] text-primary">{page.highlightEyebrow}</p>
            <h2 className="mt-4 font-display text-4xl leading-tight sm:text-5xl">{page.highlightTitle}</h2>
            <p className="mt-5 text-lg leading-8 text-muted-foreground">{page.highlightDescription}</p>
          </div>
          <div className="overflow-hidden rounded-[26px] border border-border/70 bg-surface-muted">
            {page.hasHighlightImage && highlightImageUrl ? (
              <Image
                className="aspect-[4/3] h-auto w-full object-cover"
                src={highlightImageUrl}
                alt={page.highlightImageAlt}
                width={1000}
                height={750}
              />
            ) : (
              <div className="grid aspect-[4/3] place-items-center p-8 text-center">
                <div>
                  <Home className="mx-auto size-12 text-primary" />
                  <p className="mt-4 font-display text-3xl">{page.brandName}</p>
                  <p className="mt-2 text-muted-foreground">{page.brandTagline}</p>
                </div>
              </div>
            )}
          </div>
        </div>
      </section>

      <section className="px-4 pb-20 pt-8 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-5xl rounded-[34px] bg-primary px-6 py-12 text-center text-primary-foreground shadow-lg sm:px-12">
          <h2 className="font-display text-4xl leading-tight sm:text-5xl">{page.finalCtaTitle}</h2>
          <p className="mx-auto mt-5 max-w-2xl text-lg leading-8 opacity-85">{page.finalCtaDescription}</p>
          <Button asChild className="mt-8 bg-primary-foreground text-primary hover:bg-primary-foreground/90" size="lg">
            <a href={page.primaryCtaUrl} target="_blank" rel="noreferrer">
              {page.primaryCtaLabel}
              <ArrowRight />
            </a>
          </Button>
        </div>
      </section>

      <footer className="border-t border-border/70 bg-surface/75 px-4 py-10 sm:px-6 lg:px-8">
        <div className="mx-auto flex max-w-7xl flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="font-display text-2xl">{page.brandName}</p>
            <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground">{page.footerText}</p>
          </div>
          <Button asChild variant="ghost">
            <Link href="/projects">Acessar o HomePit</Link>
          </Button>
        </div>
      </footer>
    </main>
  );
}

function SectionHeading({ title, description }: { title: string; description: string }) {
  return (
    <div className="mx-auto max-w-3xl text-center">
      <h2 className="font-display text-4xl leading-tight sm:text-5xl">{title}</h2>
      <p className="mt-4 text-lg leading-8 text-muted-foreground">{description}</p>
    </div>
  );
}

function ProductIllustration() {
  return (
    <div className="grid aspect-[4/3] gap-3 rounded-[24px] bg-surface-muted p-4 sm:grid-cols-[0.8fr_1.2fr]">
      <div className="space-y-3 rounded-[20px] bg-surface-strong p-4">
        <div className="h-8 w-2/3 rounded-full bg-primary/20" />
        {["Casa", "Projetos", "Prompts", "Rotinas"].map((item, index) => (
          <div className="flex items-center gap-3 rounded-[14px] bg-surface-muted p-3" key={item}>
            <span className={`size-3 rounded-full ${index === 1 ? "bg-primary" : "bg-border"}`} />
            <span className="text-sm font-semibold">{item}</span>
          </div>
        ))}
      </div>
      <div className="grid gap-3 sm:grid-rows-[auto_1fr]">
        <div className="rounded-[20px] bg-surface-strong p-5">
          <p className="text-xs uppercase tracking-[0.16em] text-muted-foreground">Casa em movimento</p>
          <p className="mt-2 font-display text-3xl">12 atividades</p>
        </div>
        <div className="grid grid-cols-2 gap-3">
          {["Planejar", "Em andamento", "Concluído", "Conhecimento"].map((item) => (
            <div className="rounded-[20px] bg-surface-strong p-4" key={item}>
              <div className="h-2 w-1/2 rounded-full bg-primary/25" />
              <p className="mt-4 text-sm font-semibold">{item}</p>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function buildImageUrl(slot: "hero" | "highlight", updatedAt?: string | null) {
  if (!updatedAt) {
    return null;
  }

  return `${API_BASE_URL}/api/institutional-page/images/${slot}?v=${encodeURIComponent(updatedAt)}`;
}

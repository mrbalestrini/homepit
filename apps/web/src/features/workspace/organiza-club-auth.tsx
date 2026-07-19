"use client";

import Image from "next/image";
import { Layers, Repeat2, Users } from "lucide-react";
import { FormEvent, useState } from "react";
import { AuthResponse, apiFetch } from "@/lib/api";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";

export function OrganizaClubAuth({
  onAuthenticated,
}: {
  onAuthenticated: (auth: AuthResponse) => void;
}) {
  const [mode, setMode] = useState<"login" | "register">("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setLoading(true);

    try {
      const auth = await apiFetch<AuthResponse>(mode === "login" ? "/api/auth/login" : "/api/auth/register", {
        method: "POST",
        body: JSON.stringify(
          mode === "login"
            ? { email, password }
            : { email, password, displayName, phoneNumber: phoneNumber || null },
        ),
      });
      onAuthenticated(auth);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível entrar agora. Tente novamente.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="relative min-h-screen overflow-hidden px-4 py-8 sm:px-6 lg:px-10">
      <div className="mx-auto grid min-h-[calc(100vh-4rem)] max-w-7xl items-center gap-10 lg:grid-cols-[1.15fr_0.85fr]">
        <section className="relative overflow-hidden rounded-[30px] border border-border/70 bg-surface p-8 shadow-lg backdrop-blur-md sm:p-10">
          <div className="absolute inset-x-0 top-0 h-40 bg-[radial-gradient(circle_at_top_left,rgba(31,143,120,0.18),transparent_58%)]" />
          <div className="relative space-y-8">
            <div className="flex items-center gap-3">
              <div>
                <span className="relative block">
                  <Image className="brand-logo-navy h-12 w-auto" src="/brand/organiza-club-wordmark-navy.svg" alt="Organiza Club" width={288} height={100} priority />
                  <Image className="brand-logo-cream h-12 w-auto" src="/brand/organiza-club-wordmark-cream.svg" alt="Organiza Club" width={288} height={100} priority />
                </span>
                <p className="mt-2 text-sm text-muted-foreground">Um clube de organização.</p>
              </div>
            </div>

            <div className="space-y-5">
              <Badge className="rounded-full px-3 py-1 text-[12px]" variant="default">
                Controle tranquilo
              </Badge>
              <div className="space-y-3">
                <h1 className="font-display text-4xl leading-tight text-foreground sm:text-5xl">
                  Clareza inteligente para cuidar da vida.
                </h1>
                <p className="max-w-2xl text-base leading-7 text-muted-foreground sm:text-lg">
                  Seu espaço para organizar finanças, projetos, estudos, vida e muito mais. Compartilhe o que fizer
                  sentido, um passo de cada vez.
                </p>
              </div>
            </div>

            <div className="grid gap-4 sm:grid-cols-3">
              <HighlightCard
                icon={<Layers className="size-5" />}
                title="Menos coisas soltas"
                description="Espaços, núcleos e projetos ajudam cada assunto a encontrar seu lugar."
              />
              <HighlightCard
                icon={<Users className="size-5" />}
                title="Colaboração"
                description="Compartilhe cada espaço com permissões claras para cada pessoa."
              />
              <HighlightCard
                icon={<Repeat2 className="size-5" />}
                title="Mais clareza"
                description="Entenda o que precisa de atenção e cuide do próximo passo."
              />
            </div>
          </div>
        </section>

        <Card className="bg-surface shadow-lg backdrop-blur-md">
          <CardContent className="p-6 sm:p-8">
            <form className="space-y-6" onSubmit={submit}>
              <div className="space-y-3">
                <div className="inline-flex w-full rounded-[18px] border border-border/70 bg-surface-muted p-1">
                  <ModeButton active={mode === "login"} onClick={() => setMode("login")}>
                    Entrar
                  </ModeButton>
                  <ModeButton active={mode === "register"} onClick={() => setMode("register")}>
                    Criar conta
                  </ModeButton>
                </div>

                <div>
                  <h2 className="text-2xl font-semibold text-foreground">
                    {mode === "login" ? "Entrar no clube" : "Criar sua conta"}
                  </h2>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {mode === "login"
                      ? "Continue de onde parou, com tudo no seu lugar."
                      : "Crie sua conta e monte seu primeiro espaço quando entrar."}
                  </p>
                </div>
              </div>

              {error ? (
                <div className="rounded-[18px] border border-danger/20 bg-status-danger-soft px-4 py-3 text-sm text-danger">
                  {error}
                </div>
              ) : null}

              <div className="space-y-4">
                <Field label="E-mail">
                  <Input
                    type="email"
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
                    autoComplete="email"
                    required
                  />
                </Field>

                <Field label="Senha">
                  <Input
                    type="password"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    autoComplete={mode === "login" ? "current-password" : "new-password"}
                    minLength={8}
                    required
                  />
                </Field>

                {mode === "register" ? (
                  <>
                    <Field label="Nome">
                      <Input
                        value={displayName}
                        onChange={(event) => setDisplayName(event.target.value)}
                        autoComplete="name"
                        required
                      />
                    </Field>

                    <Field label="WhatsApp">
                      <Input
                        value={phoneNumber}
                        onChange={(event) => setPhoneNumber(event.target.value)}
                        autoComplete="tel"
                      />
                    </Field>
                  </>
                ) : null}
              </div>

              <Button className="w-full" size="lg" type="submit" disabled={loading}>
                {loading ? "Entrando..." : mode === "login" ? "Entrar no clube" : "Criar conta"}
              </Button>
            </form>
          </CardContent>
        </Card>
      </div>
    </main>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="grid gap-2">
      <span className="text-sm font-semibold text-foreground/80">{label}</span>
      {children}
    </label>
  );
}

function ModeButton({
  active,
  children,
  onClick,
}: {
  active: boolean;
  children: React.ReactNode;
  onClick: () => void;
}) {
  return (
    <button
      className={cn(
        "flex-1 rounded-[18px] px-4 py-3 text-sm font-semibold transition",
        active ? "bg-surface-strong text-foreground shadow-sm" : "text-muted-foreground hover:text-foreground",
      )}
      type="button"
      onClick={onClick}
    >
      {children}
    </button>
  );
}

function HighlightCard({
  icon,
  title,
  description,
}: {
  icon: React.ReactNode;
  title: string;
  description: string;
}) {
  return (
    <div className="rounded-[22px] border border-border/70 bg-surface-elevated p-5 shadow-sm">
      <div className="mb-4 inline-flex size-11 items-center justify-center rounded-[18px] bg-accent text-accent-foreground">
        {icon}
      </div>
      <h3 className="text-base font-semibold text-foreground">{title}</h3>
      <p className="mt-2 text-sm leading-6 text-muted-foreground">{description}</p>
    </div>
  );
}

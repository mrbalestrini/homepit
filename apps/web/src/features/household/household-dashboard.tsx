"use client";

import Link from "next/link";
import { ArrowRight, Layers, Pencil, RefreshCw, Share2, ShieldCheck, Sparkles, UserMinus, Users } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { HomePitAuth } from "@/features/workspace/homepit-auth";
import { AvatarCircle } from "@/features/workspace/protected-user-avatar";
import {
  LoadingState,
  Notice,
  HomePitWorkspaceShell,
} from "@/features/workspace/homepit-workspace-shell";
import { type HouseholdMember } from "@/lib/api";
import { useProjectDashboard } from "@/features/projects/use-project-dashboard";

export function HouseholdDashboard() {
  const dashboard = useProjectDashboard();

  if (!dashboard.session) {
    return <HomePitAuth onAuthenticated={dashboard.handleAuthenticated} />;
  }

  return <HouseholdDashboardWorkspace dashboard={dashboard} />;
}

function HouseholdDashboardWorkspace({ dashboard }: { dashboard: ReturnType<typeof useProjectDashboard> }) {
  const adminCount = dashboard.members.filter((member) => member.role === "Admin").length;
  const ownerCount = dashboard.members.filter((member) => member.role === "Owner").length;
  const memberCount = dashboard.members.filter((member) => member.role === "Member").length;
  const headerStats = [
    { label: "Membros", value: dashboard.members.length },
    { label: "Proprietários", value: ownerCount },
    { label: "Administradores", value: adminCount },
    { label: "Projetos", value: dashboard.projects.length },
    { label: "Universos", value: dashboard.universes.length },
  ];

  return (
    <HomePitWorkspaceShell
      controller={{
        session: dashboard.session,
        activeHouseholdId: dashboard.activeHouseholdId,
        activeHousehold: dashboard.activeHousehold,
        members: dashboard.members,
        theme: dashboard.theme,
        sidebarCollapsed: dashboard.sidebarCollapsed,
        loading: dashboard.loading,
        error: dashboard.error,
        canShareHousehold: dashboard.canShareHousehold,
        canManageHousehold: dashboard.canManageHousehold,
        editingHousehold: dashboard.editingHousehold,
        isHouseholdDialogOpen: dashboard.activeModal === "household",
        isShareDialogOpen: dashboard.activeModal === "share",
        setError: dashboard.setError,
        setSidebarCollapsed: dashboard.setSidebarCollapsed,
        setTheme: dashboard.setTheme,
        handleHouseholdChange: dashboard.handleHouseholdChange,
        handleLogout: dashboard.handleLogout,
        refreshHouseholds: dashboard.refreshHouseholds,
        refreshWorkspace: async () => dashboard.loadWorkspace(),
        openCreateHousehold: dashboard.openCreateHousehold,
        openEditHousehold: dashboard.openEditHousehold,
        openShareHousehold: dashboard.openShareHousehold,
        closeCommonModal: dashboard.closeModal,
        createHousehold: dashboard.createHousehold,
        updateHousehold: dashboard.updateHousehold,
        deleteHousehold: dashboard.deleteHousehold,
        shareHousehold: dashboard.shareHousehold,
        updateProfile: dashboard.updateProfile,
      }}
      activeModule="household"
      subtitle="Visão de administração, permissões e acesso da casa"
      visibleCount={dashboard.members.length}
      visibleLabel="pessoas"
      headerStats={headerStats}
    >
      {dashboard.loading && dashboard.members.length === 0 ? (
        <LoadingState
          title="Carregando administração da casa"
          description="Estamos reunindo membros, permissões e atalhos antes de mostrar o painel."
          icon={<ShieldCheck className="size-5 animate-pulse" />}
        />
      ) : (
        <div className="grid gap-3 xl:grid-cols-[minmax(0,1.3fr)_360px]">
          <div className="space-y-3">
            <Card className="overflow-hidden">
              <CardContent className="grid gap-6 p-0">
                <div className="border-b border-border/70 bg-gradient-to-br from-surface-strong via-surface to-highlight/20 p-5 sm:p-6">
                  <div className="flex flex-wrap items-start justify-between gap-4">
                    <div className="max-w-2xl">
                      <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
                        Administração da casa
                      </p>
                      <h1 className="mt-2 text-3xl font-semibold text-foreground">
                        {dashboard.activeHousehold?.name ?? "Casa sem nome"}
                      </h1>
                      <p className="mt-3 max-w-xl text-sm leading-6 text-muted-foreground">
                        Centralize o nome da casa, o acesso das pessoas e os atalhos mais importantes em uma visão
                        mais operacional, sem perder o histórico do que já foi feito.
                      </p>
                    </div>

                    <div className="flex flex-wrap items-center gap-2">
                      <Badge variant="outline">{dashboard.activeHousehold?.role ?? "Membro"}</Badge>
                      <Badge variant="neutral">
                        {dashboard.canShareHousehold ? "Pode convidar pessoas" : "Acesso limitado"}
                      </Badge>
                    </div>
                  </div>

                  <div className="mt-5 flex flex-wrap gap-2">
                    <Button onClick={dashboard.openEditHousehold} disabled={!dashboard.canManageHousehold}>
                      <Pencil />
                      Editar nome
                    </Button>
                    <Button variant="secondary" onClick={dashboard.openShareHousehold} disabled={!dashboard.canShareHousehold}>
                      <Share2 />
                      Compartilhar
                    </Button>
                    <Button asChild variant="ghost">
                      <Link href="/projects">
                        <Layers />
                        Projetos
                      </Link>
                    </Button>
                    <Button asChild variant="ghost">
                      <Link href="/prompts">
                        <Sparkles />
                        Prompts
                      </Link>
                    </Button>
                  </div>
                </div>

                <div className="grid gap-3 px-5 pb-5 sm:px-6 lg:grid-cols-3">
                  <MetricCard label="Pessoas" value={dashboard.members.length} description="Pessoas vinculadas à casa" />
                  <MetricCard label="Admins" value={adminCount} description="Capazes de compartilhar a casa" />
                  <MetricCard label="Membros" value={memberCount} description="Pessoas com acesso básico" />
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Membros</CardTitle>
                <CardDescription>Os vínculos da casa preservam histórico, comentários e autoria.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-2">
                {dashboard.members.length ? (
                  dashboard.members.map((member) => (
                    <MemberRow
                      key={member.id}
                      member={member}
                      canManageHousehold={dashboard.canManageHousehold}
                    />
                  ))
                ) : (
                  <Notice tone="warning">Ainda não há membros carregados para esta casa.</Notice>
                )}
              </CardContent>
            </Card>
          </div>

          <div className="space-y-3">
            <Card>
              <CardHeader>
                <CardTitle>Atalhos</CardTitle>
                <CardDescription>Acesso direto às ações mais recorrentes da casa.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-2">
                <ShortcutButton
                  icon={<Pencil />}
                  title="Editar casa"
                  description="Ajuste o nome e mantenha a estrutura alinhada ao momento atual."
                  action={
                    <Button variant="ghost" size="sm" onClick={dashboard.openEditHousehold} disabled={!dashboard.canManageHousehold}>
                      Abrir
                      <ArrowRight />
                    </Button>
                  }
                />
                <ShortcutButton
                  icon={<Share2 />}
                  title="Compartilhar"
                  description="Convide administradores e membros sem sair do contexto da casa."
                  action={
                    <Button variant="ghost" size="sm" onClick={dashboard.openShareHousehold} disabled={!dashboard.canShareHousehold}>
                      Abrir
                      <ArrowRight />
                    </Button>
                  }
                />
                <ShortcutButton
                  icon={<RefreshCw />}
                  title="Atualizar dados"
                  description="Recarregue membros e contexto quando algo mudar fora da aba atual."
                  action={
                    <Button variant="ghost" size="sm" onClick={() => void dashboard.loadWorkspace()}>
                      Atualizar
                    </Button>
                  }
                />
                <ShortcutButton
                  icon={<Users />}
                  title="Voltar ao trabalho"
                  description="Acesse rapidamente os módulos de operação mais usados."
                  action={
                    <div className="flex flex-wrap gap-2">
                      <Button asChild variant="ghost" size="sm">
                        <Link href="/projects">Projetos</Link>
                      </Button>
                      <Button asChild variant="ghost" size="sm">
                        <Link href="/prompts">Prompts</Link>
                      </Button>
                    </div>
                  }
                />
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Retirada de membros</CardTitle>
                <CardDescription>Estrutura preparada para remoção sem apagar a história da pessoa na casa.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                <Notice tone="warning">
                  A interface já reserva o espaço para retirada individual, mas o endpoint de remoção ainda não está
                  exposto no contrato atual da API.
                </Notice>
                <div className="rounded-[18px] border border-dashed border-border/70 bg-surface-muted p-4">
                  <div className="flex items-center gap-3">
                    <div className="grid size-10 place-items-center rounded-full bg-surface-strong text-muted-foreground">
                      <UserMinus className="size-4" />
                    </div>
                    <div>
                      <p className="text-sm font-semibold text-foreground">Retirar membro</p>
                      <p className="text-sm text-muted-foreground">
                        Quando a operação chegar na API, ela poderá ser conectada aqui sem mexer no restante da tela.
                      </p>
                    </div>
                  </div>
                </div>
              </CardContent>
            </Card>
          </div>
        </div>
      )}
    </HomePitWorkspaceShell>
  );
}

function MetricCard({
  label,
  value,
  description,
}: {
  label: string;
  value: number;
  description: string;
}) {
  return (
    <div className="rounded-[18px] border border-border/70 bg-surface-muted p-4">
      <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">{label}</p>
      <p className="mt-2 text-3xl font-semibold text-foreground">{value}</p>
      <p className="mt-2 text-sm leading-6 text-muted-foreground">{description}</p>
    </div>
  );
}

function ShortcutButton({
  icon,
  title,
  description,
  action,
}: {
  icon: React.ReactNode;
  title: string;
  description: string;
  action: React.ReactNode;
}) {
  return (
    <div className="rounded-[18px] border border-border/70 bg-surface-muted p-4">
      <div className="flex items-start gap-3">
        <div className="grid size-10 place-items-center rounded-full bg-surface-strong text-foreground">{icon}</div>
        <div className="min-w-0 flex-1">
          <p className="text-sm font-semibold text-foreground">{title}</p>
          <p className="mt-1 text-sm leading-6 text-muted-foreground">{description}</p>
        </div>
      </div>
      <div className="mt-3 flex justify-end">{action}</div>
    </div>
  );
}

function MemberRow({
  member,
  canManageHousehold,
}: {
  member: HouseholdMember;
  canManageHousehold: boolean;
}) {
  return (
    <div className="flex flex-col gap-3 rounded-[18px] border border-border/70 bg-surface-muted p-4 sm:flex-row sm:items-center sm:justify-between">
      <div className="flex min-w-0 items-center gap-3">
        <AvatarCircle name={member.displayName} className="size-11 border border-border/70 bg-surface-strong text-foreground" />
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <p className="truncate text-sm font-semibold text-foreground">{member.displayName}</p>
            {member.isCurrentUser ? <Badge variant="neutral">Você</Badge> : null}
          </div>
          <p className="truncate text-sm text-muted-foreground">{member.email}</p>
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <Badge variant={member.role === "Owner" ? "default" : "outline"}>{member.role}</Badge>
        {member.isCurrentUser ? (
          <Badge variant="neutral">Seu acesso</Badge>
        ) : (
          <Button variant="ghost" size="sm" disabled={!canManageHousehold} title="Remoção ainda não conectada à API">
            <UserMinus />
            Retirar
          </Button>
        )}
      </div>
    </div>
  );
}

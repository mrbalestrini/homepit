"use client";

import Link from "next/link";
import { CheckCircle2, Inbox, Loader2, Layers, NotebookPen, Pencil, Share2, ShieldCheck, UserMinus, XCircle } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Select } from "@/components/ui/select";
import { AccountStateGate } from "@/features/workspace/account-state-gate";
import { OrganizaClubAuth } from "@/features/workspace/organiza-club-auth";
import { SpaceMemberAvatar } from "@/features/workspace/protected-user-avatar";
import {
  LoadingState,
  Notice,
  OrganizaClubWorkspaceShell,
} from "@/features/workspace/organiza-club-workspace-shell";
import { apiFetch, type Space, type SpaceInvitation, type SpaceMember } from "@/lib/api";
import { useProjectDashboard } from "@/features/projects/use-project-dashboard";
import { toast } from "sonner";

export function SpaceDashboard() {
  const dashboard = useProjectDashboard();

  if (!dashboard.session) {
    return <OrganizaClubAuth onAuthenticated={dashboard.handleAuthenticated} />;
  }

  return (
    <AccountStateGate session={dashboard.session}>
      <SpaceDashboardWorkspace dashboard={dashboard} />
    </AccountStateGate>
  );
}

function SpaceDashboardWorkspace({ dashboard }: { dashboard: ReturnType<typeof useProjectDashboard> }) {
  const spaces = dashboard.session?.spaces ?? [];
  const adminCount = dashboard.members.filter((member) => member.role === "Admin").length;
  const ownerCount = dashboard.members.filter((member) => member.role === "Owner").length;
  const headerStats = [
    { label: "Membros", value: dashboard.members.length },
    { label: "Proprietários", value: ownerCount },
    { label: "Administradores", value: adminCount },
    { label: "Projetos", value: dashboard.projects.length },
    { label: "Núcleos", value: dashboard.cores.length },
  ];

  return (
    <OrganizaClubWorkspaceShell
      controller={{
        session: dashboard.session,
        activeSpaceId: dashboard.activeSpaceId,
        activeSpace: dashboard.activeSpace,
        members: dashboard.members,
        theme: dashboard.theme,
        sidebarCollapsed: dashboard.sidebarCollapsed,
        loading: dashboard.loading,
        error: dashboard.error,
        canShareSpace: dashboard.canShareSpace,
        canManageSpace: dashboard.canManageSpace,
        editingSpace: dashboard.editingSpace,
        isSpaceDialogOpen: dashboard.activeModal === "space",
        isShareDialogOpen: dashboard.activeModal === "share",
        setError: dashboard.setError,
        setSidebarCollapsed: dashboard.setSidebarCollapsed,
        setTheme: dashboard.setTheme,
        handleSpaceChange: dashboard.handleSpaceChange,
        handleLogout: dashboard.handleLogout,
        refreshSpaces: dashboard.refreshSpaces,
        refreshWorkspace: async () => dashboard.loadWorkspace(),
        openCreateSpace: dashboard.openCreateSpace,
        openEditSpace: dashboard.openEditSpace,
        openShareSpace: dashboard.openShareSpace,
        closeCommonModal: dashboard.closeModal,
        createSpace: dashboard.createSpace,
        updateSpace: dashboard.updateSpace,
        deleteSpace: dashboard.deleteSpace,
        shareSpace: dashboard.shareSpace,
      }}
      activeModule="space"
      subtitle="Visão de administração, permissões e acesso do espaço"
      visibleCount={dashboard.members.length}
      visibleLabel="pessoas"
      headerStats={headerStats}
      requireSpace={false}
    >
      <SpaceWorkspaceContent dashboard={dashboard} spaces={spaces} />
    </OrganizaClubWorkspaceShell>
  );
}

function SpaceWorkspaceContent({
  dashboard,
  spaces,
}: {
  dashboard: ReturnType<typeof useProjectDashboard>;
  spaces: Space[];
}) {
  const [invitations, setInvitations] = useState<SpaceInvitation[]>([]);
  const [invitesLoading, setInvitesLoading] = useState(true);
  const [busyInvitationId, setBusyInvitationId] = useState<string | null>(null);
  const adminCount = useMemo(() => dashboard.members.filter((member) => member.role === "Admin").length, [dashboard.members]);
  const ownerCount = useMemo(() => dashboard.members.filter((member) => member.role === "Owner").length, [dashboard.members]);
  const memberCount = useMemo(() => dashboard.members.filter((member) => member.role === "Member").length, [dashboard.members]);

  const token = dashboard.session?.accessToken ?? "";

  const incomingInvitations = useMemo(
    () => invitations.filter((invitation) => invitation.isIncoming),
    [invitations],
  );
  const outgoingInvitations = useMemo(
    () => invitations.filter((invitation) => !invitation.isIncoming),
    [invitations],
  );

  const loadInvitations = useCallback(async () => {
    if (!token) {
      setInvitesLoading(false);
      return;
    }

    setInvitesLoading(true);

    try {
      const nextInvitations = await apiFetch<SpaceInvitation[]>("/api/spaces/invitations", {
        token,
      });
      setInvitations(nextInvitations);
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível carregar os convites.");
    } finally {
      setInvitesLoading(false);
    }
  }, [token]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadInvitations();
    }, 0);

    return () => window.clearTimeout(timer);
  }, [loadInvitations]);

  async function acceptInvitation(invitation: SpaceInvitation) {
    if (!token) {
      return;
    }

    setBusyInvitationId(invitation.id);

    try {
      const space = await apiFetch<Space>(`/api/spaces/invitations/${invitation.id}/accept`, {
        method: "POST",
        token,
      });

      await dashboard.refreshSpaces();
      dashboard.handleSpaceChange(space.id);
      await loadInvitations();
      toast.success(`Convite do espaço ${invitation.spaceName} aceito.`);
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível aceitar o convite.");
    } finally {
      setBusyInvitationId(null);
    }
  }

  async function declineInvitation(invitation: SpaceInvitation) {
    if (!token) {
      return;
    }

    setBusyInvitationId(invitation.id);

    try {
      await apiFetch<void>(`/api/spaces/invitations/${invitation.id}/decline`, {
        method: "POST",
        token,
      });

      await loadInvitations();
      toast.success(`Convite do espaço ${invitation.spaceName} recusado.`);
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível recusar o convite.");
    } finally {
      setBusyInvitationId(null);
    }
  }

  if (dashboard.loading && dashboard.members.length === 0 && invitesLoading) {
    return (
      <LoadingState
        title="Carregando administração do espaço"
        description="Estamos reunindo membros, convites e atalhos antes de mostrar o painel."
        icon={<ShieldCheck className="size-5 animate-pulse" />}
      />
    );
  }

  return (
    <div className="grid gap-3 xl:grid-cols-[minmax(0,1.3fr)_360px]">
      <div className="space-y-3">
        <SpaceInviteSection
          invitesLoading={invitesLoading}
          incomingInvitations={incomingInvitations}
          outgoingInvitations={outgoingInvitations}
          busyInvitationId={busyInvitationId}
          onAcceptInvitation={acceptInvitation}
          onDeclineInvitation={declineInvitation}
        />

        <Card className="overflow-hidden">
          <CardContent className="grid gap-6 p-0">
            <div className="border-b border-border/70 bg-gradient-to-br from-surface-strong via-surface to-highlight/20 p-5 sm:p-6">
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div className="max-w-2xl">
                  <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
                    Administração do espaço
                  </p>
                  <h1 className="mt-2 text-3xl font-semibold text-foreground">
                    {dashboard.activeSpace?.name ?? "Espaço sem nome"}
                  </h1>
                  <p className="mt-3 max-w-xl text-sm leading-6 text-muted-foreground">
                    Centralize o nome do espaço, o acesso das pessoas e os atalhos mais importantes em uma visão
                    mais operacional, sem perder o histórico do que já foi feito.
                  </p>
                </div>

                <div className="flex flex-wrap items-center gap-2">
                  <Badge variant="outline">{dashboard.activeSpace?.role ?? "Membro"}</Badge>
                  <Badge variant="neutral">
                    {dashboard.canShareSpace ? "Pode convidar pessoas" : "Acesso limitado"}
                  </Badge>
                </div>
              </div>

              <div className="mt-5 flex flex-wrap gap-2">
                <Button onClick={dashboard.openEditSpace} disabled={!dashboard.canManageSpace}>
                  <Pencil />
                  Editar nome
                </Button>
                <Button variant="secondary" onClick={dashboard.openShareSpace} disabled={!dashboard.canShareSpace}>
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
                    <NotebookPen />
                    Prompts
                  </Link>
                </Button>
              </div>
            </div>

            <div className="grid gap-3 px-5 pb-5 sm:px-6 lg:grid-cols-3">
              <MetricCard label="Pessoas" value={dashboard.members.length} description="Pessoas vinculadas ao espaço" />
              <MetricCard label="Admins" value={adminCount + ownerCount} description="Capazes de compartilhar o espaço" />
              <MetricCard label="Membros" value={memberCount} description="Pessoas com acesso básico" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Membros</CardTitle>
            <CardDescription>Os vínculos do espaço preservam histórico, comentários e autoria.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            {dashboard.members.length ? (
              dashboard.members.map((member) => (
                <MemberRow
                  key={`${member.id}-${member.role}`}
                  member={member}
                  spaceId={dashboard.activeSpaceId}
                  token={dashboard.session?.accessToken}
                  canManageSpace={dashboard.canManageSpace}
                  onUpdateRole={dashboard.updateSpaceMember}
                  onRemove={dashboard.removeSpaceMember}
                />
              ))
            ) : (
              <Notice tone="warning">Ainda não há membros carregados para este espaço.</Notice>
            )}
          </CardContent>
        </Card>
      </div>

      <div className="space-y-3">
        {!dashboard.activeSpaceId ? (
          <Card>
            <CardHeader>
              <CardTitle>Sem espaço vinculado</CardTitle>
              <CardDescription>Veja os convites pendentes ou crie um novo espaço para começar.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              {incomingInvitations.length === 0 ? (
                <Notice tone="warning">Nenhum convite aguardando sua resposta no momento.</Notice>
              ) : null}
              <div className="flex flex-col gap-2 sm:flex-row">
                <Button onClick={dashboard.openCreateSpace}>
                  <ShieldCheck />
                  Novo espaço
                </Button>
                <Button variant="secondary" onClick={() => void loadInvitations()} disabled={invitesLoading}>
                  <Inbox />
                  Atualizar convites
                </Button>
              </div>
            </CardContent>
          </Card>
        ) : null}

        {spaces.length > 1 ? (
          <Card>
            <CardHeader>
              <CardTitle>Troca rápida</CardTitle>
              <CardDescription>Troque de espaço sem sair da administração.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <Select
                value={dashboard.activeSpaceId}
                onChange={(event) => dashboard.handleSpaceChange(event.target.value)}
                aria-label="Selecionar espaço"
              >
                {spaces.map((space) => (
                  <option key={space.id} value={space.id}>
                    {space.name}
                  </option>
                ))}
              </Select>
              <Button onClick={dashboard.openCreateSpace}>
                <ShieldCheck />
                Novo espaço
              </Button>
            </CardContent>
          </Card>
        ) : null}
      </div>
    </div>
  );
}

function SpaceInviteSection({
  invitesLoading,
  incomingInvitations,
  outgoingInvitations,
  busyInvitationId,
  onAcceptInvitation,
  onDeclineInvitation,
}: {
  invitesLoading: boolean;
  incomingInvitations: SpaceInvitation[];
  outgoingInvitations: SpaceInvitation[];
  busyInvitationId: string | null;
  onAcceptInvitation: (invitation: SpaceInvitation) => Promise<void>;
  onDeclineInvitation: (invitation: SpaceInvitation) => Promise<void>;
}) {
  if (invitesLoading && incomingInvitations.length === 0 && outgoingInvitations.length === 0) {
    return (
      <LoadingState
        title="Carregando convites"
        description="Estamos reunindo os convites recebidos e enviados."
        icon={<Inbox className="size-5 animate-pulse" />}
      />
    );
  }

  if (incomingInvitations.length === 0 && outgoingInvitations.length === 0) {
    return null;
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Convites</CardTitle>
        <CardDescription>Convites recebidos aparecem aqui para aceitar ou recusar. Os enviados mostram o status atual.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {incomingInvitations.length > 0 ? (
          <div className="space-y-2">
            <SectionLabel>Recebidos</SectionLabel>
            {incomingInvitations.map((invitation) => (
              <InvitationRow
                key={invitation.id}
                invitation={invitation}
                busy={busyInvitationId === invitation.id}
                onAccept={() => void onAcceptInvitation(invitation)}
                onDecline={() => void onDeclineInvitation(invitation)}
                incoming
              />
            ))}
          </div>
        ) : null}

        {outgoingInvitations.length > 0 ? (
          <div className="space-y-2">
            <SectionLabel>Enviados</SectionLabel>
            {outgoingInvitations.map((invitation) => (
              <InvitationRow key={invitation.id} invitation={invitation} busy={false} incoming={false} />
            ))}
          </div>
        ) : null}
      </CardContent>
    </Card>
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

function SectionLabel({ children }: { children: React.ReactNode }) {
  return <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">{children}</p>;
}

function InvitationRow({
  invitation,
  busy,
  incoming,
  onAccept,
  onDecline,
}: {
  invitation: SpaceInvitation;
  busy: boolean;
  incoming: boolean;
  onAccept?: () => void;
  onDecline?: () => void;
}) {
  const statusLabel =
    invitation.status === "Pending"
      ? "Pendente"
      : invitation.status === "Accepted"
        ? "Aceito"
        : "Recusado";

  return (
    <div className="rounded-[18px] border border-border/70 bg-surface-muted p-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0 space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <p className="truncate text-sm font-semibold text-foreground">{invitation.spaceName}</p>
            <Badge variant={invitation.status === "Pending" ? "neutral" : invitation.status === "Accepted" ? "success" : "danger"}>
              {statusLabel}
            </Badge>
            <Badge variant="outline">{invitation.role}</Badge>
          </div>
          <p className="text-sm text-muted-foreground">
            {incoming ? `Convidado por ${invitation.inviterDisplayName || "outro membro"}` : `Enviado para ${invitation.inviteeEmail}`}
          </p>
          <p className="text-xs text-muted-foreground">
            Enviado em {formatDateTime(invitation.invitedAt)}
            {invitation.respondedAt ? ` • Respondido em ${formatDateTime(invitation.respondedAt)}` : ""}
          </p>
        </div>
        {incoming && invitation.status === "Pending" ? (
          <div className="flex flex-wrap gap-2">
            <Button onClick={onAccept} disabled={busy}>
              {busy ? <Loader2 className="animate-spin" /> : <CheckCircle2 />}
              Aceitar
            </Button>
            <Button variant="secondary" onClick={onDecline} disabled={busy}>
              {busy ? <Loader2 className="animate-spin" /> : <XCircle />}
              Recusar
            </Button>
          </div>
        ) : null}
      </div>
    </div>
  );
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat("pt-BR", {
    dateStyle: "short",
    timeStyle: "short",
  }).format(new Date(value));
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
  spaceId,
  token,
  canManageSpace,
  onUpdateRole,
  onRemove,
}: {
  member: SpaceMember;
  spaceId?: string;
  token?: string;
  canManageSpace: boolean;
  onUpdateRole: (memberId: string, role: SpaceMember["role"]) => Promise<void>;
  onRemove: (member: SpaceMember) => Promise<void>;
}) {
  const [role, setRole] = useState(member.role);
  const [saving, setSaving] = useState(false);

  const isEditable = canManageSpace && !member.isCurrentUser && member.role !== "Owner";
  const canSave = isEditable && role !== member.role && !saving;

  async function saveRole() {
    if (!canSave) {
      return;
    }

    setSaving(true);
    try {
      await onUpdateRole(member.id, role);
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-3 rounded-[18px] border border-border/70 bg-surface-muted p-4 sm:flex-row sm:items-center sm:justify-between">
      <div className="flex min-w-0 items-center gap-3">
        <SpaceMemberAvatar
          member={member}
          token={token}
          spaceId={spaceId}
          className="size-11 border border-border/70 bg-surface-strong text-foreground"
        />
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
        {member.isCurrentUser ? <Badge variant="neutral">Seu acesso</Badge> : null}

        {isEditable ? (
          <>
            <Select value={role} onChange={(event) => setRole(event.target.value as SpaceMember["role"])}>
              <option value="Admin">Administrador</option>
              <option value="Member">Membro</option>
            </Select>
            <Button variant="secondary" size="sm" onClick={saveRole} disabled={!canSave}>
              <Pencil />
              Salvar
            </Button>
            <Button variant="ghost" size="sm" onClick={() => void onRemove(member)} disabled={saving}>
              <UserMinus />
              Remover
            </Button>
          </>
        ) : member.isCurrentUser ? null : (
          <Badge variant="neutral">Função protegida</Badge>
        )}
      </div>
    </div>
  );
}

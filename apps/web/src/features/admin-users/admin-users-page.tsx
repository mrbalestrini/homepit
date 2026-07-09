"use client";

import Link from "next/link";
import { Loader2, LogOut, ShieldOff, Trash2, UserCheck } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Select } from "@/components/ui/select";
import { type AdminUserListItem, apiFetch, clearSession } from "@/lib/api";
import { useProjectDashboard } from "@/features/projects/use-project-dashboard";
import { AccountStateGate } from "@/features/workspace/account-state-gate";
import { DeleteConfirmationDialog } from "@/features/workspace/delete-confirmation-dialog";
import { HomePitAuth } from "@/features/workspace/homepit-auth";
import { HomePitWorkspaceShell, Notice } from "@/features/workspace/homepit-workspace-shell";

type UserFilter = "all" | "Active" | "PendingSelfDeletion" | "DisabledBySuperAdmin";

export function AdminUsersPage() {
  const dashboard = useProjectDashboard();

  if (!dashboard.session) {
    return <HomePitAuth onAuthenticated={dashboard.handleAuthenticated} />;
  }

  return (
    <AccountStateGate session={dashboard.session}>
      <AdminUsersWorkspace dashboard={dashboard} />
    </AccountStateGate>
  );
}

function AdminUsersWorkspace({ dashboard }: { dashboard: ReturnType<typeof useProjectDashboard> }) {
  const session = dashboard.session!;

  if (session.user.systemRole !== "SuperAdmin") {
    return <AdminAccessDenied />;
  }

  return (
    <HomePitWorkspaceShell
      controller={{
        session,
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
      }}
      activeModule="platform"
      subtitle="Gestão global de contas, desativações e exclusões definitivas"
      visibleCount={session.households.length}
      visibleLabel="casas visíveis"
      headerStats={[{ label: "casas", value: session.households.length }]}
      requireHousehold={false}
    >
      <AdminUsersPanel token={session.accessToken} />
    </HomePitWorkspaceShell>
  );
}

function AdminUsersPanel({ token }: { token: string }) {
  const [users, setUsers] = useState<AdminUserListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyUserId, setBusyUserId] = useState<string | null>(null);
  const [filter, setFilter] = useState<UserFilter>("all");
  const [userToDelete, setUserToDelete] = useState<AdminUserListItem | null>(null);

  const loadUsers = useCallback(async () => {
    setLoading(true);

    try {
      setUsers(await apiFetch<AdminUserListItem[]>("/api/admin/users", { token }));
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível carregar os usuários.");
    } finally {
      setLoading(false);
    }
  }, [token]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadUsers();
    }, 0);

    return () => window.clearTimeout(timer);
  }, [loadUsers]);

  async function deactivateUser(userId: string) {
    setBusyUserId(userId);

    try {
      const updated = await apiFetch<AdminUserListItem>(`/api/admin/users/${userId}/deactivate`, {
        method: "POST",
        token,
      });
      setUsers((current) => current.map((user) => (user.id === updated.id ? updated : user)));
      toast.success("Usuário desativado.");
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível desativar o usuário.");
    } finally {
      setBusyUserId(null);
    }
  }

  async function reactivateUser(userId: string) {
    setBusyUserId(userId);

    try {
      const updated = await apiFetch<AdminUserListItem>(`/api/admin/users/${userId}/reactivate`, {
        method: "POST",
        token,
      });
      setUsers((current) => current.map((user) => (user.id === updated.id ? updated : user)));
      toast.success("Usuário reativado.");
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível reativar o usuário.");
    } finally {
      setBusyUserId(null);
    }
  }

  async function deleteUser(userId: string) {
    setBusyUserId(userId);

    try {
      await apiFetch<void>(`/api/admin/users/${userId}`, {
        method: "DELETE",
        token,
      });
      setUsers((current) => current.filter((user) => user.id !== userId));
      toast.success("Usuário excluído definitivamente.");
      setUserToDelete(null);
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível excluir o usuário.");
    } finally {
      setBusyUserId(null);
    }
  }

  const filteredUsers = useMemo(
    () => (filter === "all" ? users : users.filter((user) => user.accountState === filter)),
    [filter, users],
  );

  const summary = useMemo(
    () => ({
      total: users.length,
      active: users.filter((user) => user.accountState === "Active").length,
      pending: users.filter((user) => user.accountState === "PendingSelfDeletion").length,
      disabled: users.filter((user) => user.accountState === "DisabledBySuperAdmin").length,
    }),
    [users],
  );

  return (
    <div className="space-y-4">
      <Card>
        <CardContent className="flex flex-col gap-4 p-5 sm:p-6 lg:flex-row lg:items-end lg:justify-between">
          <div className="max-w-2xl">
            <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">SuperAdmin</p>
            <h1 className="mt-2 text-3xl font-semibold text-foreground">Usuários da plataforma</h1>
            <p className="mt-2 text-sm leading-6 text-muted-foreground">
              Desative, reative ou exclua contas comuns com visibilidade sobre casas próprias e vínculos ativos.
            </p>
          </div>
          <div className="w-full max-w-xs">
            <Select value={filter} onChange={(event) => setFilter(event.target.value as UserFilter)}>
              <option value="all">Todos os estados</option>
              <option value="Active">Ativos</option>
              <option value="PendingSelfDeletion">Cancelamento pendente</option>
              <option value="DisabledBySuperAdmin">Desativados pelo superadmin</option>
            </Select>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-3 md:grid-cols-4">
        <MetricCard label="Total" value={summary.total} />
        <MetricCard label="Ativos" value={summary.active} />
        <MetricCard label="Pendentes" value={summary.pending} />
        <MetricCard label="Desativados" value={summary.disabled} />
      </div>

      <Notice tone="warning">
        O superadmin é protegido e aparece apenas para leitura. Excluir um usuário proprietário apaga suas casas próprias e
        todos os vínculos dessas casas.
      </Notice>

      <Card>
        <CardHeader>
          <CardTitle>Lista global</CardTitle>
          <CardDescription>Estado atual das contas comuns e da conta protegida do superadmin.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {loading ? (
            <div className="grid min-h-[220px] place-items-center text-muted-foreground">
              <div className="flex items-center gap-2 text-sm">
                <Loader2 className="size-4 animate-spin" />
                Carregando usuários...
              </div>
            </div>
          ) : filteredUsers.length === 0 ? (
            <Notice tone="warning">Nenhum usuário encontrado para o filtro atual.</Notice>
          ) : (
            filteredUsers.map((user) => (
              <UserRow
                key={user.id}
                user={user}
                busy={busyUserId === user.id}
                onDeactivate={deactivateUser}
                onReactivate={reactivateUser}
                onDelete={setUserToDelete}
              />
            ))
          )}
        </CardContent>
      </Card>

      <DeleteConfirmationDialog
        open={Boolean(userToDelete)}
        title="Excluir usuário"
        description="Essa ação remove a conta e todos os vínculos do usuário. Se ele possuir casas próprias, elas também serão apagadas com seus dados vinculados."
        confirmationTarget={userToDelete?.email}
        confirmationLabel={`Digite o e-mail ${userToDelete?.email ?? ""} para confirmar`}
        confirmLabel="Excluir usuário"
        impactItems={[
          "A conta, os tokens e os vínculos ativos do usuário.",
          "Comentários autorados por ele em outras casas.",
          "Casas próprias do usuário e todos os vínculos dessas casas, quando existirem.",
        ]}
        onOpenChange={(open) => {
          if (!open) {
            setUserToDelete(null);
          }
        }}
        onConfirm={async () => {
          if (!userToDelete) {
            return;
          }

          await deleteUser(userToDelete.id);
        }}
      />
    </div>
  );
}

function UserRow({
  user,
  busy,
  onDeactivate,
  onReactivate,
  onDelete,
}: {
  user: AdminUserListItem;
  busy: boolean;
  onDeactivate: (userId: string) => Promise<void>;
  onReactivate: (userId: string) => Promise<void>;
  onDelete: (user: AdminUserListItem) => void;
}) {
  const stateLabel =
    user.accountState === "Active"
      ? "Ativa"
      : user.accountState === "PendingSelfDeletion"
        ? "Cancelamento pendente"
        : "Desativada pelo superadmin";

  const scheduledDeletionLabel =
    user.scheduledDeletionAt
      ? new Intl.DateTimeFormat("pt-BR", {
          dateStyle: "short",
          timeStyle: "short",
        }).format(new Date(user.scheduledDeletionAt))
      : null;

  return (
    <div className="rounded-[20px] border border-border/70 bg-surface-muted p-4">
      <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
        <div className="min-w-0 space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <p className="truncate text-base font-semibold text-foreground">{user.displayName}</p>
            <Badge variant={user.isProtected ? "default" : "outline"}>{user.systemRole}</Badge>
            <Badge variant="neutral">{stateLabel}</Badge>
          </div>
          <div className="flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted-foreground">
            <span>{user.email}</span>
            <span>{user.membershipCount} vínculo(s)</span>
            <span>{user.ownedHouseholdCount} casa(s) própria(s)</span>
          </div>
          {scheduledDeletionLabel ? (
            <p className="text-xs text-danger">Exclusão agendada para {scheduledDeletionLabel}</p>
          ) : null}
        </div>

        <div className="flex flex-wrap gap-2">
          {user.isProtected ? (
            <Badge variant="neutral">Conta protegida</Badge>
          ) : user.accountState === "DisabledBySuperAdmin" ? (
            <Button variant="secondary" onClick={() => void onReactivate(user.id)} disabled={busy}>
              {busy ? <Loader2 className="animate-spin" /> : <UserCheck />}
              Reativar
            </Button>
          ) : (
            <Button variant="secondary" onClick={() => void onDeactivate(user.id)} disabled={busy}>
              {busy ? <Loader2 className="animate-spin" /> : <ShieldOff />}
              Desativar
            </Button>
          )}
          {!user.isProtected ? (
            <Button variant="danger" onClick={() => onDelete(user)} disabled={busy}>
              <Trash2 />
              Excluir
            </Button>
          ) : null}
        </div>
      </div>
    </div>
  );
}

function MetricCard({ label, value }: { label: string; value: number }) {
  return (
    <Card>
      <CardContent className="p-4">
        <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">{label}</p>
        <p className="mt-2 text-3xl font-semibold text-foreground">{value}</p>
      </CardContent>
    </Card>
  );
}

function AdminAccessDenied() {
  return (
    <main className="grid min-h-screen place-items-center bg-background px-4">
      <Card className="w-full max-w-lg">
        <CardHeader>
          <CardTitle>Acesso restrito</CardTitle>
          <CardDescription>Somente o perfil SuperAdmin pode acessar a gestão global de usuários.</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-wrap gap-2">
          <Button asChild>
            <Link href="/projects">Voltar ao sistema</Link>
          </Button>
          <Button variant="ghost" onClick={() => clearSession()}>
            <LogOut />
            Sair
          </Button>
        </CardContent>
      </Card>
    </main>
  );
}

"use client";

import Link from "next/link";
import {
  ArrowLeft,
  ArrowRight,
  Home,
  Globe2,
  Layers,
  Leaf,
  LogOut,
  Menu,
  MoonStar,
  Palette,
  Pencil,
  Plus,
  Loader2,
  RefreshCw,
  Repeat2,
  Share2,
  ShieldCheck,
  ShoppingCart,
  Smartphone,
  Sparkles,
  Trash2,
  UserPlus,
  Users,
  UserRound,
  Wallet,
  X,
} from "lucide-react";
import { FormEvent, useState } from "react";
import type { AuthResponse, Household, HouseholdMember, User } from "@/lib/api";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Sheet, SheetContent } from "@/components/ui/sheet";
import { cn } from "@/lib/utils";
import { DeleteConfirmationDialog } from "./delete-confirmation-dialog";
import { HouseholdMemberAvatar, ProtectedUserAvatar } from "./protected-user-avatar";

type WorkspaceTheme = "cozy" | "earthy" | "dark";
type ActiveModule = "projects" | "prompts" | "household" | "gsm" | "finance" | "profile" | "admin-users";

type ThemeOption = { value: WorkspaceTheme; label: string };

export type HomePitWorkspaceController = {
  session: AuthResponse | null;
  activeHouseholdId: string;
  activeHousehold: Household | null;
  members: HouseholdMember[];
  theme: WorkspaceTheme;
  sidebarCollapsed: boolean;
  loading: boolean;
  error: string | null;
  canShareHousehold: boolean;
  canManageHousehold: boolean;
  editingHousehold: Household | null;
  isHouseholdDialogOpen: boolean;
  isShareDialogOpen: boolean;
  setError: (error: string | null) => void;
  setSidebarCollapsed: (collapsed: boolean) => void;
  setTheme: (theme: WorkspaceTheme) => void;
  handleHouseholdChange: (householdId: string) => void;
  handleLogout: () => void;
  refreshHouseholds: () => Promise<void>;
  refreshWorkspace: () => Promise<void>;
  openCreateHousehold: () => void;
  openEditHousehold: () => void;
  openShareHousehold: () => void;
  closeCommonModal: () => void;
  createHousehold: (name: string) => Promise<void>;
  updateHousehold: (householdId: string, name: string) => Promise<void>;
  deleteHousehold: (household: Household) => Promise<void>;
  shareHousehold: (input: { email: string; role: "Admin" | "Member" }) => Promise<void>;
};

export type HeaderStatItem = {
  label: string;
  value: number;
};

const moduleIcons = {
  projects: Layers,
  prompts: Sparkles,
  household: ShieldCheck,
  gsm: Smartphone,
  profile: UserRound,
  "admin-users": Users,
  market: ShoppingCart,
  finance: Wallet,
  routines: Repeat2,
  institutional: Globe2,
};

const modules = [
  { key: "projects", label: "Projetos", href: "/projects", state: "active" as const, superAdminOnly: false },
  { key: "prompts", label: "Prompts", href: "/prompts", state: "active" as const, superAdminOnly: false },
  { key: "household", label: "Casa", href: "/household", state: "active" as const, superAdminOnly: false },
  { key: "gsm", label: "GSM", href: "/gsm", state: "active" as const, superAdminOnly: false },
  { key: "profile", label: "Perfil", href: "/profile", state: "active" as const, superAdminOnly: false },
  { key: "institutional", label: "Site institucional", href: "/admin/institutional", state: "active" as const, superAdminOnly: true },
  { key: "admin-users", label: "Usuários", href: "/admin/users", state: "active" as const, superAdminOnly: true },
  { key: "market", label: "Mercado", href: "#", state: "roadmap" as const, superAdminOnly: false },
  { key: "finance", label: "Financeiro", href: "/finance", state: "active" as const, superAdminOnly: false },
  { key: "routines", label: "Rotinas", href: "#", state: "roadmap" as const, superAdminOnly: false },
];

const roleLabels: Record<Household["role"], string> = {
  Owner: "Proprietário",
  Admin: "Administrador",
  Member: "Membro",
};

const themeOptions: ThemeOption[] = [
  { value: "cozy", label: "Atual" },
  { value: "earthy", label: "Terroso" },
  { value: "dark", label: "Escuro" },
];

const themeIcons: Record<WorkspaceTheme, typeof Sparkles> = {
  cozy: Sparkles,
  earthy: Leaf,
  dark: MoonStar,
};

export function HomePitWorkspaceShell({
  controller,
  activeModule,
  subtitle,
  visibleCount,
  visibleLabel = "visíveis",
  headerStats,
  requireHousehold = true,
  children,
}: {
  controller: HomePitWorkspaceController;
  activeModule: ActiveModule;
  subtitle: string;
  visibleCount: number;
  visibleLabel?: string;
  headerStats: HeaderStatItem[];
  requireHousehold?: boolean;
  children: React.ReactNode;
}) {
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false);
  const [deleteHouseholdId, setDeleteHouseholdId] = useState<string | null>(null);
  const householdToDelete =
    deleteHouseholdId && deleteHouseholdId === controller.activeHousehold?.id ? controller.activeHousehold : null;

  return (
    <div className="min-h-screen bg-background lg:flex">
      <aside
        className={cn(
          "hidden shrink-0 border-r border-sidebar-border bg-sidebar lg:flex lg:h-screen lg:sticky lg:top-0",
          controller.sidebarCollapsed ? "w-[96px]" : "w-[292px]",
        )}
      >
        <SidebarContent
          controller={controller}
          collapsed={controller.sidebarCollapsed}
          activeModule={activeModule}
          onRequestDeleteHousehold={() => setDeleteHouseholdId(controller.activeHousehold?.id ?? null)}
        />
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <TopBar
          controller={controller}
          activeModule={activeModule}
          subtitle={subtitle}
          visibleCount={visibleCount}
          visibleLabel={visibleLabel}
          headerStats={headerStats}
          onOpenSidebar={() => setMobileSidebarOpen(true)}
        />

        <main className="flex flex-1 flex-col gap-3 p-3 sm:p-4">
          {controller.error ? (
            <div className="flex items-center justify-between gap-3 rounded-[20px] border border-danger/20 bg-status-danger-soft px-4 py-3 text-sm text-danger shadow-xs">
              <span>{controller.error}</span>
              <Button variant="ghost" size="icon" onClick={() => controller.setError(null)} aria-label="Fechar erro">
                <X />
              </Button>
            </div>
          ) : null}

          {requireHousehold && !controller.activeHouseholdId ? (
            <NoHouseholdState
              loading={controller.loading}
              onCreateHousehold={controller.openCreateHousehold}
              onRefreshHouseholds={() => void controller.refreshHouseholds()}
            />
          ) : (
            children
          )}
        </main>
      </div>

      <Sheet open={mobileSidebarOpen} onOpenChange={setMobileSidebarOpen}>
        <SheetContent side="left" className="p-0 lg:hidden">
        <SidebarContent
          controller={controller}
          collapsed={false}
          activeModule={activeModule}
          onRequestDeleteHousehold={() => setDeleteHouseholdId(controller.activeHousehold?.id ?? null)}
        />
        </SheetContent>
      </Sheet>

      <HouseholdDialog
        key={`household-${controller.editingHousehold?.id ?? "new"}-${controller.isHouseholdDialogOpen ? "open" : "closed"}`}
        household={controller.editingHousehold}
        open={controller.isHouseholdDialogOpen}
        onOpenChange={(open) => !open && controller.closeCommonModal()}
        onSave={(name) =>
          controller.editingHousehold
            ? controller.updateHousehold(controller.editingHousehold.id, name)
            : controller.createHousehold(name)
        }
      />

      <ShareDialog
        key={`share-${controller.members.length}-${controller.isShareDialogOpen ? "open" : "closed"}`}
        open={controller.isShareDialogOpen}
        members={controller.members}
        token={controller.session?.accessToken}
        householdId={controller.activeHouseholdId}
        canShare={Boolean(controller.canShareHousehold)}
        onOpenChange={(open) => !open && controller.closeCommonModal()}
        onShare={controller.shareHousehold}
      />

      <DeleteConfirmationDialog
        key={`household-delete-${deleteHouseholdId ?? "none"}-${householdToDelete?.id ?? "none"}`}
        open={Boolean(householdToDelete)}
        title="Excluir casa"
        description="Essa ação é permanente e remove toda a estrutura e o conteúdo vinculados a esta casa."
        confirmationTarget={householdToDelete?.name}
        confirmationLabel={`Digite o nome da casa, ${householdToDelete?.name ?? ""}, para confirmar`}
        confirmLabel="Excluir casa"
        impactItems={[
          "Todos os universos, projetos, atividades e pendências vinculados à casa.",
          "Comentários associados às atividades e o histórico operacional relacionado.",
          "Prompts, categorias e associações do banco de prompts desta casa.",
          "Membros, permissões, convites e preferências de notificação.",
        ]}
        onOpenChange={(open) => {
          if (!open) {
            setDeleteHouseholdId(null);
          }
        }}
        onConfirm={async () => {
          if (!householdToDelete) {
            return;
          }

          await controller.deleteHousehold(householdToDelete);
        }}
      />
    </div>
  );
}

function SidebarContent({
  controller,
  collapsed,
  activeModule,
  onRequestDeleteHousehold,
}: {
  controller: HomePitWorkspaceController;
  collapsed: boolean;
  activeModule: ActiveModule;
  onRequestDeleteHousehold: () => void;
}) {
  return (
    <div className="flex h-full w-full flex-col gap-3 p-3">
      <div className="rounded-[20px] border border-sidebar-border bg-surface-strong p-3 shadow-sm">
        <div className="flex items-center justify-between gap-3">
          <div className="flex min-w-0 items-center gap-3">
            <div className="grid size-10 shrink-0 place-items-center rounded-[14px] bg-primary text-primary-foreground shadow-sm">
              <Home className="size-4" />
            </div>
            {!collapsed ? (
              <div className="min-w-0">
                <div className="font-display text-[26px] leading-none">HomePit</div>
                <div className="mt-1 text-[11px] uppercase tracking-[0.16em] text-muted-foreground">Residencial OS</div>
              </div>
            ) : null}
          </div>

          <Button
            variant="ghost"
            size="icon"
            className="hidden lg:inline-flex"
            onClick={() => controller.setSidebarCollapsed(!controller.sidebarCollapsed)}
            aria-label={collapsed ? "Expandir menu" : "Recolher menu"}
            title={collapsed ? "Expandir menu" : "Recolher menu"}
          >
            {collapsed ? <ArrowRight /> : <ArrowLeft />}
          </Button>
        </div>
      </div>

      <Card>
        <CardContent className={cn("space-y-2 p-3", collapsed && "px-2")}>
          <p className={cn("px-1.5 text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground", collapsed && "sr-only")}>
            Módulos
          </p>
          {modules.map((module) => {
            if (module.superAdminOnly && controller.session?.user.systemRole !== "SuperAdmin") {
              return null;
            }

            const Icon = moduleIcons[module.key as keyof typeof moduleIcons];
            const isActive = module.key === activeModule;
            const isEnabled = module.state === "active";

            if (!isEnabled) {
              return (
                <button
                  key={module.key}
                  className={cn(
                    "flex w-full items-center gap-3 rounded-[16px] px-3 py-2.5 text-left text-muted-foreground transition hover:bg-surface-muted hover:text-foreground",
                    collapsed && "justify-center px-2",
                  )}
                  type="button"
                  disabled
                  title={`${module.label} em breve`}
                >
                  <Icon className="size-4 shrink-0" />
                  {!collapsed ? (
                    <>
                      <span className="min-w-0 flex-1 text-sm font-semibold">{module.label}</span>
                      <Badge variant="neutral">Em breve</Badge>
                    </>
                  ) : null}
                </button>
              );
            }

            return (
              <Link
                key={module.key}
                href={module.href}
                className={cn(
                  "flex w-full items-center gap-3 rounded-[16px] px-3 py-2.5 text-left transition",
                  isActive
                    ? "bg-highlight text-accent-foreground shadow-xs"
                    : "text-muted-foreground hover:bg-surface-muted hover:text-foreground",
                  collapsed && "justify-center px-2",
                )}
                title={module.label}
              >
                <Icon className="size-4 shrink-0" />
                {!collapsed ? <span className="min-w-0 flex-1 text-sm font-semibold">{module.label}</span> : null}
              </Link>
            );
          })}
        </CardContent>
      </Card>

      <Card>
        <CardContent className={cn("space-y-3 p-3", collapsed && "px-2")}>
          <div className="flex items-center justify-between gap-2">
            {!collapsed ? (
              <div>
                <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">Casa ativa</p>
                <p className="mt-1 text-sm text-muted-foreground">Contexto e permissões</p>
              </div>
            ) : null}
            {!collapsed && controller.activeHousehold ? (
              <Badge variant="outline">{roleLabels[controller.activeHousehold.role]}</Badge>
            ) : null}
          </div>

          {controller.session?.households.length ? (
            <Select
              value={controller.activeHouseholdId}
              onChange={(event) => controller.handleHouseholdChange(event.target.value)}
              aria-label="Casa ativa"
            >
              {controller.session.households.map((household) => (
                <option key={household.id} value={household.id}>
                  {household.name}
                </option>
              ))}
            </Select>
          ) : !collapsed ? (
            <Notice tone="warning">Aguardando convite ou criação da primeira casa.</Notice>
          ) : null}

          <div className={cn("grid gap-2", collapsed && "justify-items-center")}>
            <Button variant="secondary" className={cn(collapsed && "w-10 px-0")} onClick={controller.openCreateHousehold}>
              <Plus />
              {!collapsed ? "Nova casa" : null}
            </Button>

            {controller.activeHousehold ? (
              <div className={cn("flex gap-2", collapsed && "flex-col")}>
                <Button
                  variant="ghost"
                  size={collapsed ? "icon" : "default"}
                  onClick={controller.openEditHousehold}
                  disabled={!controller.canManageHousehold}
                  title="Editar casa"
                >
                  <Pencil />
                  {!collapsed ? "Editar" : null}
                </Button>
                <Button
                  variant="ghost"
                  size={collapsed ? "icon" : "default"}
                  onClick={onRequestDeleteHousehold}
                  disabled={!controller.canManageHousehold}
                  title="Excluir casa"
                >
                  <Trash2 />
                  {!collapsed ? "Excluir" : null}
                </Button>
              </div>
            ) : null}

            {!collapsed ? (
              <Button asChild variant="ghost">
                <Link href="/household">
                  <ShieldCheck />
                  Administração
                </Link>
              </Button>
            ) : null}
          </div>
        </CardContent>
      </Card>

      {controller.session ? (
        <div className="mt-auto border-t border-sidebar-border pt-3">
          <SidebarUserMenu
            user={controller.session.user}
            token={controller.session.accessToken}
            householdId={controller.activeHouseholdId}
            collapsed={collapsed}
            theme={controller.theme}
            onChangeTheme={controller.setTheme}
            onLogout={controller.handleLogout}
          />
        </div>
      ) : null}
    </div>
  );
}

function TopBar({
  controller,
  activeModule,
  subtitle,
  visibleCount,
  visibleLabel,
  headerStats,
  onOpenSidebar,
}: {
  controller: HomePitWorkspaceController;
  activeModule: ActiveModule;
  subtitle: string;
  visibleCount: number;
  visibleLabel: string;
  headerStats: HeaderStatItem[];
  onOpenSidebar: () => void;
}) {
  return (
    <header className="sticky top-0 z-10 border-b border-border/70 bg-surface-strong backdrop-blur-md">
      <div className="flex flex-wrap items-center gap-3 px-3 py-3 sm:px-4">
        <div className="flex min-w-0 flex-1 items-center gap-3">
          <Button variant="secondary" size="icon" className="lg:hidden" onClick={onOpenSidebar} aria-label="Abrir menu">
            <Menu />
          </Button>
          <div className="min-w-0 flex-1 space-y-1">
            <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
              <p className="truncate text-xl font-semibold text-foreground">
                {controller.activeHousehold?.name ?? "HomePit"}
              </p>
              <Badge variant="neutral">
                {visibleCount} {visibleLabel}
              </Badge>
              {headerStats.length > 0 ? <HeaderStatsInline stats={headerStats} /> : null}
            </div>
            <p className="truncate text-sm text-muted-foreground">{subtitle}</p>
          </div>
        </div>

        {controller.session ? (
          <div className="ml-auto flex items-center gap-2">
            {activeModule !== "household" ? (
              <Button asChild variant="secondary" className="hidden sm:inline-flex">
                <Link href="/household">
                  <ShieldCheck />
                  Casa
                </Link>
              </Button>
            ) : null}
            <MembersBar
              members={controller.members}
              currentUser={controller.session.user}
              token={controller.session.accessToken}
              householdId={controller.activeHouseholdId}
            />
            <Button
              variant="secondary"
              size="icon"
              onClick={() => (controller.activeHouseholdId ? void controller.refreshWorkspace() : void controller.refreshHouseholds())}
              disabled={controller.loading}
              aria-label={controller.loading ? "Atualizando dados" : "Atualizar dados"}
              title={controller.loading ? "Atualizando" : "Atualizar"}
            >
              <RefreshCw className={cn(controller.loading && "animate-spin")} />
            </Button>
            <Button
              variant="secondary"
              size="icon"
              onClick={controller.openShareHousehold}
              disabled={!controller.activeHousehold}
              aria-label="Compartilhar casa"
              title="Compartilhar"
            >
              <Share2 />
            </Button>
          </div>
        ) : null}
      </div>
    </header>
  );
}

export function HeaderStatsInline({ stats }: { stats: HeaderStatItem[] }) {
  return (
    <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-[11px] leading-5">
      {stats.map((stat, index) => (
        <span key={stat.label} className="inline-flex items-baseline text-foreground">
          {index > 0 ? <span className="mr-2 text-border">•</span> : null}
          <span className="font-semibold text-foreground">{stat.value} </span>
          <span className="text-muted-foreground">{stat.label.toLowerCase()}</span>
        </span>
      ))}
    </div>
  );
}

function MembersBar({
  members,
  currentUser,
  token,
  householdId,
}: {
  members: HouseholdMember[];
  currentUser: User;
  token: string;
  householdId?: string;
}) {
  if (members.length === 0) {
    return null;
  }

  const visibleMembers = members.slice(0, 6);
  const remainingCount = Math.max(0, members.length - visibleMembers.length);

  return (
    <div className="hidden items-center gap-1.5 lg:flex">
      {visibleMembers.map((member) => (
        <MemberAvatarPill
          key={member.id}
          member={member}
          currentUser={currentUser}
          token={token}
          householdId={householdId}
        />
      ))}
      {remainingCount > 0 ? <Badge variant="neutral">+{remainingCount}</Badge> : null}
    </div>
  );
}

function MemberAvatarPill({
  member,
  currentUser,
  token,
  householdId,
}: {
  member: HouseholdMember;
  currentUser: User;
  token: string;
  householdId?: string;
}) {
  return (
    <div className="group relative">
      {member.isCurrentUser ? (
        <ProtectedUserAvatar
          user={currentUser}
          token={token}
          householdId={householdId}
          className="size-9 border border-primary/30 bg-highlight text-accent-foreground shadow-xs"
        />
      ) : (
        <HouseholdMemberAvatar
          member={member}
          token={token}
          householdId={householdId}
          className="size-9 border border-border/70 bg-surface text-[11px] font-semibold text-foreground shadow-xs"
        />
      )}

      <div className="pointer-events-none absolute right-0 top-full z-20 mt-2 w-56 rounded-[16px] border border-border/70 bg-popover p-3 opacity-0 shadow-md transition group-hover:pointer-events-auto group-hover:opacity-100">
        <div className="space-y-1">
          <p className="truncate text-sm font-semibold text-foreground">{member.displayName}</p>
          <p className="truncate text-xs text-muted-foreground">{member.email}</p>
          <Badge variant="neutral">{roleLabels[member.role]}</Badge>
        </div>
        {member.isCurrentUser ? (
          <div className="mt-3">
            <Button asChild variant="secondary" size="sm" className="w-full">
              <Link href="/profile">
                <Pencil />
                Editar perfil
              </Link>
            </Button>
          </div>
        ) : null}
      </div>
    </div>
  );
}

function SidebarUserMenu({
  user,
  token,
  householdId,
  collapsed,
  theme,
  onChangeTheme,
  onLogout,
}: {
  user: User;
  token: string;
  householdId?: string;
  collapsed: boolean;
  theme: WorkspaceTheme;
  onChangeTheme: (theme: WorkspaceTheme) => void;
  onLogout: () => void;
}) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          className={cn(
            "flex w-full items-center gap-2.5 rounded-[16px] px-3 py-2.5 text-left text-sm font-semibold text-foreground transition hover:bg-surface-muted",
            collapsed && "justify-center px-2",
          )}
          type="button"
          aria-label="Menu do usuário"
        >
          <ProtectedUserAvatar
            user={user}
            token={token}
            householdId={householdId}
            className="size-9 border border-border/70 bg-surface text-foreground"
          />
          {!collapsed ? <span className="truncate">{user.displayName}</span> : null}
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align={collapsed ? "center" : "end"}>
        <DropdownMenuLabel>{user.displayName}</DropdownMenuLabel>
        <div className="px-3 pb-2 text-xs text-muted-foreground">{user.email}</div>
        <DropdownMenuItem asChild>
          <Link href="/profile">
            <Pencil className="size-4" />
            Editar perfil
          </Link>
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuLabel>Tema</DropdownMenuLabel>
        {themeOptions.map((option) => {
          const Icon = themeIcons[option.value];

          return (
            <DropdownMenuItem key={option.value} onClick={() => onChangeTheme(option.value)}>
              <Icon className="size-4" />
              <span className="flex-1">{option.label}</span>
              {option.value === theme ? <Palette className="size-4 text-primary" /> : null}
            </DropdownMenuItem>
          );
        })}
        <DropdownMenuSeparator />
        <DropdownMenuItem className="text-danger focus:text-danger" onClick={onLogout}>
          <LogOut className="size-4" />
          Sair
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function NoHouseholdState({
  loading,
  onCreateHousehold,
  onRefreshHouseholds,
}: {
  loading: boolean;
  onCreateHousehold: () => void;
  onRefreshHouseholds: () => void;
}) {
  return (
    <Card>
      <CardContent className="grid gap-4 p-5 sm:p-6 lg:grid-cols-[auto_minmax(0,1fr)_auto] lg:items-center">
        <div className="grid size-14 place-items-center rounded-[18px] bg-accent text-accent-foreground">
          <Home className="size-6" />
        </div>
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">Conta pronta</p>
          <h2 className="mt-1 text-2xl font-semibold text-foreground">Sem casa vinculada</h2>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground">
            Crie a primeira casa agora ou atualize a lista quando alguém compartilhar uma estrutura com seu e-mail.
          </p>
        </div>
        <div className="flex flex-col gap-2 sm:flex-row lg:flex-col">
          <Button onClick={onCreateHousehold}>
            <Plus />
            Criar casa
          </Button>
          <Button variant="secondary" onClick={onRefreshHouseholds} disabled={loading}>
            <RefreshCw className={cn(loading && "animate-spin")} />
            Atualizar convites
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

function HouseholdDialog({
  household,
  open,
  onOpenChange,
  onSave,
}: {
  household: Household | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSave: (name: string) => Promise<void>;
}) {
  const [name, setName] = useState(household?.name ?? "");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const isEditing = Boolean(household);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSaving(true);

    try {
      await onSave(name);
      onOpenChange(false);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível salvar a casa.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{isEditing ? "Editar casa" : "Nova casa"}</DialogTitle>
          <DialogDescription>Defina o nome da casa que agrupa universos, projetos, prompts e membros.</DialogDescription>
        </DialogHeader>
        <form className="space-y-4" onSubmit={submit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}
          <Field label="Nome da casa">
            <Input value={name} onChange={(event) => setName(event.target.value)} autoFocus required />
          </Field>
          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saving}>
              {isEditing ? "Salvar casa" : "Criar casa"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function ShareDialog({
  members,
  token,
  householdId,
  canShare,
  open,
  onOpenChange,
  onShare,
}: {
  members: HouseholdMember[];
  token?: string;
  householdId?: string;
  canShare: boolean;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onShare: (input: { email: string; role: "Admin" | "Member" }) => Promise<void>;
}) {
  const [email, setEmail] = useState("");
  const [role, setRole] = useState<"Admin" | "Member">("Member");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSaving(true);

    try {
      await onShare({ email, role });
      setEmail("");
      onOpenChange(false);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível compartilhar a casa.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Compartilhar casa</DialogTitle>
          <DialogDescription>Convide outras pessoas e mantenha as permissões claras desde o início.</DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="rounded-[18px] border border-border/60 bg-surface-muted p-4">
            <div className="mb-3 flex items-center gap-2">
              <ShieldCheck className="size-4 text-primary" />
              <span className="text-sm font-semibold text-foreground">Perfis e permissões</span>
            </div>
            <div className="grid gap-3 text-sm leading-6 text-muted-foreground">
              <PermissionItem
                role="Proprietário"
                text="Edita ou exclui a casa, gerencia entidades e remove comentários de qualquer pessoa."
              />
              <PermissionItem
                role="Administrador"
                text="Cria e gerencia entidades da casa, inclusive de terceiros, e remove comentários."
              />
              <PermissionItem
                role="Membro"
                text="Cria conteúdo e gerencia apenas o que foi criado por ele."
              />
            </div>
          </div>

          <div className="space-y-3">
            <h3 className="text-sm font-semibold text-foreground">Membros atuais</h3>
            <div className="space-y-2">
              {members.map((member) => (
                <div
                  key={member.id}
                  className="flex items-center justify-between gap-3 rounded-[16px] border border-border/60 bg-surface-strong px-4 py-3"
                >
                  <div className="flex min-w-0 items-center gap-3">
                    <HouseholdMemberAvatar
                      member={member}
                      token={token}
                      householdId={householdId}
                      className="size-10"
                    />
                    <div className="min-w-0">
                      <p className="truncate text-sm font-semibold text-foreground">{member.displayName}</p>
                      <p className="truncate text-xs text-muted-foreground">{member.email}</p>
                    </div>
                  </div>
                  <Badge variant="neutral">{roleLabels[member.role]}</Badge>
                </div>
              ))}
            </div>
          </div>

          <form className="space-y-4" onSubmit={submit}>
            {error ? <Notice tone="danger">{error}</Notice> : null}
            {!canShare ? <Notice tone="warning">Seu perfil atual não permite adicionar pessoas.</Notice> : null}
            <Field label="E-mail">
              <Input
                type="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                disabled={!canShare}
                required
              />
            </Field>
            <Field label="Perfil">
              <Select value={role} onChange={(event) => setRole(event.target.value as "Admin" | "Member")} disabled={!canShare}>
                <option value="Member">Membro</option>
                <option value="Admin">Administrador</option>
              </Select>
            </Field>
            <DialogFooter>
              <Button variant="secondary" type="button" onClick={() => onOpenChange(false)}>
                Fechar
              </Button>
              <Button type="submit" disabled={saving || !canShare}>
                <UserPlus />
                Adicionar pessoa
              </Button>
            </DialogFooter>
          </form>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function PermissionItem({ role, text }: { role: string; text: string }) {
  return (
    <div className="rounded-[16px] border border-border/60 bg-surface-strong px-4 py-3">
      <p className="text-sm font-semibold text-foreground">{role}</p>
      <p className="mt-1 text-sm text-muted-foreground">{text}</p>
    </div>
  );
}

export function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="grid gap-2">
      <span className="text-sm font-semibold text-foreground/85">{label}</span>
      {children}
    </label>
  );
}

export function Notice({
  tone,
  children,
}: {
  tone: "danger" | "warning";
  children: React.ReactNode;
}) {
  return (
    <div
      className={cn(
        "rounded-[20px] border px-4 py-3 text-sm",
        tone === "danger" && "border-danger/20 bg-status-danger-soft text-danger",
        tone === "warning" && "border-warning/20 bg-status-warning-soft text-warning",
      )}
    >
      {children}
    </div>
  );
}

export function EmptyState({
  icon,
  title,
  description,
  action,
}: {
  icon: React.ReactNode;
  title: string;
  description: string;
  action?: React.ReactNode;
}) {
  return (
    <div className="grid min-h-[220px] place-items-center rounded-[22px] border border-dashed border-border/70 bg-surface-muted p-6 text-center">
      <div className="max-w-md">
        <div className="mx-auto mb-4 grid size-14 place-items-center rounded-[18px] bg-surface-strong text-accent-foreground shadow-xs">
          {icon}
        </div>
        <h3 className="text-lg font-semibold text-foreground">{title}</h3>
        <p className="mt-2 text-sm leading-6 text-muted-foreground">{description}</p>
        {action ? <div className="mt-5 flex justify-center">{action}</div> : null}
      </div>
    </div>
  );
}

export function LoadingState({
  title,
  description,
  icon = <Loader2 className="size-5 animate-spin" />,
}: {
  title: string;
  description: string;
  icon?: React.ReactNode;
}) {
  return (
    <div
      className="grid min-h-[220px] place-items-center rounded-[22px] border border-dashed border-border/70 bg-surface-muted p-6 text-center"
      role="status"
      aria-busy="true"
    >
      <div className="max-w-md">
        <div className="mx-auto mb-4 grid size-14 place-items-center rounded-[18px] bg-surface-strong text-accent-foreground shadow-xs">
          {icon}
        </div>
        <h3 className="text-lg font-semibold text-foreground">{title}</h3>
        <p className="mt-2 text-sm leading-6 text-muted-foreground">{description}</p>
      </div>
    </div>
  );
}


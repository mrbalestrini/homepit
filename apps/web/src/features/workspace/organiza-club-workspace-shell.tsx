"use client";

import Image from "next/image";
import Link from "next/link";
import {
  ArrowLeft,
  ArrowRight,
  Globe2,
  Layers,
  LogOut,
  Menu,
  Monitor,
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
  Lightbulb,
  NotebookPen,
  SunMedium,
  Trash2,
  UserPlus,
  Users,
  Wallet,
  X,
  type LucideIcon,
} from "lucide-react";
import { FormEvent, useState } from "react";
import { toast } from "sonner";
import { apiFetch, type AuthResponse, type CreateToolImprovementSuggestionRequest, type Space, type SpaceMember, type User } from "@/lib/api";
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
import { Sheet, SheetContent, SheetDescription, SheetTitle } from "@/components/ui/sheet";
import { Textarea } from "@/components/ui/textarea";
import { cn } from "@/lib/utils";
import { DeleteConfirmationDialog } from "./delete-confirmation-dialog";
import { SpaceMemberAvatar, ProtectedUserAvatar } from "./protected-user-avatar";

type WorkspaceTheme = "light" | "system" | "dark";
type ActiveModule = "projects" | "prompts" | "space" | "gsm" | "finance" | "profile" | "platform";

type ThemeOption = { value: WorkspaceTheme; label: string };

export type OrganizaClubWorkspaceController = {
  session: AuthResponse | null;
  activeSpaceId: string;
  activeSpace: Space | null;
  members: SpaceMember[];
  theme: WorkspaceTheme;
  sidebarCollapsed: boolean;
  loading: boolean;
  error: string | null;
  canShareSpace: boolean;
  canManageSpace: boolean;
  editingSpace: Space | null;
  isSpaceDialogOpen: boolean;
  isShareDialogOpen: boolean;
  setError: (error: string | null) => void;
  setSidebarCollapsed: (collapsed: boolean) => void;
  setTheme: (theme: WorkspaceTheme) => void;
  handleSpaceChange: (spaceId: string) => void;
  handleLogout: () => void;
  refreshSpaces: () => Promise<void>;
  refreshWorkspace: () => Promise<void>;
  openCreateSpace: () => void;
  openEditSpace: () => void;
  openShareSpace: () => void;
  closeCommonModal: () => void;
  createSpace: (name: string) => Promise<void>;
  updateSpace: (spaceId: string, name: string) => Promise<void>;
  deleteSpace: (space: Space) => Promise<void>;
  shareSpace: (input: { email: string; role: "Admin" | "Member" }) => Promise<void>;
};

export type HeaderStatItem = {
  label: string;
  value: number;
};

const moduleIcons = {
  projects: Layers,
  prompts: NotebookPen,
  space: ShieldCheck,
  gsm: Smartphone,
  market: ShoppingCart,
  finance: Wallet,
  routines: Repeat2,
};

const modules = [
  { key: "space", label: "Espaços", href: "/spaces", state: "active" as const, superAdminOnly: false },
  { key: "routines", label: "Rotinas", href: "#", state: "roadmap" as const, superAdminOnly: false },
  { key: "projects", label: "Núcleos e projetos", href: "/projects", state: "active" as const, superAdminOnly: false },
  { key: "finance", label: "Financeiro", href: "/finance", state: "active" as const, superAdminOnly: false },
  { key: "market", label: "Mercado", href: "#", state: "roadmap" as const, superAdminOnly: false },
  { key: "gsm", label: "GSM", href: "/gsm", state: "active" as const, superAdminOnly: false },
  { key: "prompts", label: "Prompts", href: "/prompts", state: "active" as const, superAdminOnly: false },
];

const roleLabels: Record<Space["role"], string> = {
  Owner: "Proprietário",
  Admin: "Administrador",
  Member: "Membro",
};

const themeOptions: ThemeOption[] = [
  { value: "system", label: "Sistema" },
  { value: "light", label: "Claro" },
  { value: "dark", label: "Escuro" },
];

const themeIcons: Record<WorkspaceTheme, LucideIcon> = {
  system: Monitor,
  light: SunMedium,
  dark: MoonStar,
};

export function OrganizaClubWorkspaceShell({
  controller,
  activeModule,
  subtitle,
  visibleCount,
  visibleLabel = "visíveis",
  headerStats,
  requireSpace = true,
  children,
}: {
  controller: OrganizaClubWorkspaceController;
  activeModule: ActiveModule;
  subtitle: string;
  visibleCount: number;
  visibleLabel?: string;
  headerStats: HeaderStatItem[];
  requireSpace?: boolean;
  children: React.ReactNode;
}) {
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false);
  const [deleteSpaceId, setDeleteSpaceId] = useState<string | null>(null);
  const [toolImprovementDialogOpen, setToolImprovementDialogOpen] = useState(false);
  const [toolImprovementText, setToolImprovementText] = useState("");
  const [submittingToolImprovement, setSubmittingToolImprovement] = useState(false);
  const spaceToDelete =
    deleteSpaceId && deleteSpaceId === controller.activeSpace?.id ? controller.activeSpace : null;

  async function submitToolImprovementSuggestion(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const suggestionText = toolImprovementText.trim();
    if (!suggestionText || !controller.session) {
      return;
    }

    setSubmittingToolImprovement(true);

    try {
      const payload: CreateToolImprovementSuggestionRequest = { suggestionText };
      await apiFetch("/api/users/me/tool-improvement-suggestions", {
        method: "POST",
        token: controller.session.accessToken,
        body: JSON.stringify(payload),
      });
      setToolImprovementText("");
      setToolImprovementDialogOpen(false);
      toast.success("Sugestão enviada. Obrigado por ajudar a melhorar a ferramenta.");
    } catch (exception) {
      toast.error(exception instanceof Error ? exception.message : "Não foi possível enviar a sugestão.");
    } finally {
      setSubmittingToolImprovement(false);
    }
  }

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
          onOpenToolImprovementSuggestion={() => setToolImprovementDialogOpen(true)}
          onRequestDeleteSpace={() => setDeleteSpaceId(controller.activeSpace?.id ?? null)}
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

          {requireSpace && !controller.activeSpaceId ? (
            <NoSpaceState
              loading={controller.loading}
              onCreateSpace={controller.openCreateSpace}
              onRefreshSpaces={() => void controller.refreshSpaces()}
            />
          ) : (
            children
          )}
        </main>
      </div>

      <Sheet open={mobileSidebarOpen} onOpenChange={setMobileSidebarOpen}>
        <SheetContent side="left" className="p-0 lg:hidden">
          <SheetTitle className="sr-only">Navegação principal</SheetTitle>
          <SheetDescription className="sr-only">
            Acesse os módulos e as configurações do Espaço ativo.
          </SheetDescription>
          <SidebarContent
            controller={controller}
            collapsed={false}
            activeModule={activeModule}
            onOpenToolImprovementSuggestion={() => setToolImprovementDialogOpen(true)}
            onRequestDeleteSpace={() => setDeleteSpaceId(controller.activeSpace?.id ?? null)}
          />
        </SheetContent>
      </Sheet>

      <SpaceDialog
        key={`space-${controller.editingSpace?.id ?? "new"}-${controller.isSpaceDialogOpen ? "open" : "closed"}`}
        space={controller.editingSpace}
        open={controller.isSpaceDialogOpen}
        onOpenChange={(open) => !open && controller.closeCommonModal()}
        onSave={(name) =>
          controller.editingSpace
            ? controller.updateSpace(controller.editingSpace.id, name)
            : controller.createSpace(name)
        }
      />

      <ShareDialog
        key={`share-${controller.members.length}-${controller.isShareDialogOpen ? "open" : "closed"}`}
        open={controller.isShareDialogOpen}
        members={controller.members}
        token={controller.session?.accessToken}
        spaceId={controller.activeSpaceId}
        canShare={Boolean(controller.canShareSpace)}
        onOpenChange={(open) => !open && controller.closeCommonModal()}
        onShare={controller.shareSpace}
      />

      <Dialog
        open={toolImprovementDialogOpen}
        onOpenChange={(open) => {
          if (submittingToolImprovement) {
            return;
          }

          setToolImprovementDialogOpen(open);
          if (!open) {
            setToolImprovementText("");
          }
        }}
      >
        <DialogContent className="w-[min(94vw,42rem)] max-h-[88vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Sugestão de melhoria</DialogTitle>
            <DialogDescription>
              O sistema está no início e conta com suas sugestões para ficar cada vez melhor.
            </DialogDescription>
          </DialogHeader>
          <form className="grid gap-4" onSubmit={(event) => void submitToolImprovementSuggestion(event)}>
            <Notice tone="warning">
              Se puder, detalhe módulo, seção e funcionalidades envolvidas. Quanto mais contexto, mais fácil entender a
              melhoria e evoluir a ferramenta.
            </Notice>
            <Field label="Sua sugestão">
              <Textarea
                value={toolImprovementText}
                onChange={(event) => setToolImprovementText(event.target.value)}
                placeholder="Descreva a melhoria sugerida"
                rows={8}
                maxLength={8000}
                aria-label="Sua sugestão"
              />
            </Field>
            <DialogFooter>
              <Button
                type="button"
                variant="secondary"
                onClick={() => {
                  setToolImprovementDialogOpen(false);
                  setToolImprovementText("");
                }}
                disabled={submittingToolImprovement}
              >
                Cancelar
              </Button>
              <Button type="submit" disabled={submittingToolImprovement || toolImprovementText.trim() === ""}>
                {submittingToolImprovement ? <Loader2 className="animate-spin" /> : <Lightbulb />}
                Enviar sugestão
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <DeleteConfirmationDialog
        key={`space-delete-${deleteSpaceId ?? "none"}-${spaceToDelete?.id ?? "none"}`}
        open={Boolean(spaceToDelete)}
        title="Excluir espaço"
        description="Essa ação é permanente e remove toda a estrutura e o conteúdo vinculados a este espaço."
        confirmationTarget={spaceToDelete?.name}
        confirmationLabel={`Digite o nome do espaço, ${spaceToDelete?.name ?? ""}, para confirmar`}
        confirmLabel="Excluir espaço"
        impactItems={[
          "Todos os núcleos, projetos, atividades e pendências vinculados ao espaço.",
          "Comentários associados às atividades e o histórico operacional relacionado.",
          "Prompts, categorias e associações do banco de prompts deste espaço.",
          "Membros, permissões, convites e preferências de notificação.",
        ]}
        onOpenChange={(open) => {
          if (!open) {
            setDeleteSpaceId(null);
          }
        }}
        onConfirm={async () => {
          if (!spaceToDelete) {
            return;
          }

          await controller.deleteSpace(spaceToDelete);
        }}
      />
    </div>
  );
}

function SidebarContent({
  controller,
  collapsed,
  activeModule,
  onOpenToolImprovementSuggestion,
  onRequestDeleteSpace,
}: {
  controller: OrganizaClubWorkspaceController;
  collapsed: boolean;
  activeModule: ActiveModule;
  onOpenToolImprovementSuggestion: () => void;
  onRequestDeleteSpace: () => void;
}) {
  return (
    <div className="flex h-full w-full flex-col gap-3 p-3">
      <div className="rounded-[20px] border border-sidebar-border bg-surface-strong p-3 shadow-sm">
        <div className="flex items-center justify-between gap-3">
          <div className="flex min-w-0 items-center gap-3">
            <Image className="size-10 shrink-0" src="/brand/organiza-club-mark-navy.svg" alt="" width={40} height={40} />
            {!collapsed ? (
              <div className="min-w-0">
                <span className="relative block">
                  <Image className="brand-logo-navy h-7 w-auto" src="/brand/organiza-club-wordmark-navy.svg" alt="Organiza Club" width={202} height={70} />
                  <Image className="brand-logo-cream h-7 w-auto" src="/brand/organiza-club-wordmark-cream.svg" alt="Organiza Club" width={202} height={70} />
                </span>
                <div className="mt-1 text-[11px] uppercase tracking-[0.16em] text-muted-foreground">Controle tranquilo</div>
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

      {controller.activeSpaceId ? (
        <Card>
          <CardContent className={cn("space-y-2 p-3", collapsed && "px-2")}>
            <p
              className={cn(
                "px-1.5 text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground",
                collapsed && "sr-only",
              )}
            >
              Módulos
            </p>
            {modules.map((module) => {
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
      ) : null}

      <Card>
        <CardContent className={cn("space-y-3 p-3", collapsed && "px-2")}>
          <div className="flex items-center justify-between gap-2">
            {!collapsed ? (
              <div>
                <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">Espaço ativo</p>
                <p className="mt-1 text-sm text-muted-foreground">Contexto e permissões</p>
              </div>
            ) : null}
            {!collapsed && controller.activeSpace ? (
              <Badge variant="outline">{roleLabels[controller.activeSpace.role]}</Badge>
            ) : null}
          </div>

          {controller.session?.spaces.length ? (
            <Select
              value={controller.activeSpaceId}
              onChange={(event) => controller.handleSpaceChange(event.target.value)}
              aria-label="Espaço ativo"
            >
              {controller.session.spaces.map((space) => (
                <option key={space.id} value={space.id}>
                  {space.name}
                </option>
              ))}
            </Select>
          ) : !collapsed ? (
            <Notice tone="warning">Aguardando convite ou criação do primeiro espaço.</Notice>
          ) : null}

          <div className={cn("grid gap-2", collapsed && "justify-items-center")}>
            <Button variant="secondary" className={cn(collapsed && "w-10 px-0")} onClick={controller.openCreateSpace}>
              <Plus />
              {!collapsed ? "Novo espaço" : null}
            </Button>

            {controller.activeSpace ? (
              <div className={cn("flex gap-2", collapsed && "flex-col")}>
                <Button
                  variant="ghost"
                  size={collapsed ? "icon" : "default"}
                  onClick={controller.openEditSpace}
                  disabled={!controller.canManageSpace}
                  title="Editar espaço"
                >
                  <Pencil />
                  {!collapsed ? "Editar" : null}
                </Button>
                <Button
                  variant="ghost"
                  size={collapsed ? "icon" : "default"}
                  onClick={onRequestDeleteSpace}
                  disabled={!controller.canManageSpace}
                  title="Excluir espaço"
                >
                  <Trash2 />
                  {!collapsed ? "Excluir" : null}
                </Button>
              </div>
            ) : null}

            {!collapsed ? (
              <Button asChild variant="ghost">
                <Link href="/spaces">
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
            spaceId={controller.activeSpaceId}
            collapsed={collapsed}
            theme={controller.theme}
            onChangeTheme={controller.setTheme}
            onOpenToolImprovementSuggestion={onOpenToolImprovementSuggestion}
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
  controller: OrganizaClubWorkspaceController;
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
                {controller.activeSpace?.name ?? "Organiza Club"}
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
            {activeModule !== "space" ? (
              <Button asChild variant="secondary" className="hidden sm:inline-flex">
                <Link href="/spaces">
                  <ShieldCheck />
                  Espaços
                </Link>
              </Button>
            ) : null}
            <MembersBar
              members={controller.members}
              currentUser={controller.session.user}
              token={controller.session.accessToken}
              spaceId={controller.activeSpaceId}
            />
            <Button
              variant="secondary"
              size="icon"
              onClick={() => (controller.activeSpaceId ? void controller.refreshWorkspace() : void controller.refreshSpaces())}
              disabled={controller.loading}
              aria-label={controller.loading ? "Atualizando dados" : "Atualizar dados"}
              title={controller.loading ? "Atualizando" : "Atualizar"}
            >
              <RefreshCw className={cn(controller.loading && "animate-spin")} />
            </Button>
            <Button
              variant="secondary"
              size="icon"
              onClick={controller.openShareSpace}
              disabled={!controller.activeSpace}
              aria-label="Compartilhar espaço"
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
  spaceId,
}: {
  members: SpaceMember[];
  currentUser: User;
  token: string;
  spaceId?: string;
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
          spaceId={spaceId}
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
  spaceId,
}: {
  member: SpaceMember;
  currentUser: User;
  token: string;
  spaceId?: string;
}) {
  return (
    <div className="group relative">
      {member.isCurrentUser ? (
        <ProtectedUserAvatar
          user={currentUser}
          token={token}
          spaceId={spaceId}
          className="size-9 border border-primary/30 bg-highlight text-accent-foreground shadow-xs"
        />
      ) : (
        <SpaceMemberAvatar
          member={member}
          token={token}
          spaceId={spaceId}
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
                Perfil
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
  spaceId,
  collapsed,
  theme,
  onChangeTheme,
  onOpenToolImprovementSuggestion,
  onLogout,
}: {
  user: User;
  token: string;
  spaceId?: string;
  collapsed: boolean;
  theme: WorkspaceTheme;
  onChangeTheme: (theme: WorkspaceTheme) => void;
  onOpenToolImprovementSuggestion: () => void;
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
            spaceId={spaceId}
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
            Perfil
          </Link>
        </DropdownMenuItem>
        {user.systemRole === "SuperAdmin" ? (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuLabel>Administração</DropdownMenuLabel>
            <DropdownMenuItem asChild>
              <Link href="/admin/platform">
                <Users className="size-4" />
                Plataforma
              </Link>
            </DropdownMenuItem>
            <DropdownMenuItem asChild>
              <Link href="/admin/institutional">
                <Globe2 className="size-4" />
                Site institucional
              </Link>
            </DropdownMenuItem>
            <DropdownMenuItem asChild>
              <Link href="/admin/users">
                <ShieldCheck className="size-4" />
                Usuários
              </Link>
            </DropdownMenuItem>
          </>
        ) : null}
        <DropdownMenuItem onClick={onOpenToolImprovementSuggestion}>
          <Lightbulb className="size-4" />
          Sugestão melhoria ferramenta
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

function NoSpaceState({
  loading,
  onCreateSpace,
  onRefreshSpaces,
}: {
  loading: boolean;
  onCreateSpace: () => void;
  onRefreshSpaces: () => void;
}) {
  return (
    <Card>
      <CardContent className="grid gap-4 p-5 sm:p-6 lg:grid-cols-[auto_minmax(0,1fr)_auto] lg:items-center">
        <Image
          className="size-20 rounded-[18px] bg-accent p-2"
          src="/brand/organiza-club-host-navy.svg"
          alt="Anfitrião Organiza Club"
          width={80}
          height={80}
        />
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">Conta pronta</p>
          <h2 className="mt-1 text-2xl font-semibold text-foreground">Nenhum espaço por aqui ainda</h2>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground">
            Crie seu primeiro espaço ou atualize a lista quando alguém compartilhar um com seu e-mail.
          </p>
        </div>
        <div className="flex flex-col gap-2 sm:flex-row lg:flex-col">
          <Button onClick={onCreateSpace}>
            <Plus />
            Criar espaço
          </Button>
          <Button variant="secondary" onClick={onRefreshSpaces} disabled={loading}>
            <RefreshCw className={cn(loading && "animate-spin")} />
            Atualizar convites
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

function SpaceDialog({
  space,
  open,
  onOpenChange,
  onSave,
}: {
  space: Space | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSave: (name: string) => Promise<void>;
}) {
  const [name, setName] = useState(space?.name ?? "");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const isEditing = Boolean(space);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSaving(true);

    try {
      await onSave(name);
      onOpenChange(false);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível salvar o espaço.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{isEditing ? "Editar espaço" : "Novo espaço"}</DialogTitle>
          <DialogDescription>Defina o nome do espaço que agrupa núcleos, projetos, prompts e membros.</DialogDescription>
        </DialogHeader>
        <form className="space-y-4" onSubmit={submit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}
          <Field label="Nome do espaço">
            <Input value={name} onChange={(event) => setName(event.target.value)} autoFocus required />
          </Field>
          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saving}>
              {isEditing ? "Salvar espaço" : "Criar espaço"}
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
  spaceId,
  canShare,
  open,
  onOpenChange,
  onShare,
}: {
  members: SpaceMember[];
  token?: string;
  spaceId?: string;
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
      setError(exception instanceof Error ? exception.message : "Não foi possível compartilhar o espaço.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Compartilhar espaço</DialogTitle>
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
                text="Edita ou exclui o espaço, gerencia entidades e remove comentários de qualquer pessoa."
              />
              <PermissionItem
                role="Administrador"
                text="Cria e gerencia entidades do espaço, inclusive de terceiros, e remove comentários."
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
                    <SpaceMemberAvatar
                      member={member}
                      token={token}
                      spaceId={spaceId}
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

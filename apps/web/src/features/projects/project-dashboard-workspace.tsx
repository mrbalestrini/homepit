"use client";

import {
  closestCorners,
  DndContext,
  DragEndEvent,
  DragOverlay,
  DragStartEvent,
  KeyboardSensor,
  type Modifier,
  PointerSensor,
  useDroppable,
  useSensor,
  useSensors,
} from "@dnd-kit/core";
import { SortableContext, sortableKeyboardCoordinates, useSortable, verticalListSortingStrategy } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import {
  ArrowLeft,
  ArrowRight,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  ClipboardList,
  Filter,
  Folder,
  FolderPlus,
  GripVertical,
  Home,
  Layers,
  Leaf,
  ListFilter,
  LogOut,
  Menu,
  MoonStar,
  MoreHorizontal,
  Palette,
  Pencil,
  Plus,
  RefreshCw,
  Repeat2,
  Search,
  Share2,
  ShieldCheck,
  ShoppingCart,
  Sparkles,
  Table2,
  Trash2,
  UserPlus,
  Users,
  Wallet,
  X,
} from "lucide-react";
import { FormEvent, useEffect, useMemo, useState } from "react";
import type { Activity, ActivityComment, ActivityStatus, Household, HouseholdMember, Priority, Project, Universe, User } from "@/lib/api";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
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
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import { cn } from "@/lib/utils";
import {
  activityColumns,
  activitySortOptions,
  modules,
  priorityLabels,
  roleLabels,
  themeOptions,
  viewModeOptions,
} from "./project-dashboard.constants";
import type { ActivityFormInput, AppTheme, ProjectViewMode } from "./project-dashboard.types";
import { AvatarCircle, ProtectedUserAvatar } from "./protected-user-avatar";
import type { ProjectDashboardController } from "./use-project-dashboard";
import { formatDateTime, getInitials, getPriorityVariant } from "./project-dashboard.utils";

const moduleIcons = {
  projects: ClipboardList,
  market: ShoppingCart,
  finance: Wallet,
  routines: Repeat2,
};

const themeIcons: Record<AppTheme, typeof Sparkles> = {
  cozy: Sparkles,
  earthy: Leaf,
  dark: MoonStar,
};

const statusSectionStyles: Record<Activity["status"], { card: string; header: string; dropzone: string }> = {
  NaoIniciada: {
    card: "border-warning/25 bg-status-warning-soft",
    header: "bg-status-warning-soft",
    dropzone: "border-warning/25",
  },
  EmAndamento: {
    card: "border-success/25 bg-status-success-soft",
    header: "bg-status-success-soft",
    dropzone: "border-success/25",
  },
  Concluido: {
    card: "border-border/70 bg-status-neutral-soft",
    header: "bg-status-neutral-soft",
    dropzone: "border-border/70",
  },
};

const snapOverlayToCursor: Modifier = ({
  activatorEvent,
  draggingNodeRect,
  overlayNodeRect,
  transform,
}) => {
  if (
    !(activatorEvent instanceof MouseEvent || activatorEvent instanceof PointerEvent) ||
    !draggingNodeRect ||
    !overlayNodeRect
  ) {
    return transform;
  }

  return {
    ...transform,
    x: transform.x + activatorEvent.clientX - draggingNodeRect.left - overlayNodeRect.width / 2,
    y: transform.y + activatorEvent.clientY - draggingNodeRect.top - overlayNodeRect.height / 2,
  };
};

export function ProjectDashboardWorkspace({ dashboard }: { dashboard: ProjectDashboardController }) {
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false);
  const [profileDialogOpen, setProfileDialogOpen] = useState(false);

  return (
    <div className="min-h-screen bg-background lg:flex">
      <aside
        className={cn(
          "hidden shrink-0 border-r border-sidebar-border bg-sidebar lg:flex lg:h-screen lg:sticky lg:top-0",
          dashboard.sidebarCollapsed ? "w-[96px]" : "w-[292px]",
        )}
      >
        <SidebarContent
          dashboard={dashboard}
          collapsed={dashboard.sidebarCollapsed}
          onOpenProfile={() => setProfileDialogOpen(true)}
        />
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <TopBar
          dashboard={dashboard}
          onOpenSidebar={() => setMobileSidebarOpen(true)}
          onOpenProfile={() => setProfileDialogOpen(true)}
        />

        <main className="flex flex-1 flex-col gap-3 p-3 sm:p-4">
          {dashboard.error ? (
            <div className="flex items-center justify-between gap-3 rounded-[20px] border border-danger/20 bg-status-danger-soft px-4 py-3 text-sm text-danger shadow-xs">
              <span>{dashboard.error}</span>
              <Button variant="ghost" size="icon" onClick={() => dashboard.setError(null)} aria-label="Fechar erro">
                <X />
              </Button>
            </div>
          ) : null}

          {!dashboard.activeHouseholdId ? (
            <NoHouseholdState
              loading={dashboard.loading}
              onCreateHousehold={dashboard.openCreateHousehold}
              onRefreshHouseholds={() => void dashboard.refreshHouseholds()}
            />
          ) : (
            <>
              <QuickStats
                universes={dashboard.universes}
                projects={dashboard.projects}
                activities={dashboard.activities}
                members={dashboard.members}
              />

              <div className="grid gap-3 xl:grid-cols-[316px_minmax(0,1fr)]">
                <ProjectExplorer dashboard={dashboard} />
                <WorkspaceBoard dashboard={dashboard} />
              </div>
            </>
          )}
        </main>
      </div>

      <Sheet open={mobileSidebarOpen} onOpenChange={setMobileSidebarOpen}>
        <SheetContent side="left" className="p-0 lg:hidden">
          <SidebarContent
            dashboard={dashboard}
            collapsed={false}
            onOpenProfile={() => setProfileDialogOpen(true)}
          />
        </SheetContent>
      </Sheet>

      <HouseholdDialog
        key={`household-${dashboard.editingHousehold?.id ?? "new"}-${dashboard.activeModal === "household" ? "open" : "closed"}`}
        household={dashboard.editingHousehold}
        open={dashboard.activeModal === "household"}
        onOpenChange={(open) => !open && dashboard.closeModal()}
        onSave={(name) =>
          dashboard.editingHousehold
            ? dashboard.updateHousehold(dashboard.editingHousehold.id, name)
            : dashboard.createHousehold(name)
        }
      />

      <UniverseDialog
        key={`universe-${dashboard.editingUniverse?.id ?? "new"}-${dashboard.activeModal === "universe" ? "open" : "closed"}`}
        open={dashboard.activeModal === "universe"}
        universe={dashboard.editingUniverse}
        onOpenChange={(open) => !open && dashboard.closeModal()}
        onSave={(input) =>
          dashboard.editingUniverse
            ? dashboard.updateUniverse(dashboard.editingUniverse.id, input)
            : dashboard.createUniverse(input)
        }
      />

      <ProjectDialog
        key={`project-${dashboard.editingProject?.id ?? "new"}-${dashboard.activeModal === "project" ? "open" : "closed"}`}
        open={dashboard.activeModal === "project"}
        project={dashboard.editingProject}
        universes={dashboard.universes}
        defaultUniverseId={dashboard.selectedUniverseId}
        onOpenChange={(open) => !open && dashboard.closeModal()}
        onSave={(universeId, name) =>
          dashboard.editingProject
            ? dashboard.updateProject(dashboard.editingProject.id, universeId, name)
            : dashboard.createProject(universeId, name)
        }
      />

      <ActivityDialog
        key={`activity-${dashboard.editingActivity?.id ?? "new"}-${dashboard.activeModal === "activity" ? "open" : "closed"}`}
        open={dashboard.activeModal === "activity"}
        activity={dashboard.editingActivity}
        projects={dashboard.filteredProjects.length > 0 ? dashboard.filteredProjects : dashboard.projects}
        members={dashboard.members}
        defaultProjectId={dashboard.selectedProjectId}
        onOpenChange={(open) => !open && dashboard.closeModal()}
        onSave={(input) =>
          dashboard.editingActivity
            ? dashboard.updateActivity(dashboard.editingActivity.id, input)
            : dashboard.createActivity(input)
        }
      />

      <ShareDialog
        key={`share-${dashboard.members.length}-${dashboard.activeModal === "share" ? "open" : "closed"}`}
        open={dashboard.activeModal === "share"}
        members={dashboard.members}
        canShare={Boolean(dashboard.canShareHousehold)}
        onOpenChange={(open) => !open && dashboard.closeModal()}
        onShare={dashboard.shareHousehold}
      />

      {dashboard.session ? (
        <ProfileDialog
          key={`profile-${dashboard.session.user.id}-${dashboard.session.user.displayName}-${dashboard.session.user.phoneNumber ?? ""}-${dashboard.session.user.hasProfilePhoto ? "photo" : "no-photo"}-${dashboard.session.user.profilePhotoUpdatedAt ?? "none"}-${profileDialogOpen ? "open" : "closed"}`}
          open={profileDialogOpen}
          user={dashboard.session.user}
          token={dashboard.session.accessToken}
          onOpenChange={setProfileDialogOpen}
          onSave={dashboard.updateProfile}
        />
      ) : null}

      {dashboard.selectedActivity ? (
        <ActivityDetailsSheet
          activity={dashboard.selectedActivity}
          comments={dashboard.activityComments}
          commentsLoading={dashboard.commentsLoading}
          onClose={dashboard.closeActivity}
          onCreateComment={dashboard.createComment}
          onUpdateComment={dashboard.updateComment}
          onDeleteComment={dashboard.deleteComment}
          onMove={dashboard.moveActivity}
          onEditActivity={dashboard.openEditActivity}
          onDeleteActivity={dashboard.deleteActivity}
        />
      ) : null}
    </div>
  );
}

function SidebarContent({
  dashboard,
  collapsed,
  onOpenProfile,
}: {
  dashboard: ProjectDashboardController;
  collapsed: boolean;
  onOpenProfile: () => void;
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
            onClick={() => dashboard.setSidebarCollapsed(!dashboard.sidebarCollapsed)}
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
            const Icon = moduleIcons[module.key as keyof typeof moduleIcons];
            const active = module.key === "projects";

            return (
              <button
                key={module.key}
                className={cn(
                  "flex w-full items-center gap-3 rounded-[16px] px-3 py-2.5 text-left transition",
                  active
                    ? "bg-highlight text-accent-foreground shadow-xs"
                    : "text-muted-foreground hover:bg-surface-muted hover:text-foreground",
                  collapsed && "justify-center px-2",
                )}
                type="button"
                disabled={!active}
                title={active ? module.label : `${module.label} em breve`}
              >
                <Icon className="size-4 shrink-0" />
                {!collapsed ? (
                  <>
                    <span className="min-w-0 flex-1 text-sm font-semibold">{module.label}</span>
                    {!active ? <Badge variant="neutral">Em breve</Badge> : null}
                  </>
                ) : null}
              </button>
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
            {!collapsed && dashboard.activeHousehold ? (
              <Badge variant="outline">{roleLabels[dashboard.activeHousehold.role]}</Badge>
            ) : null}
          </div>

          {dashboard.session?.households.length ? (
            <Select
              value={dashboard.activeHouseholdId}
              onChange={(event) => dashboard.handleHouseholdChange(event.target.value)}
              aria-label="Casa ativa"
            >
              {dashboard.session.households.map((household) => (
                <option key={household.id} value={household.id}>
                  {household.name}
                </option>
              ))}
            </Select>
          ) : !collapsed ? (
            <Notice tone="warning">Aguardando convite ou criação da primeira casa.</Notice>
          ) : null}

          <div className={cn("grid gap-2", collapsed && "justify-items-center")}>
            <Button variant="secondary" className={cn(collapsed && "w-10 px-0")} onClick={dashboard.openCreateHousehold}>
              <Plus />
              {!collapsed ? "Nova casa" : null}
            </Button>

            {dashboard.activeHousehold ? (
              <div className={cn("flex gap-2", collapsed && "flex-col")}>
                <Button
                  variant="ghost"
                  size={collapsed ? "icon" : "default"}
                  onClick={dashboard.openEditHousehold}
                  disabled={!dashboard.canManageHousehold}
                  title="Editar casa"
                >
                  <Pencil />
                  {!collapsed ? "Editar" : null}
                </Button>
                <Button
                  variant="ghost"
                  size={collapsed ? "icon" : "default"}
                  onClick={() => {
                    if (dashboard.activeHousehold) {
                      void dashboard.deleteHousehold(dashboard.activeHousehold).catch(() => undefined);
                    }
                  }}
                  disabled={!dashboard.canManageHousehold}
                  title="Excluir casa"
                >
                  <Trash2 />
                  {!collapsed ? "Excluir" : null}
                </Button>
              </div>
            ) : null}
          </div>
        </CardContent>
      </Card>

      <div className="mt-auto border-t border-sidebar-border pt-3">
        <SidebarUserMenu
          user={dashboard.session!.user}
          token={dashboard.session!.accessToken}
          collapsed={collapsed}
          theme={dashboard.theme}
          onChangeTheme={dashboard.setTheme}
          onOpenProfile={onOpenProfile}
          onLogout={dashboard.handleLogout}
        />
      </div>
    </div>
  );
}

function TopBar({
  dashboard,
  onOpenSidebar,
  onOpenProfile,
}: {
  dashboard: ProjectDashboardController;
  onOpenSidebar: () => void;
  onOpenProfile: () => void;
}) {
  return (
    <header className="sticky top-0 z-10 border-b border-border/70 bg-surface-strong backdrop-blur-md">
      <div className="flex flex-wrap items-center gap-3 px-3 py-3 sm:px-4">
        <div className="flex min-w-0 flex-1 items-center gap-3">
          <Button variant="secondary" size="icon" className="lg:hidden" onClick={onOpenSidebar} aria-label="Abrir menu">
            <Menu />
          </Button>
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <p className="truncate text-xl font-semibold text-foreground">
                {dashboard.activeHousehold?.name ?? "HomePit"}
              </p>
              <Badge variant="neutral">{dashboard.visibleActivities.length} visíveis</Badge>
            </div>
            <p className="truncate text-sm text-muted-foreground">{dashboard.selectedScopeLabel}</p>
          </div>
        </div>

        <div className="ml-auto flex items-center gap-2">
          <MembersBar
            members={dashboard.members}
            currentUser={dashboard.session!.user}
            token={dashboard.session!.accessToken}
            onOpenProfile={onOpenProfile}
          />
          <Button
            variant="secondary"
            size="icon"
            onClick={() => (dashboard.activeHouseholdId ? void dashboard.loadWorkspace() : void dashboard.refreshHouseholds())}
            disabled={dashboard.loading}
            aria-label={dashboard.loading ? "Atualizando dados" : "Atualizar dados"}
            title={dashboard.loading ? "Atualizando" : "Atualizar"}
          >
            <RefreshCw className={cn(dashboard.loading && "animate-spin")} />
          </Button>
          <Button
            variant="secondary"
            size="icon"
            onClick={dashboard.openShareHousehold}
            disabled={!dashboard.activeHousehold}
            aria-label="Compartilhar casa"
            title="Compartilhar"
          >
            <Share2 />
          </Button>
        </div>
      </div>
    </header>
  );
}

function MembersBar({
  members,
  currentUser,
  token,
  onOpenProfile,
}: {
  members: HouseholdMember[];
  currentUser: User;
  token: string;
  onOpenProfile: () => void;
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
          onOpenProfile={onOpenProfile}
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
  onOpenProfile,
}: {
  member: HouseholdMember;
  currentUser: User;
  token: string;
  onOpenProfile: () => void;
}) {
  return (
    <div className="group relative">
      {member.isCurrentUser ? (
        <ProtectedUserAvatar
          user={currentUser}
          token={token}
          className="size-9 border border-primary/30 bg-highlight text-accent-foreground shadow-xs"
        />
      ) : (
        <AvatarCircle
          name={member.displayName}
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
            <Button variant="secondary" size="sm" className="w-full" onClick={onOpenProfile}>
              <Pencil />
              Editar perfil
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
  collapsed,
  theme,
  onChangeTheme,
  onOpenProfile,
  onLogout,
}: {
  user: User;
  token: string;
  collapsed: boolean;
  theme: AppTheme;
  onChangeTheme: (theme: AppTheme) => void;
  onOpenProfile: () => void;
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
          <ProtectedUserAvatar user={user} token={token} className="size-9 border border-border/70 bg-surface text-foreground" />
          {!collapsed ? <span className="truncate">{user.displayName}</span> : null}
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align={collapsed ? "center" : "end"}>
        <DropdownMenuLabel>{user.displayName}</DropdownMenuLabel>
        <div className="px-3 pb-2 text-xs text-muted-foreground">{user.email}</div>
        <DropdownMenuItem onClick={onOpenProfile}>
          <Pencil className="size-4" />
          Editar perfil
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

function QuickStats({
  universes,
  projects,
  activities,
  members,
}: {
  universes: Universe[];
  projects: Project[];
  activities: Activity[];
  members: HouseholdMember[];
}) {
  const openActivities = activities.filter((activity) => activity.status !== "Concluido").length;
  const urgentActivities = activities.filter((activity) => activity.priority === "Urgente").length;

  return (
    <div className="grid gap-2.5 sm:grid-cols-2 xl:grid-cols-5">
      <StatCard label="Universos" value={universes.length} icon={<Layers className="size-4" />} tone="default" />
      <StatCard label="Projetos" value={projects.length} icon={<Folder className="size-4" />} tone="default" />
      <StatCard label="Abertas" value={openActivities} icon={<ClipboardList className="size-4" />} tone="success" />
      <StatCard label="Urgentes" value={urgentActivities} icon={<Sparkles className="size-4" />} tone="warning" />
      <StatCard label="Pessoas" value={members.length} icon={<Users className="size-4" />} tone="default" />
    </div>
  );
}

function StatCard({
  label,
  value,
  icon,
  tone,
  className,
}: {
  label: string;
  value: number;
  icon: React.ReactNode;
  tone: "default" | "success" | "warning";
  className?: string;
}) {
  return (
    <Card className={className}>
      <CardContent className="flex items-center gap-3 p-4">
        <div
          className={cn(
            "grid size-9 place-items-center rounded-[14px]",
            tone === "default" && "bg-accent text-accent-foreground",
            tone === "success" && "bg-status-success-soft text-success",
            tone === "warning" && "bg-status-warning-soft text-warning",
          )}
        >
          {icon}
        </div>
        <div className="min-w-0">
          <p className="text-[28px] font-semibold leading-none text-foreground">{value}</p>
          <p className="mt-1 text-[13px] text-muted-foreground">{label}</p>
        </div>
      </CardContent>
    </Card>
  );
}

function UniverseAvatar({
  name,
  imageUrl,
  className,
}: {
  name: string;
  imageUrl?: string | null;
  className?: string;
}) {
  return <AvatarCircle name={name} imageUrl={imageUrl} className={className} />;
}

function ProjectExplorer({ dashboard }: { dashboard: ProjectDashboardController }) {
  const [collapsedUniverses, setCollapsedUniverses] = useState<Record<string, boolean>>({});

  function toggleUniverse(universeId: string) {
    setCollapsedUniverses((current) => ({ ...current, [universeId]: !current[universeId] }));
  }

  function selectUniverse(universeId: string) {
    setCollapsedUniverses((current) => ({ ...current, [universeId]: false }));
    dashboard.selectUniverseScope(universeId);
  }

  function selectProject(project: Project) {
    setCollapsedUniverses((current) => ({ ...current, [project.universeId]: false }));
    dashboard.selectProjectScope(project);
  }

  return (
    <Card>
      <CardHeader className="border-b border-border/60 pb-4">
        <div className="flex items-start justify-between gap-3">
          <h2 className="text-lg font-semibold text-foreground">Universos e projetos</h2>
          <ExplorerCreateMenu dashboard={dashboard} />
        </div>
      </CardHeader>

      <CardContent className="space-y-3 p-4">
        <button
          className={cn(
            "flex w-full items-center justify-between rounded-[18px] border px-3 py-3 text-left transition",
            !dashboard.selectedUniverseId && !dashboard.selectedProjectId
              ? "border-primary/20 bg-highlight text-accent-foreground"
              : "border-border/70 bg-surface-strong hover:bg-surface-muted",
          )}
          type="button"
          onClick={dashboard.selectAllScopes}
        >
          <div>
            <p className="text-sm font-semibold">Todos os projetos</p>
            <p className="mt-1 text-xs text-muted-foreground">Casa inteira</p>
          </div>
          <Badge variant="neutral">{dashboard.activities.length}</Badge>
        </button>

        <div className="space-y-2">
          {dashboard.universes.length === 0 ? (
            <EmptyState
              icon={<Layers className="size-5" />}
              title="Nenhum universo criado"
              description="Crie o primeiro agrupador para começar a estruturar projetos da casa."
              action={
                <Button variant="secondary" onClick={dashboard.openCreateUniverse}>
                  <Plus />
                  Criar universo
                </Button>
              }
            />
          ) : (
            dashboard.universes.map((universe) => {
              const universeProjects = dashboard.projects.filter((project) => project.universeId === universe.id);
              const universeActivityCount = dashboard.activities.filter((activity) => activity.universeId === universe.id).length;
              const activeUniverse = dashboard.selectedUniverseId === universe.id && !dashboard.selectedProjectId;
              const hasActiveProject = universeProjects.some((project) => project.id === dashboard.selectedProjectId);
              const isCollapsed = activeUniverse || hasActiveProject ? false : (collapsedUniverses[universe.id] ?? false);

              return (
                <div
                  key={universe.id}
                  className={cn(
                    "rounded-[18px] border border-border/60 bg-surface p-2.5",
                    (activeUniverse || hasActiveProject) && "border-primary/20",
                  )}
                >
                  <div className="flex items-start gap-1.5">
                    <Button
                      variant="ghost"
                      size="icon"
                      className="mt-0.5 shrink-0"
                      onClick={() => toggleUniverse(universe.id)}
                      aria-label={isCollapsed ? `Expandir ${universe.name}` : `Recolher ${universe.name}`}
                    >
                      {isCollapsed ? <ChevronRight className="size-4" /> : <ChevronDown className="size-4" />}
                    </Button>
                    <button
                      className={cn(
                        "flex min-w-0 flex-1 items-center justify-between rounded-[14px] px-2.5 py-2.5 text-left transition",
                        activeUniverse ? "bg-highlight text-accent-foreground" : "hover:bg-surface-muted",
                      )}
                      type="button"
                      onClick={() => selectUniverse(universe.id)}
                    >
                      <div className="flex min-w-0 items-center gap-2.5">
                        <UniverseAvatar
                          name={universe.name}
                          imageUrl={universe.imageUrl}
                          className="size-8"
                        />
                        <div className="min-w-0">
                          <p className="truncate text-sm font-semibold">{universe.name}</p>
                          <p className="mt-0.5 text-xs text-muted-foreground">{universeProjects.length} projetos</p>
                        </div>
                      </div>
                      <Badge variant="neutral">{universeActivityCount}</Badge>
                    </button>
                    <EntityActionMenu
                      title={universe.name}
                      onCreate={() => dashboard.openCreateProject(universe.id)}
                      onEdit={universe.canEdit ? () => dashboard.openEditUniverse(universe) : undefined}
                      onDelete={
                        universe.canDelete
                          ? () => void dashboard.deleteUniverse(universe).catch(() => undefined)
                          : undefined
                      }
                      createLabel="Novo projeto"
                      editLabel="Editar universo"
                      deleteLabel="Excluir universo"
                    />
                  </div>

                  {!isCollapsed ? (
                    <div className="mt-2 space-y-1 border-l border-border/60 pl-3">
                      {universeProjects.length === 0 ? (
                        <p className="rounded-[14px] bg-surface-muted px-3 py-2 text-sm text-muted-foreground">Sem projetos.</p>
                      ) : (
                        universeProjects.map((project) => (
                          <div key={project.id} className="flex items-start gap-1.5">
                            <button
                              className={cn(
                                "flex min-w-0 flex-1 items-center justify-between rounded-[14px] px-3 py-2.5 text-left transition",
                                dashboard.selectedProjectId === project.id
                                  ? "bg-highlight text-accent-foreground"
                                  : "hover:bg-surface-muted",
                              )}
                              type="button"
                              onClick={() => selectProject(project)}
                            >
                              <div className="flex min-w-0 items-center gap-2.5">
                                <UniverseAvatar
                                  name={project.universeName}
                                  imageUrl={project.universeImageUrl}
                                  className="size-7"
                                />
                                <div className="min-w-0">
                                  <p className="truncate text-sm font-medium">{project.name}</p>
                                  <p className="mt-0.5 text-xs text-muted-foreground">{project.activityCount} atividades</p>
                                </div>
                              </div>
                              <Badge variant="neutral">{project.activityCount}</Badge>
                            </button>
                            <EntityActionMenu
                              title={project.name}
                              onEdit={project.canEdit ? () => dashboard.openEditProject(project) : undefined}
                              onDelete={
                                project.canDelete
                                  ? () => void dashboard.deleteProject(project).catch(() => undefined)
                                  : undefined
                              }
                              editLabel="Editar projeto"
                              deleteLabel="Excluir projeto"
                            />
                          </div>
                        ))
                      )}
                    </div>
                  ) : null}
                </div>
              );
            })
          )}
        </div>
      </CardContent>
    </Card>
  );
}

function ExplorerCreateMenu({ dashboard }: { dashboard: ProjectDashboardController }) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="secondary" size="icon" aria-label="Adicionar universo ou projeto" title="Adicionar">
          <Plus />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuLabel>Criar</DropdownMenuLabel>
        <DropdownMenuItem onClick={dashboard.openCreateUniverse}>
          <FolderPlus className="size-4" />
          Novo universo
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={() => dashboard.openCreateProject(dashboard.selectedUniverseId || undefined)}
          disabled={dashboard.universes.length === 0}
        >
          <Folder className="size-4" />
          Novo projeto
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function WorkspaceBoard({ dashboard }: { dashboard: ProjectDashboardController }) {
  return (
    <Card>
      <CardHeader className="border-b border-border/60 pb-4">
        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
            <div>
              <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">Atividades</p>
              <h2 className="mt-1 text-xl font-semibold text-foreground">{dashboard.selectedScopeLabel}</h2>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <ViewModeToggle value={dashboard.viewMode} onChange={dashboard.setViewMode} />
              <Button onClick={() => dashboard.openCreateActivity()} disabled={dashboard.projects.length === 0}>
                <Plus />
                Nova atividade
              </Button>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2 xl:flex-nowrap">
            <div className="relative min-w-[18rem] flex-[1.7]">
              <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                className="pl-9"
                value={dashboard.filters.search}
                onChange={(event) => dashboard.updateFilter("search", event.target.value)}
                placeholder="Buscar por atividade, projeto ou universo"
                aria-label="Buscar atividades"
              />
            </div>

            <Select
              className="min-w-[11rem] xl:w-[11rem]"
              value={dashboard.filters.status}
              onChange={(event) => dashboard.updateFilter("status", event.target.value as ActivityStatus | "all")}
            >
              <option value="all">Todos os status</option>
              {activityColumns.map((column) => (
                <option key={column.status} value={column.status}>
                  {column.label}
                </option>
              ))}
            </Select>

            <Select
              className="min-w-[11rem] xl:w-[11rem]"
              value={dashboard.filters.priority}
              onChange={(event) => dashboard.updateFilter("priority", event.target.value as Priority | "all")}
            >
              <option value="all">Todas as prioridades</option>
              {Object.entries(priorityLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </Select>

            <Select
              className="min-w-[12rem] xl:w-[12rem]"
              value={dashboard.filters.responsibleMemberId}
              onChange={(event) => dashboard.updateFilter("responsibleMemberId", event.target.value)}
            >
              <option value="all">Todos os responsáveis</option>
              <option value="">Sem responsável</option>
              {dashboard.members.map((member) => (
                <option key={member.id} value={member.id}>
                  {member.displayName}
                </option>
              ))}
            </Select>

            <Select
              className="min-w-[11rem] xl:w-[11rem]"
              value={dashboard.filters.sort}
              onChange={(event) => dashboard.updateFilter("sort", event.target.value as typeof dashboard.filters.sort)}
            >
              {activitySortOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  Ordenar por {option.label}
                </option>
              ))}
            </Select>

            <div className="ml-auto flex items-center gap-2">
              <Button
                variant={dashboard.filters.openOnly ? "default" : "secondary"}
                onClick={() => dashboard.updateFilter("openOnly", !dashboard.filters.openOnly)}
              >
                <Filter />
                {dashboard.filters.openOnly ? "Só abertas" : "Todas"}
              </Button>
              <Button variant="ghost" onClick={dashboard.resetFilters}>
                <ListFilter />
                Limpar
              </Button>
            </div>
          </div>
        </div>
      </CardHeader>

      <CardContent className="p-4">
        {dashboard.visibleActivities.length === 0 ? (
          <EmptyState
            icon={<ClipboardList className="size-5" />}
            title="Nenhuma atividade encontrada"
            description="Ajuste os filtros atuais ou crie uma nova atividade para preencher esta visão."
            action={
              <Button onClick={dashboard.openCreateActivity} disabled={dashboard.projects.length === 0}>
                <Plus />
                Nova atividade
              </Button>
            }
          />
        ) : dashboard.viewMode === "list" ? (
          <ActivityListView dashboard={dashboard} />
        ) : (
          <ActivityKanbanView dashboard={dashboard} />
        )}
      </CardContent>
    </Card>
  );
}

function ViewModeToggle({
  value,
  onChange,
}: {
  value: ProjectViewMode;
  onChange: (mode: ProjectViewMode) => void;
}) {
  return (
    <div className="inline-flex rounded-[14px] border border-border/70 bg-surface-muted p-1">
      {viewModeOptions.map((option) => (
        <button
          key={option.value}
          className={cn(
            "inline-flex items-center gap-2 rounded-[10px] px-3 py-1.5 text-sm font-semibold transition",
            value === option.value
              ? "bg-surface-strong text-foreground shadow-xs"
              : "text-muted-foreground hover:text-foreground",
          )}
          type="button"
          onClick={() => onChange(option.value)}
        >
          {option.value === "list" ? <Table2 className="size-4" /> : <ClipboardList className="size-4" />}
          {option.label}
        </button>
      ))}
    </div>
  );
}

function ActivityListView({ dashboard }: { dashboard: ProjectDashboardController }) {
  return (
    <div className="space-y-3">
      {dashboard.groupedActivities.map((group) => (
        <Card key={group.status} className={cn("border-border/60 bg-surface-elevated", statusSectionStyles[group.status].card)}>
          <CardHeader className={cn("border-b border-border/60 pb-3", statusSectionStyles[group.status].header)}>
            <div className="flex items-center justify-between gap-3">
              <div>
                <h3 className="text-base font-semibold text-foreground">{group.label}</h3>
                <p className="mt-1 text-sm text-muted-foreground">{group.hint}</p>
              </div>
              <Badge variant="neutral">{group.items.length}</Badge>
            </div>
          </CardHeader>
          <CardContent className="p-0">
            {group.items.length === 0 ? (
              <div className="px-4 py-8 text-center text-sm text-muted-foreground">Sem atividades neste status.</div>
            ) : (
              <div className="overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow className="border-b border-border/60 bg-surface-muted hover:bg-surface-muted">
                      <TableHead>Atividade</TableHead>
                      <TableHead>Escopo</TableHead>
                      <TableHead>Responsável</TableHead>
                      <TableHead>Prioridade</TableHead>
                      <TableHead>Tamanho</TableHead>
                      <TableHead>Volume</TableHead>
                      <TableHead className="w-[60px] text-right">Ações</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {group.items.map((activity) => (
                      <TableRow key={activity.id}>
                        <TableCell className="min-w-[240px]">
                          <button
                            className="text-left"
                            type="button"
                            onClick={() => dashboard.openActivity(activity)}
                          >
                            <div className="text-sm font-semibold text-foreground hover:text-primary">{activity.title}</div>
                            {activity.description ? (
                              <div className="mt-1 max-w-xl overflow-hidden text-ellipsis whitespace-nowrap text-[13px] text-muted-foreground">
                                {activity.description}
                              </div>
                            ) : null}
                          </button>
                        </TableCell>
                        <TableCell className="min-w-[160px]">
                          <div className="flex items-center gap-2">
                            <UniverseAvatar
                              name={activity.universeName}
                              imageUrl={activity.universeImageUrl}
                              className="size-6"
                            />
                            <div className="min-w-0">
                              <div className="truncate text-sm font-medium text-foreground">{activity.projectName}</div>
                              <div className="mt-0.5 truncate text-xs text-muted-foreground">{activity.universeName}</div>
                            </div>
                          </div>
                        </TableCell>
                        <TableCell>
                          {activity.responsibleName ? (
                            <div className="inline-flex items-center gap-1.5 rounded-full border border-border/70 bg-surface-strong px-2 py-1">
                              <span className="grid size-6 place-items-center rounded-full bg-accent text-[10px] font-semibold text-accent-foreground">
                                {getInitials(activity.responsibleName)}
                              </span>
                              <span className="text-[13px]">{activity.responsibleName}</span>
                            </div>
                          ) : (
                            <span className="text-[13px] text-muted-foreground">Sem responsável</span>
                          )}
                        </TableCell>
                        <TableCell>
                          <Badge variant={getPriorityVariant(activity.priority)}>{priorityLabels[activity.priority]}</Badge>
                        </TableCell>
                        <TableCell>
                          <span className="text-[13px] text-foreground">{activity.size ? `${activity.size} pts` : "Sem tamanho"}</span>
                        </TableCell>
                        <TableCell>
                          <div className="text-[13px] text-foreground">{activity.commentCount} comentários</div>
                          <div className="mt-0.5 text-xs text-muted-foreground">{activity.pendingCount} pendências</div>
                        </TableCell>
                        <TableCell className="text-right">
                          <ActivityActionMenu
                            activity={activity}
                            onOpen={() => dashboard.openActivity(activity)}
                            onEdit={activity.canEdit ? () => dashboard.openEditActivity(activity) : undefined}
                            onDelete={
                              activity.canDelete
                                ? () => void dashboard.deleteActivity(activity).catch(() => undefined)
                                : undefined
                            }
                          />
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

function ActivityKanbanView({ dashboard }: { dashboard: ProjectDashboardController }) {
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 8 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );
  const [activeActivity, setActiveActivity] = useState<Activity | null>(null);

  async function handleDragEnd(event: DragEndEvent) {
    const dragged = event.active.data.current?.activity as Activity | undefined;
    const overData = event.over?.data.current;
    setActiveActivity(null);

    if (!dragged || !overData) {
      return;
    }

    const nextStatus =
      overData.type === "column"
        ? (overData.status as Activity["status"])
        : overData.type === "activity"
          ? (overData.status as Activity["status"])
          : null;

    if (!nextStatus || nextStatus === dragged.status) {
      return;
    }

    await dashboard.updateActivityStatus(dragged, nextStatus).catch(() => undefined);
  }

  function handleDragStart(event: DragStartEvent) {
    const dragged = event.active.data.current?.activity as Activity | undefined;
    setActiveActivity(dragged ?? null);
  }

  return (
    <DndContext sensors={sensors} collisionDetection={closestCorners} onDragStart={handleDragStart} onDragEnd={handleDragEnd}>
      <div className="grid gap-3 xl:grid-cols-3">
        {dashboard.groupedActivities.map((group) => (
          <KanbanColumn key={group.status} group={group} dashboard={dashboard} />
        ))}
      </div>
      <DragOverlay modifiers={[snapOverlayToCursor]}>
        {activeActivity ? <ActivityCard activity={activeActivity} onOpen={() => undefined} dragging /> : null}
      </DragOverlay>
    </DndContext>
  );
}

function KanbanColumn({
  group,
  dashboard,
}: {
  group: { status: Activity["status"]; label: string; hint: string; items: Activity[] };
  dashboard: ProjectDashboardController;
}) {
  const { setNodeRef, isOver } = useDroppable({
    id: `column:${group.status}`,
    data: { type: "column", status: group.status },
  });

  return (
    <div className={cn("rounded-[20px] border border-border/60 bg-surface-elevated p-2.5 shadow-xs", statusSectionStyles[group.status].card)}>
      <div className={cn("flex items-start justify-between gap-3 rounded-[16px] p-3", statusSectionStyles[group.status].header)}>
        <div>
          <h3 className="text-sm font-semibold text-foreground">{group.label}</h3>
          <p className="mt-0.5 text-xs text-muted-foreground">{group.hint}</p>
        </div>
        <Badge variant="neutral">{group.items.length}</Badge>
      </div>

      <div
        ref={setNodeRef}
        className={cn(
          "mt-2.5 min-h-[240px] rounded-[16px] border border-dashed p-2 transition",
          statusSectionStyles[group.status].dropzone,
          isOver && "border-primary/40 bg-highlight",
        )}
      >
        <SortableContext items={group.items.map((item) => item.id)} strategy={verticalListSortingStrategy}>
          <div className="space-y-2">
            {group.items.length === 0 ? (
              <div className="grid min-h-[180px] place-items-center px-4 text-center text-sm text-muted-foreground">
                Arraste uma atividade para este estágio.
              </div>
            ) : (
              group.items.map((activity) => (
                <SortableActivityCard
                  key={activity.id}
                  activity={activity}
                  onOpen={() => dashboard.openActivity(activity)}
                  onEdit={activity.canEdit ? () => dashboard.openEditActivity(activity) : undefined}
                  onDelete={
                    activity.canDelete
                      ? () => void dashboard.deleteActivity(activity).catch(() => undefined)
                      : undefined
                  }
                />
              ))
            )}
          </div>
        </SortableContext>
      </div>
    </div>
  );
}

function SortableActivityCard({
  activity,
  onOpen,
  onEdit,
  onDelete,
}: {
  activity: Activity;
  onOpen: () => void;
  onEdit?: () => void;
  onDelete?: () => void;
}) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: activity.id,
    data: { type: "activity", activity, status: activity.status },
    disabled: !activity.canEdit,
  });

  return (
    <div
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      className={cn(isDragging && "opacity-40")}
    >
      <ActivityCard
        activity={activity}
        onOpen={onOpen}
        onEdit={onEdit}
        onDelete={onDelete}
        dragHandleProps={activity.canEdit ? { ...attributes, ...listeners } : undefined}
      />
    </div>
  );
}

function ActivityCard({
  activity,
  onOpen,
  onEdit,
  onDelete,
  dragging = false,
  dragHandleProps,
}: {
  activity: Activity;
  onOpen: () => void;
  onEdit?: () => void;
  onDelete?: () => void;
  dragging?: boolean;
  dragHandleProps?: Record<string, unknown>;
}) {
  return (
    <Card className={cn("border-border/70 bg-surface-strong", dragging && "rotate-1 shadow-md")}>
      <CardContent className="space-y-3 p-3">
        <div className="flex items-start gap-2">
          <button className="min-w-0 flex-1 text-left" type="button" onClick={onOpen}>
            <h4 className="truncate text-sm font-semibold text-foreground">{activity.title}</h4>
            <div className="mt-1 flex min-w-0 items-center gap-2">
              <UniverseAvatar
                name={activity.universeName}
                imageUrl={activity.universeImageUrl}
                className="size-6"
              />
              <p className="truncate text-xs text-muted-foreground">
                {activity.universeName} / {activity.projectName}
              </p>
            </div>
            {activity.description ? (
              <p className="mt-2 overflow-hidden text-ellipsis whitespace-nowrap text-[13px] text-muted-foreground">
                {activity.description}
              </p>
            ) : null}
          </button>

          <div className="flex items-center gap-1">
            {dragHandleProps ? (
              <button
                className="inline-flex size-8 items-center justify-center rounded-xl text-muted-foreground transition hover:bg-surface-muted hover:text-foreground"
                type="button"
                aria-label="Arrastar atividade"
                {...dragHandleProps}
              >
                <GripVertical className="size-4" />
              </button>
            ) : null}

            <ActivityActionMenu activity={activity} onOpen={onOpen} onEdit={onEdit} onDelete={onDelete} />
          </div>
        </div>

        <div className="flex flex-wrap gap-2">
          <Badge variant={getPriorityVariant(activity.priority)}>{priorityLabels[activity.priority]}</Badge>
          {activity.size ? <Badge variant="neutral">{activity.size} pts</Badge> : null}
          {activity.responsibleName ? <Badge variant="neutral">{activity.responsibleName}</Badge> : null}
        </div>

        <div className="flex items-center justify-between gap-3 border-t border-border/60 pt-2 text-[12px] text-muted-foreground">
          <span>{activity.commentCount} comentários</span>
          <span>{activity.pendingCount} pendências</span>
        </div>
      </CardContent>
    </Card>
  );
}

function ActivityActionMenu({
  activity,
  onOpen,
  onEdit,
  onDelete,
}: {
  activity: Activity;
  onOpen: () => void;
  onEdit?: () => void;
  onDelete?: () => void;
}) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" aria-label={`Ações da atividade ${activity.title}`}>
          <MoreHorizontal />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuLabel>Atividade</DropdownMenuLabel>
        <DropdownMenuItem onClick={onOpen}>Abrir detalhes</DropdownMenuItem>
        {onEdit ? <DropdownMenuItem onClick={onEdit}>Editar</DropdownMenuItem> : null}
        {onDelete ? (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuItem className="text-danger focus:text-danger" onClick={onDelete}>
              Excluir
            </DropdownMenuItem>
          </>
        ) : null}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function EntityActionMenu({
  title,
  onCreate,
  onEdit,
  onDelete,
  createLabel,
  editLabel = "Editar",
  deleteLabel = "Excluir",
}: {
  title: string;
  onCreate?: () => void;
  onEdit?: () => void;
  onDelete?: () => void;
  createLabel?: string;
  editLabel?: string;
  deleteLabel?: string;
}) {
  if (!onCreate && !onEdit && !onDelete) {
    return null;
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" aria-label={`Ações de ${title}`}>
          <MoreHorizontal />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuLabel>{title}</DropdownMenuLabel>
        {onCreate ? <DropdownMenuItem onClick={onCreate}>{createLabel}</DropdownMenuItem> : null}
        {onEdit ? <DropdownMenuItem onClick={onEdit}>{editLabel}</DropdownMenuItem> : null}
        {onDelete ? (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuItem className="text-danger focus:text-danger" onClick={onDelete}>
              {deleteLabel}
            </DropdownMenuItem>
          </>
        ) : null}
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
          <DialogDescription>Defina o nome da casa que agrupa universos, projetos e membros.</DialogDescription>
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

function UniverseDialog({
  universe,
  open,
  onOpenChange,
  onSave,
}: {
  universe: Universe | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSave: (input: { name: string; imageUrl?: string }) => Promise<void>;
}) {
  const [name, setName] = useState(universe?.name ?? "");
  const [imageUrl, setImageUrl] = useState(universe?.imageUrl ?? "");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const isEditing = Boolean(universe);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSaving(true);

    try {
      await onSave({ name, imageUrl });
      onOpenChange(false);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível salvar o universo.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{isEditing ? "Editar universo" : "Novo universo"}</DialogTitle>
          <DialogDescription>Universos ajudam a separar grandes frentes, como reforma, jardim ou digital.</DialogDescription>
        </DialogHeader>
        <form className="space-y-4" onSubmit={submit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}
          <Field label="Nome do universo">
            <Input value={name} onChange={(event) => setName(event.target.value)} autoFocus required />
          </Field>
          <Field label="Imagem do universo (URL)">
            <Input
              value={imageUrl}
              onChange={(event) => setImageUrl(event.target.value)}
              placeholder="https://... ou data:image/..."
            />
          </Field>
          {imageUrl ? (
            <div className="flex items-center gap-3 rounded-[16px] border border-border/60 bg-surface-muted p-3">
              <UniverseAvatar name={name || "Universo"} imageUrl={imageUrl} className="size-12" />
              <div className="min-w-0">
                <p className="text-sm font-semibold text-foreground">{name || "Prévia do universo"}</p>
                <p className="truncate text-xs text-muted-foreground">{imageUrl}</p>
              </div>
            </div>
          ) : null}
          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saving}>
              {isEditing ? "Salvar universo" : "Criar universo"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

export function ProfileDialog({
  open,
  user,
  token,
  onOpenChange,
  onSave,
}: {
  open: boolean;
  user: User;
  token: string;
  onOpenChange: (open: boolean) => void;
  onSave: (input: { displayName: string; phoneNumber?: string; profilePhoto?: File | null }) => Promise<void>;
}) {
  const [displayName, setDisplayName] = useState(user.displayName);
  const [phoneNumber, setPhoneNumber] = useState(user.phoneNumber ?? "");
  const [profilePhoto, setProfilePhoto] = useState<File | null>(null);
  const previewUrl = useObjectUrl(profilePhoto);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSaving(true);

    try {
      await onSave({ displayName, phoneNumber, profilePhoto });
      onOpenChange(false);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível salvar o perfil.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Editar perfil</DialogTitle>
          <DialogDescription>Atualize o nome visível e o telefone usado no seu contexto da casa.</DialogDescription>
        </DialogHeader>
        <form className="space-y-4" onSubmit={submit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}
          <Field label="Foto de perfil">
            <div className="flex items-center gap-3 rounded-[16px] border border-border/60 bg-surface-muted p-3">
              {previewUrl ? (
                <AvatarCircle name={displayName || user.displayName} imageUrl={previewUrl} className="size-14" />
              ) : (
                <ProtectedUserAvatar user={user} token={token} className="size-14" />
              )}
              <div className="min-w-0 flex-1">
                <p className="text-sm font-semibold text-foreground">
                  {profilePhoto ? "Nova foto selecionada" : "Sua foto atual"}
                </p>
                <p className="truncate text-xs text-muted-foreground">
                  {profilePhoto ? profilePhoto.name : "Use JPG, PNG ou WEBP com até 5 MB."}
                </p>
              </div>
            </div>
            <Input
              type="file"
              accept="image/jpeg,image/png,image/webp"
              onChange={(event) => setProfilePhoto(event.target.files?.[0] ?? null)}
            />
          </Field>
          <Field label="Nome">
            <Input value={displayName} onChange={(event) => setDisplayName(event.target.value)} autoFocus required />
          </Field>
          <Field label="WhatsApp">
            <Input value={phoneNumber} onChange={(event) => setPhoneNumber(event.target.value)} />
          </Field>
          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saving}>
              Salvar perfil
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function useObjectUrl(file: File | null) {
  const objectUrl = useMemo(() => (file ? URL.createObjectURL(file) : null), [file]);

  useEffect(() => {
    return () => {
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [objectUrl]);

  return objectUrl;
}

function ProjectDialog({
  project,
  universes,
  defaultUniverseId,
  open,
  onOpenChange,
  onSave,
}: {
  project: Project | null;
  universes: Universe[];
  defaultUniverseId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSave: (universeId: string, name: string) => Promise<void>;
}) {
  const [universeId, setUniverseId] = useState(project?.universeId || defaultUniverseId || universes[0]?.id || "");
  const [name, setName] = useState(project?.name ?? "");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const isEditing = Boolean(project);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSaving(true);

    try {
      await onSave(universeId, name);
      onOpenChange(false);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível salvar o projeto.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{isEditing ? "Editar projeto" : "Novo projeto"}</DialogTitle>
          <DialogDescription>Cada projeto concentra um fluxo de atividades dentro de um universo.</DialogDescription>
        </DialogHeader>
        <form className="space-y-4" onSubmit={submit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}
          <Field label="Universo">
            <Select value={universeId} onChange={(event) => setUniverseId(event.target.value)} required>
              <option value="">Selecione</option>
              {universes.map((universe) => (
                <option key={universe.id} value={universe.id}>
                  {universe.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Nome do projeto">
            <Input value={name} onChange={(event) => setName(event.target.value)} autoFocus required />
          </Field>
          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saving || universes.length === 0}>
              {isEditing ? "Salvar projeto" : "Criar projeto"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function ActivityDialog({
  activity,
  projects,
  members,
  defaultProjectId,
  open,
  onOpenChange,
  onSave,
}: {
  activity: Activity | null;
  projects: Project[];
  members: HouseholdMember[];
  defaultProjectId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSave: (input: ActivityFormInput) => Promise<void>;
}) {
  const [projectId, setProjectId] = useState(activity?.projectId || defaultProjectId || projects[0]?.id || "");
  const [title, setTitle] = useState(activity?.title ?? "");
  const [description, setDescription] = useState(activity?.description ?? "");
  const [status, setStatus] = useState<ActivityStatus>(activity?.status ?? "NaoIniciada");
  const [priority, setPriority] = useState<Priority>(activity?.priority ?? "Media");
  const [size, setSize] = useState(activity?.size ? String(activity.size) : "");
  const [responsibleMemberId, setResponsibleMemberId] = useState(activity?.responsibleMemberId ?? "");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const isEditing = Boolean(activity);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSaving(true);

    try {
      await onSave({
        projectId,
        title,
        description,
        status,
        priority,
        size: size ? Number(size) : undefined,
        responsibleMemberId,
      });
      onOpenChange(false);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível salvar a atividade.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{isEditing ? "Editar atividade" : "Nova atividade"}</DialogTitle>
          <DialogDescription>Use um título direto e deixe descrição e tamanho para dar contexto operacional.</DialogDescription>
        </DialogHeader>
        <form className="space-y-4" onSubmit={submit}>
          {error ? <Notice tone="danger">{error}</Notice> : null}
          <Field label="Projeto">
            <Select value={projectId} onChange={(event) => setProjectId(event.target.value)} required>
              <option value="">Selecione</option>
              {projects.map((project) => (
                <option key={project.id} value={project.id}>
                  {project.universeName} / {project.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Título">
            <Input value={title} onChange={(event) => setTitle(event.target.value)} autoFocus required />
          </Field>
          <Field label="Descrição">
            <Textarea value={description} onChange={(event) => setDescription(event.target.value)} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Status">
              <Select value={status} onChange={(event) => setStatus(event.target.value as ActivityStatus)}>
                {activityColumns.map((column) => (
                  <option key={column.status} value={column.status}>
                    {column.label}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Prioridade">
              <Select value={priority} onChange={(event) => setPriority(event.target.value as Priority)}>
                {Object.entries(priorityLabels).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Tamanho">
              <Input type="number" step="0.5" min="0" value={size} onChange={(event) => setSize(event.target.value)} />
            </Field>
            <Field label="Responsável">
              <Select value={responsibleMemberId} onChange={(event) => setResponsibleMemberId(event.target.value)}>
                <option value="">Sem responsável</option>
                {members.map((member) => (
                  <option key={member.id} value={member.id}>
                    {member.displayName}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <DialogFooter>
            <Button variant="secondary" type="button" onClick={() => onOpenChange(false)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={saving || projects.length === 0}>
              {isEditing ? "Salvar atividade" : "Criar atividade"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function ShareDialog({
  members,
  canShare,
  open,
  onOpenChange,
  onShare,
}: {
  members: HouseholdMember[];
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
                    <span className="grid size-10 place-items-center rounded-full bg-accent text-xs font-semibold text-accent-foreground">
                      {getInitials(member.displayName)}
                    </span>
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

function ActivityDetailsSheet({
  activity,
  comments,
  commentsLoading,
  onClose,
  onCreateComment,
  onUpdateComment,
  onDeleteComment,
  onMove,
  onEditActivity,
  onDeleteActivity,
}: {
  activity: Activity;
  comments: ActivityComment[];
  commentsLoading: boolean;
  onClose: () => void;
  onCreateComment: (activityId: string, body: string) => Promise<void>;
  onUpdateComment: (activityId: string, commentId: string, body: string) => Promise<void>;
  onDeleteComment: (activityId: string, comment: ActivityComment) => Promise<void>;
  onMove: (activity: Activity, direction: -1 | 1) => Promise<void>;
  onEditActivity: (activity: Activity) => void;
  onDeleteActivity: (activity: Activity) => Promise<void>;
}) {
  const columnIndex = activityColumns.findIndex((column) => column.status === activity.status);

  return (
    <Sheet open onOpenChange={(open) => !open && onClose()}>
      <SheetContent side="right" className="overflow-y-auto">
        <SheetHeader>
          <div className="space-y-4 pr-10">
            <div className="flex flex-wrap items-center gap-2">
              <div className="inline-flex items-center gap-2 rounded-full border border-border/70 bg-surface-strong px-2.5 py-1">
                <UniverseAvatar
                  name={activity.universeName}
                  imageUrl={activity.universeImageUrl}
                  className="size-6"
                />
                <span className="text-xs font-semibold text-foreground">{activity.universeName}</span>
              </div>
              <Badge variant="neutral">{activity.projectName}</Badge>
            </div>
            <div>
              <SheetTitle>{activity.title}</SheetTitle>
              <SheetDescription className="mt-2">
                {activity.description || "Sem descrição detalhada para esta atividade."}
              </SheetDescription>
            </div>
            <div className="flex flex-wrap gap-2">
              <Button variant="secondary" onClick={() => onEditActivity(activity)} disabled={!activity.canEdit}>
                <Pencil />
                Editar
              </Button>
              <Button variant="secondary" onClick={() => void onMove(activity, -1).catch(() => undefined)} disabled={columnIndex === 0 || !activity.canEdit}>
                <ArrowLeft />
                Voltar
              </Button>
              <Button variant="secondary" onClick={() => void onMove(activity, 1).catch(() => undefined)} disabled={columnIndex === activityColumns.length - 1 || !activity.canEdit}>
                <ArrowRight />
                Avançar
              </Button>
              <Button variant="danger" onClick={() => void onDeleteActivity(activity).catch(() => undefined)} disabled={!activity.canDelete}>
                <Trash2 />
                Excluir
              </Button>
            </div>
          </div>
        </SheetHeader>

        <div className="space-y-4 p-5 pt-4">
          <div className="grid gap-3 sm:grid-cols-2">
            <DetailCard label="Status" value={activityColumns.find((column) => column.status === activity.status)?.label ?? activity.status} />
            <DetailCard label="Prioridade" value={priorityLabels[activity.priority]} />
            <DetailCard label="Responsável" value={activity.responsibleName ?? "Sem responsável"} />
            <DetailCard label="Tamanho" value={activity.size ? `${activity.size} pts` : "Sem tamanho"} />
          </div>

          <Card className="border-border/60 bg-surface-elevated">
            <CardHeader className="pb-3">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <h3 className="text-base font-semibold text-foreground">Comentários</h3>
                  <p className="mt-1 text-sm text-muted-foreground">{comments.length} entradas na conversa</p>
                </div>
                <Badge variant="neutral">{comments.length}</Badge>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              <CommentComposer activityId={activity.id} onCreateComment={onCreateComment} />
              <div className="space-y-3">
                {commentsLoading ? (
                  <div className="rounded-[18px] border border-border/60 bg-surface-muted px-4 py-8 text-center text-sm text-muted-foreground">
                    Carregando comentários...
                  </div>
                ) : comments.length === 0 ? (
                  <div className="rounded-[18px] border border-border/60 bg-surface-muted px-4 py-8 text-center text-sm text-muted-foreground">
                    Nenhum comentário ainda.
                  </div>
                ) : (
                  comments.map((comment) => (
                    <EditableComment
                      key={comment.id}
                      activityId={activity.id}
                      comment={comment}
                      onUpdateComment={onUpdateComment}
                      onDeleteComment={onDeleteComment}
                    />
                  ))
                )}
              </div>
            </CardContent>
          </Card>
        </div>
      </SheetContent>
    </Sheet>
  );
}

function DetailCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[18px] border border-border/60 bg-surface-elevated px-4 py-3.5">
      <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">{label}</p>
      <p className="mt-2 text-sm font-semibold text-foreground">{value}</p>
    </div>
  );
}

function CommentComposer({
  activityId,
  onCreateComment,
}: {
  activityId: string;
  onCreateComment: (activityId: string, body: string) => Promise<void>;
}) {
  const [body, setBody] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSaving(true);

    try {
      await onCreateComment(activityId, body);
      setBody("");
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível comentar.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <form className="space-y-3" onSubmit={submit}>
      {error ? <Notice tone="danger">{error}</Notice> : null}
      <Textarea value={body} onChange={(event) => setBody(event.target.value)} placeholder="Escreva um comentário" required />
      <div className="flex justify-end">
        <Button type="submit" disabled={saving}>
          <CheckCircle2 />
          Comentar
        </Button>
      </div>
    </form>
  );
}

function EditableComment({
  activityId,
  comment,
  onUpdateComment,
  onDeleteComment,
}: {
  activityId: string;
  comment: ActivityComment;
  onUpdateComment: (activityId: string, commentId: string, body: string) => Promise<void>;
  onDeleteComment: (activityId: string, comment: ActivityComment) => Promise<void>;
}) {
  const [isEditing, setIsEditing] = useState(false);
  const [body, setBody] = useState(comment.body);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSaving(true);

    try {
      await onUpdateComment(activityId, comment.id, body);
      setIsEditing(false);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Não foi possível salvar o comentário.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="rounded-[18px] border border-border/60 bg-surface-elevated px-4 py-4">
      <div className="flex items-start justify-between gap-3">
        <div className="flex min-w-0 items-start gap-3">
          <span className="grid size-10 place-items-center rounded-full bg-accent text-xs font-semibold text-accent-foreground">
            {getInitials(comment.authorName)}
          </span>
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <p className="text-sm font-semibold text-foreground">{comment.authorName}</p>
              <span className="text-xs text-muted-foreground">{formatDateTime(comment.createdAt)}</span>
              {comment.isEdited ? <Badge variant="neutral">Editado</Badge> : null}
            </div>
            {isEditing ? (
              <form className="mt-3 space-y-3" onSubmit={submit}>
                {error ? <Notice tone="danger">{error}</Notice> : null}
                <Textarea value={body} onChange={(event) => setBody(event.target.value)} required />
                <div className="flex flex-wrap gap-2">
                  <Button variant="secondary" type="button" onClick={() => { setBody(comment.body); setIsEditing(false); }}>
                    Cancelar
                  </Button>
                  <Button type="submit" disabled={saving}>
                    Salvar comentário
                  </Button>
                </div>
              </form>
            ) : (
              <p className="mt-3 text-sm leading-7 text-muted-foreground">{comment.body}</p>
            )}
          </div>
        </div>

        {comment.canEdit || comment.canDelete ? (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon" aria-label={`Ações do comentário de ${comment.authorName}`}>
                <MoreHorizontal />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              {comment.canEdit ? (
                <DropdownMenuItem
                  onClick={() => {
                    setBody(comment.body);
                    setIsEditing(true);
                  }}
                >
                  Editar
                </DropdownMenuItem>
              ) : null}
              {comment.canDelete ? (
                <>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem className="text-danger focus:text-danger" onClick={() => void onDeleteComment(activityId, comment).catch(() => undefined)}>
                    Excluir
                  </DropdownMenuItem>
                </>
              ) : null}
            </DropdownMenuContent>
          </DropdownMenu>
        ) : null}
      </div>
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="grid gap-2">
      <span className="text-sm font-semibold text-foreground/85">{label}</span>
      {children}
    </label>
  );
}

function Notice({
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

function EmptyState({
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

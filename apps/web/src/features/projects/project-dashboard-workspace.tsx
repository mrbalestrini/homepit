"use client";

import {
  closestCorners,
  DndContext,
  type DragCancelEvent,
  DragEndEvent,
  DragOverlay,
  DragStartEvent,
  type DragOverEvent,
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
  Folder,
  FolderPlus,
  GripVertical,
  Layers,
  ListFilter,
  MoreHorizontal,
  Pencil,
  Plus,
  Search,
  Table2,
  Trash2,
} from "lucide-react";
import { ChangeEvent, FormEvent, type ReactNode, useEffect, useMemo, useState } from "react";
import type { Activity, ActivityComment, ActivityStatus, HouseholdMember, Priority, Project, Universe } from "@/lib/api";
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
import { DeleteConfirmationDialog } from "@/features/workspace/delete-confirmation-dialog";
import {
  EmptyState,
  Field,
  HomePitWorkspaceShell,
  LoadingState,
  Notice,
} from "@/features/workspace/homepit-workspace-shell";
import { ProtectedUniverseAvatar, useProtectedUniverseImage } from "@/features/workspace/protected-universe-avatar";
import { ProtectedActivityImageFrame } from "./protected-activity-image";
import {
  activityColumns,
  activitySortOptions,
  defaultActivityFilters,
  priorityLabels,
  viewModeOptions,
} from "./project-dashboard.constants";
import type { ActivityFormInput, ProjectViewMode } from "./project-dashboard.types";
import type { ProjectDashboardController } from "./use-project-dashboard";
import { formatDateOnly, formatDateTime, getInitials, getPriorityVariant } from "./project-dashboard.utils";

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

type ActivityDragTarget =
  | {
      type: "activity";
      status: Activity["status"];
      activityId: string;
    }
  | {
      type: "column";
      status: Activity["status"];
      activityId: null;
    }
  | null;

function getDragTarget(over: DragOverEvent["over"] | DragEndEvent["over"]): ActivityDragTarget {
  const overData = over?.data.current as
    | { type?: string; status?: Activity["status"]; activity?: Activity }
    | undefined;

  if (!overData?.type || !overData.status) {
    return null;
  }

  if (overData.type === "activity" && overData.activity) {
    return {
      type: "activity",
      status: overData.status,
      activityId: overData.activity.id,
    };
  }

  if (overData.type === "column") {
    return {
      type: "column",
      status: overData.status,
      activityId: null,
    };
  }

  return null;
}

export function ProjectDashboardWorkspace({ dashboard }: { dashboard: ProjectDashboardController }) {
  const openActivities = dashboard.activities.filter((activity) => activity.status !== "Concluido").length;
  const urgentActivities = dashboard.activities.filter((activity) => activity.priority === "Urgente").length;
  const headerStats = [
    { label: "Universos", value: dashboard.universes.length },
    { label: "Projetos", value: dashboard.projects.length },
    { label: "Abertas", value: openActivities },
    { label: "Urgentes", value: urgentActivities },
    { label: "Pessoas", value: dashboard.members.length },
  ];

  return (
    <>
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
        activeModule="projects"
        subtitle={dashboard.selectedScopeLabel}
        visibleCount={dashboard.visibleActivities.length}
        headerStats={headerStats}
      >
        <div className="grid gap-3 xl:grid-cols-[316px_minmax(0,1fr)]">
          <ProjectExplorer dashboard={dashboard} />
          <WorkspaceBoard dashboard={dashboard} />
        </div>
      </HomePitWorkspaceShell>

      <UniverseDialog
        key={`universe-${dashboard.editingUniverse?.id ?? "new"}-${dashboard.activeModal === "universe" ? "open" : "closed"}`}
        open={dashboard.activeModal === "universe"}
        universe={dashboard.editingUniverse}
        onOpenChange={(open) => !open && dashboard.closeModal()}
        token={dashboard.session?.accessToken}
        householdId={dashboard.activeHouseholdId}
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
        projects={dashboard.activityDialogProjects}
        members={dashboard.members}
        defaultProjectId={dashboard.activityDraftProjectId || dashboard.selectedProjectId}
        token={dashboard.session?.accessToken}
        householdId={dashboard.activeHouseholdId}
        onOpenChange={(open) => !open && dashboard.closeModal()}
        onSave={(input) =>
          dashboard.editingActivity
            ? dashboard.updateActivity(dashboard.editingActivity.id, input)
            : dashboard.createActivity(input)
        }
      />

      {dashboard.selectedActivity ? (
        <ActivityDetailsSheet
          activity={dashboard.selectedActivity}
          token={dashboard.session?.accessToken}
          householdId={dashboard.activeHouseholdId}
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
    </>
  );
}

function UniverseAvatar({
  universeId,
  name,
  imageUrl,
  hasImage,
  imageUpdatedAt,
  token,
  householdId,
  className,
}: {
  universeId?: string | null;
  name: string;
  imageUrl?: string | null;
  hasImage?: boolean;
  imageUpdatedAt?: string | null;
  token?: string;
  householdId?: string;
  className?: string;
}) {
  return (
    <ProtectedUniverseAvatar
      universeId={universeId}
      name={name}
      imageUrl={imageUrl}
      hasImage={hasImage}
      imageUpdatedAt={imageUpdatedAt}
      token={token}
      householdId={householdId}
      className={className}
    />
  );
}

function ProjectExplorer({ dashboard }: { dashboard: ProjectDashboardController }) {
  const [collapsedUniverses, setCollapsedUniverses] = useState<Record<string, boolean>>({});
  const [deletingUniverse, setDeletingUniverse] = useState<Universe | null>(null);
  const [deletingProject, setDeletingProject] = useState<Project | null>(null);
  const openActivityCount = dashboard.projects.reduce((total, project) => total + project.activityCount, 0);

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
          <Badge variant="neutral">{openActivityCount}</Badge>
        </button>

        <div className="space-y-2">
          {dashboard.loading && dashboard.universes.length === 0 && dashboard.projects.length === 0 ? (
            <LoadingState
              title="Buscando universos e projetos"
              description="Carregando a estrutura da casa antes de exibir a navegação lateral."
            />
          ) : dashboard.universes.length === 0 ? (
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
              const universeActivityCount = universeProjects.reduce((total, project) => total + project.activityCount, 0);
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
                          universeId={universe.id}
                          name={universe.name}
                          imageUrl={universe.imageUrl}
                          hasImage={universe.hasImage}
                          imageUpdatedAt={universe.imageUpdatedAt}
                          token={dashboard.session?.accessToken}
                          householdId={dashboard.activeHouseholdId}
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
                      onDelete={universe.canDelete ? () => setDeletingUniverse(universe) : undefined}
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
                                  universeId={project.universeId}
                                  name={project.universeName}
                                  imageUrl={project.universeImageUrl}
                                  hasImage={project.universeHasImage}
                                  imageUpdatedAt={project.universeImageUpdatedAt}
                                  token={dashboard.session?.accessToken}
                                  householdId={dashboard.activeHouseholdId}
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
                              onCreate={() => dashboard.openCreateActivity(project.id)}
                              onEdit={project.canEdit ? () => dashboard.openEditProject(project) : undefined}
                              onDelete={project.canDelete ? () => setDeletingProject(project) : undefined}
                              createLabel="Nova atividade"
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

      {deletingUniverse ? (
        <DeleteConfirmationDialog
          key={`universe-delete-${deletingUniverse.id}`}
          open={Boolean(deletingUniverse)}
          title="Excluir universo"
          description="Essa ação é permanente e remove este universo junto com a estrutura que depende dele."
          confirmationTarget={deletingUniverse.name}
          confirmationLabel={`Digite o nome do universo, ${deletingUniverse.name}, para confirmar`}
          confirmLabel="Excluir universo"
          impactItems={[
            "Todos os projetos deste universo, junto com suas atividades, comentários e pendências.",
            "Os prompts que usam este universo continuarão existindo, mas ficarão sem universo.",
            "A imagem vinculada ao universo será removida.",
          ]}
          onOpenChange={(open) => !open && setDeletingUniverse(null)}
          onConfirm={async () => {
            await dashboard.deleteUniverse(deletingUniverse);
          }}
        />
      ) : null}

      {deletingProject ? (
        <DeleteConfirmationDialog
          key={`project-delete-${deletingProject.id}`}
          open={Boolean(deletingProject)}
          title="Excluir projeto"
          description="Essa ação é permanente e remove o projeto e tudo o que foi criado dentro dele."
          confirmLabel="Excluir projeto"
          impactItems={[
            "Todas as atividades deste projeto.",
            "Comentários e pendências vinculados a essas atividades.",
            "O universo ao redor do projeto permanecerá intacto.",
          ]}
          onOpenChange={(open) => !open && setDeletingProject(null)}
          onConfirm={async () => {
            await dashboard.deleteProject(deletingProject);
          }}
        />
      ) : null}
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
  const hasActiveFilters =
    dashboard.filters.search.trim() !== defaultActivityFilters.search ||
    dashboard.filters.status !== defaultActivityFilters.status ||
    dashboard.filters.priority !== defaultActivityFilters.priority ||
    dashboard.filters.responsibleMemberId !== defaultActivityFilters.responsibleMemberId ||
    dashboard.filters.sort !== defaultActivityFilters.sort;

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

          <div className="flex flex-wrap items-center gap-2">
            <div className="relative w-full md:w-1/2 md:max-w-[28rem] md:flex-none">
              <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                className="pl-9"
                value={dashboard.filters.search}
                onChange={(event) => dashboard.updateFilter("search", event.target.value)}
                placeholder="Buscar por atividade, projeto ou universo"
                aria-label="Buscar atividades"
              />
            </div>

            <div className="ml-auto flex min-w-0 flex-wrap items-center gap-2 md:min-w-[46rem] md:flex-1">
              <Select
                className="min-w-[10rem] flex-1 text-[12px] leading-none xl:text-[13px]"
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
                className="min-w-[10rem] flex-1 text-[12px] leading-none xl:text-[13px]"
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
                className="min-w-[10.5rem] flex-1 text-[12px] leading-none xl:text-[13px]"
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
                className="min-w-[10rem] flex-1 text-[12px] leading-none xl:text-[13px]"
                value={dashboard.filters.sort}
                onChange={(event) => dashboard.updateFilter("sort", event.target.value as typeof dashboard.filters.sort)}
              >
                {activitySortOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    Ordenar por {option.label}
                  </option>
                ))}
              </Select>

              <Button
                variant={hasActiveFilters ? "outline" : "ghost"}
                className={cn(
                  "shrink-0",
                  hasActiveFilters &&
                    "border-primary/25 bg-primary/10 text-primary shadow-sm hover:border-primary/35 hover:bg-primary/15 hover:text-primary",
                )}
                onClick={dashboard.resetFilters}
              >
                <ListFilter />
                Limpar
              </Button>
            </div>
          </div>
        </div>
      </CardHeader>

      <CardContent className="p-4">
        {dashboard.loading && dashboard.activities.length === 0 ? (
          <LoadingState
            title="Buscando atividades"
            description="Carregando as atividades da casa para montar o quadro e aplicar os filtros."
          />
        ) : dashboard.visibleActivities.length === 0 ? (
          <EmptyState
            icon={<ClipboardList className="size-5" />}
            title="Nenhuma atividade encontrada"
            description="Ajuste os filtros atuais ou crie uma nova atividade para preencher esta visão."
            action={
              <Button onClick={() => dashboard.openCreateActivity()} disabled={dashboard.projects.length === 0}>
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

export function ActivityListView({ dashboard }: { dashboard: ProjectDashboardController }) {
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
                      <TableHead>Prazo</TableHead>
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
                            {activity.hasImage ? (
                              <Badge variant="neutral" className="mt-2">
                                Imagem
                              </Badge>
                            ) : null}
                          </button>
                        </TableCell>
                        <TableCell className="min-w-[160px]">
                          <div className="flex items-center gap-2">
                            <UniverseAvatar
                              universeId={activity.universeId}
                              name={activity.universeName}
                              imageUrl={activity.universeImageUrl}
                              hasImage={activity.universeHasImage}
                              imageUpdatedAt={activity.universeImageUpdatedAt}
                              token={dashboard.session?.accessToken}
                              householdId={dashboard.activeHouseholdId}
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
                          <span className="text-[13px] text-foreground">
                            {activity.size != null ? `${activity.size} pts` : "Sem tamanho"}
                          </span>
                        </TableCell>
                        <TableCell>
                          <span className="text-[13px] text-foreground">{formatDateOnly(activity.dueDate)}</span>
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
  const [dragTarget, setDragTarget] = useState<ActivityDragTarget>(null);

  function handleDragEnd(event: DragEndEvent) {
    const dragged = event.active.data.current?.activity as Activity | undefined;
    const nextTarget = getDragTarget(event.over) ?? dragTarget;
    setActiveActivity(null);
    setDragTarget(null);

    if (!dragged || !nextTarget) {
      return;
    }

    const nextStatus = nextTarget.status;

    if (!nextStatus || nextStatus === dragged.status) {
      return;
    }

    void dashboard.updateActivityStatusOptimistic(dragged, nextStatus);
  }

  function handleDragStart(event: DragStartEvent) {
    const dragged = event.active.data.current?.activity as Activity | undefined;
    setActiveActivity(dragged ?? null);
    setDragTarget(null);
  }

  function handleDragOver(event: DragOverEvent) {
    const nextTarget = getDragTarget(event.over);
    setDragTarget((current) => {
      if (
        current?.type === nextTarget?.type &&
        current?.status === nextTarget?.status &&
        current?.activityId === nextTarget?.activityId
      ) {
        return current;
      }

      return nextTarget;
    });
  }

  function handleDragCancel(_event: DragCancelEvent) {
    setActiveActivity(null);
    setDragTarget(null);
  }

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCorners}
      onDragStart={handleDragStart}
      onDragOver={handleDragOver}
      onDragCancel={handleDragCancel}
      onDragEnd={handleDragEnd}
    >
      <div className="grid gap-3 xl:grid-cols-3">
        {dashboard.groupedActivities.map((group) => (
          <KanbanColumn
            key={group.status}
            group={group}
            dashboard={dashboard}
            activeActivityId={activeActivity?.id ?? null}
            dragTarget={dragTarget}
          />
        ))}
      </div>
      <DragOverlay modifiers={[snapOverlayToCursor]}>
        {activeActivity ? (
          <ActivityDragPreview
            activity={activeActivity}
            token={dashboard.session?.accessToken}
            householdId={dashboard.activeHouseholdId}
          />
        ) : null}
      </DragOverlay>
    </DndContext>
  );
}

function KanbanColumn({
  group,
  dashboard,
  activeActivityId,
  dragTarget,
}: {
  group: { status: Activity["status"]; label: string; hint: string; items: Activity[] };
  dashboard: ProjectDashboardController;
  activeActivityId: string | null;
  dragTarget: ActivityDragTarget;
}) {
  const { setNodeRef, isOver } = useDroppable({
    id: `column:${group.status}`,
    data: { type: "column", status: group.status },
  });

  const isDropTarget = Boolean(dragTarget && dragTarget.status === group.status);
  const draggedActivityId = dragTarget?.type === "activity" ? dragTarget.activityId : null;

  return (
    <KanbanColumnFrame
      group={group}
      isDropTarget={isDropTarget || isOver}
      setNodeRef={setNodeRef}
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
                token={dashboard.session?.accessToken}
                householdId={dashboard.activeHouseholdId}
                onOpen={() => dashboard.openActivity(activity)}
                onEdit={activity.canEdit ? () => dashboard.openEditActivity(activity) : undefined}
                onDelete={
                  activity.canDelete
                    ? () => void dashboard.deleteActivity(activity).catch(() => undefined)
                    : undefined
                }
                isDropTarget={dragTarget?.type === "activity" && draggedActivityId === activity.id && activity.id !== activeActivityId}
              />
            ))
          )}
        </div>
      </SortableContext>
    </KanbanColumnFrame>
  );
}

export function KanbanColumnFrame({
  group,
  isDropTarget,
  setNodeRef,
  children,
}: {
  group: { status: Activity["status"]; label: string; hint: string; items: Activity[] };
  isDropTarget: boolean;
  setNodeRef: (node: HTMLElement | null) => void;
  children: ReactNode;
}) {
  return (
    <div
      className={cn(
        "rounded-[20px] border border-border/60 bg-surface-elevated p-2.5 shadow-xs transition-all duration-200",
        statusSectionStyles[group.status].card,
        isDropTarget && "border-primary/35 shadow-md shadow-primary/10",
      )}
      data-drop-target={isDropTarget ? "true" : "false"}
    >
      <div
        className={cn(
          "flex items-start justify-between gap-3 rounded-[16px] p-3 transition-all duration-200",
          statusSectionStyles[group.status].header,
          isDropTarget && "bg-highlight ring-1 ring-primary/20",
        )}
      >
        <div>
          <h3 className="text-sm font-semibold text-foreground">{group.label}</h3>
          <p className="mt-0.5 text-xs text-muted-foreground">{group.hint}</p>
        </div>
        <Badge variant="neutral">{group.items.length}</Badge>
      </div>

      <div
        ref={setNodeRef}
        className={cn(
          "mt-2.5 min-h-[240px] rounded-[16px] border border-dashed p-2 transition-all duration-200",
          statusSectionStyles[group.status].dropzone,
          isDropTarget && "border-primary/45 bg-highlight/80 shadow-inner shadow-primary/5",
        )}
      >
        {children}
      </div>
    </div>
  );
}

function SortableActivityCard({
  activity,
  token,
  householdId,
  onOpen,
  onEdit,
  onDelete,
  isDropTarget,
}: {
  activity: Activity;
  token?: string;
  householdId?: string;
  onOpen: () => void;
  onEdit?: () => void;
  onDelete?: () => void;
  isDropTarget: boolean;
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
      className={cn(isDragging && "opacity-20")}
    >
      <ActivityCard
        activity={activity}
        token={token}
        householdId={householdId}
        onOpen={onOpen}
        onEdit={onEdit}
        onDelete={onDelete}
        dragHandleProps={activity.canEdit ? { ...attributes, ...listeners } : undefined}
        dragging={isDragging}
        isDropTarget={isDropTarget}
      />
    </div>
  );
}

export function ActivityCard({
  activity,
  token,
  householdId,
  onOpen,
  onEdit,
  onDelete,
  dragging = false,
  isDropTarget = false,
  dragHandleProps,
}: {
  activity: Activity;
  token?: string;
  householdId?: string;
  onOpen: () => void;
  onEdit?: () => void;
  onDelete?: () => void;
  dragging?: boolean;
  isDropTarget?: boolean;
  dragHandleProps?: Record<string, unknown>;
}) {
  return (
    <Card
      className={cn(
        "border-border/70 bg-surface-strong transition-all duration-200",
        dragging && "scale-[0.985] rotate-1 opacity-20 shadow-sm",
        isDropTarget && "border-primary/35 bg-highlight shadow-md shadow-primary/10 ring-1 ring-primary/20",
      )}
      data-drop-target={isDropTarget ? "true" : "false"}
      data-dragging={dragging ? "true" : "false"}
    >
      <CardContent className="space-y-3 p-3">
        <div className="flex items-start gap-2">
          <button className="min-w-0 flex-1 text-left" type="button" onClick={onOpen}>
            {activity.hasImage ? (
              <ProtectedActivityImageFrame
                activityId={activity.id}
                title={activity.title}
                hasImage={activity.hasImage}
                imageUpdatedAt={activity.imageUpdatedAt}
                token={token}
                householdId={householdId}
                className="mb-3 rounded-[18px]"
              />
            ) : null}
            <h4 className="truncate text-sm font-semibold text-foreground">{activity.title}</h4>
            <div className="mt-1 flex min-w-0 items-center gap-2">
              <UniverseAvatar
                universeId={activity.universeId}
                name={activity.universeName}
                imageUrl={activity.universeImageUrl}
                hasImage={activity.universeHasImage}
                imageUpdatedAt={activity.universeImageUpdatedAt}
                token={token}
                householdId={householdId}
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
          {activity.size != null ? <Badge variant="neutral">{activity.size} pts</Badge> : null}
          {activity.hasImage ? <Badge variant="neutral">Imagem</Badge> : null}
          <Badge variant="neutral">{activity.dueDate ? `Prazo ${formatDateOnly(activity.dueDate)}` : "Sem prazo"}</Badge>
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

export function ActivityDragPreview({
  activity,
  token,
  householdId,
}: {
  activity: Activity;
  token?: string;
  householdId?: string;
}) {
  return (
    <Card
      className={cn(
        "pointer-events-none w-[min(340px,calc(100vw-2rem))] rounded-full border-primary/25 bg-surface-strong shadow-lg shadow-primary/10 ring-1 ring-primary/10",
      )}
      data-drag-preview="true"
    >
      <CardContent className="flex items-center gap-3 p-2.5">
        <UniverseAvatar
          universeId={activity.universeId}
          name={activity.universeName}
          imageUrl={activity.universeImageUrl}
          hasImage={activity.universeHasImage}
          imageUpdatedAt={activity.universeImageUpdatedAt}
          token={token}
          householdId={householdId}
          className="size-9 shrink-0"
        />
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-semibold text-foreground">{activity.title}</p>
          <p className="truncate text-[12px] text-muted-foreground">
            {activity.universeName} / {activity.projectName}
          </p>
        </div>
        <div className="flex shrink-0 items-center gap-1">
          <Badge variant={getPriorityVariant(activity.priority)}>{priorityLabels[activity.priority]}</Badge>
          {activity.size != null ? <Badge variant="neutral">{activity.size} pts</Badge> : null}
          {activity.hasImage ? <Badge variant="neutral">Imagem</Badge> : null}
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

function UniverseDialog({
  universe,
  open,
  onOpenChange,
  token,
  householdId,
  onSave,
}: {
  universe: Universe | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  token?: string;
  householdId?: string;
  onSave: (input: { name: string; imageFile?: File | null; removeImage?: boolean }) => Promise<void>;
}) {
  const [name, setName] = useState(universe?.name ?? "");
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [removeImage, setRemoveImage] = useState(false);
  const [fileInputKey, setFileInputKey] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const isEditing = Boolean(universe);
  const previewUrl = useObjectUrl(imageFile);
  const currentImageUrl = useProtectedUniverseImage({
    universeId: universe?.id,
    imageUrl: universe?.imageUrl,
    hasImage: universe?.hasImage,
    imageUpdatedAt: universe?.imageUpdatedAt,
    token,
    householdId,
  });
  const displayImageUrl = removeImage ? null : previewUrl ?? currentImageUrl;

  function handleImageChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    if (!["image/jpeg", "image/png", "image/webp"].includes(file.type)) {
      setError("Envie uma imagem JPG, PNG ou WEBP.");
      event.target.value = "";
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      setError("A imagem do universo deve ter no máximo 5 MB.");
      event.target.value = "";
      return;
    }

    setError(null);
    setImageFile(file);
    setRemoveImage(false);
  }

  function discardSelectedImage() {
    setImageFile(null);
    setFileInputKey((current) => current + 1);
    setError(null);
  }

  function removeCurrentImage() {
    setImageFile(null);
    setRemoveImage(true);
    setFileInputKey((current) => current + 1);
    setError(null);
  }

  function restoreCurrentImage() {
    setRemoveImage(false);
    setError(null);
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSaving(true);

    try {
      await onSave({ name, imageFile, removeImage });
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
          <Field label="Imagem do universo">
            <Input
              key={fileInputKey}
              type="file"
              accept="image/jpeg,image/png,image/webp"
              onChange={handleImageChange}
            />
            <p className="mt-2 text-xs text-muted-foreground">Envie JPG, PNG ou WEBP com até 5 MB.</p>
          </Field>
          {displayImageUrl || removeImage ? (
            <div className="flex items-center gap-3 rounded-[16px] border border-border/60 bg-surface-muted p-3">
              <ProtectedUniverseAvatar
                universeId={universe?.id}
                name={name || "Universo"}
                imageUrl={universe?.imageUrl}
                hasImage={universe?.hasImage}
                imageUpdatedAt={universe?.imageUpdatedAt}
                token={token}
                householdId={householdId}
                previewUrl={displayImageUrl}
                className="size-12"
              />
              <div className="min-w-0">
                <p className="text-sm font-semibold text-foreground">{name || "Prévia do universo"}</p>
                <p className="truncate text-xs text-muted-foreground">
                  {imageFile
                    ? imageFile.name
                    : removeImage
                      ? "A imagem será removida ao salvar."
                      : "Imagem atual do universo."}
                </p>
              </div>
            </div>
          ) : null}
          <div className="flex items-center gap-2">
            {imageFile ? (
              <Button variant="ghost" type="button" onClick={discardSelectedImage}>
                Descartar nova imagem
              </Button>
            ) : null}
            {!imageFile && universe && !removeImage && (universe.hasImage || universe.imageUrl) ? (
              <Button variant="ghost" type="button" onClick={removeCurrentImage}>
                Remover imagem atual
              </Button>
            ) : null}
            {!imageFile && universe && removeImage ? (
              <Button variant="ghost" type="button" onClick={restoreCurrentImage}>
                Manter imagem atual
              </Button>
            ) : null}
          </div>
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

export function ActivityDialog({
  activity,
  projects,
  members,
  defaultProjectId,
  token,
  householdId,
  open,
  onOpenChange,
  onSave,
}: {
  activity: Activity | null;
  projects: Project[];	
  members: HouseholdMember[];
  defaultProjectId: string;
  token?: string;
  householdId?: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSave: (input: ActivityFormInput) => Promise<void>;
}) {
  const [projectId, setProjectId] = useState(activity?.projectId || defaultProjectId || projects[0]?.id || "");
  const [title, setTitle] = useState(activity?.title ?? "");
  const [description, setDescription] = useState(activity?.description ?? "");
  const [dueDate, setDueDate] = useState(activity?.dueDate ?? "");
  const [status, setStatus] = useState<ActivityStatus>(activity?.status ?? "NaoIniciada");
  const [priority, setPriority] = useState<Priority>(activity?.priority ?? "Media");
  const [size, setSize] = useState(activity?.size != null ? String(activity.size) : "");
  const [responsibleMemberId, setResponsibleMemberId] = useState(activity?.responsibleMemberId ?? "");
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [removeImage, setRemoveImage] = useState(false);
  const [fileInputKey, setFileInputKey] = useState(0);
  const previewUrl = useObjectUrl(imageFile);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const isEditing = Boolean(activity);
  const hasCurrentImage = Boolean(activity?.hasImage) && !removeImage && !imageFile;
  const activityImageLabel = title || "Atividade";

  function handleImageChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    if (!["image/jpeg", "image/png", "image/webp"].includes(file.type)) {
      setError("Envie uma imagem JPG, PNG ou WEBP.");
      event.target.value = "";
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      setError("A imagem da atividade deve ter no máximo 5 MB.");
      event.target.value = "";
      return;
    }

    setError(null);
    setImageFile(file);
    setRemoveImage(false);
  }

  function discardSelectedImage() {
    setImageFile(null);
    setFileInputKey((current) => current + 1);
    setError(null);
  }

  function removeCurrentImage() {
    setImageFile(null);
    setRemoveImage(true);
    setFileInputKey((current) => current + 1);
    setError(null);
  }

  function restoreCurrentImage() {
    setRemoveImage(false);
    setError(null);
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSaving(true);

    try {
      await onSave({
        projectId,
        title,
        description,
        dueDate,
        status,
        priority,
        size: size ? Number(size) : undefined,
        responsibleMemberId,
        imageFile,
        removeImage,
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
          <DialogDescription>Use um título direto e deixe descrição, prazo e tamanho para dar contexto operacional.</DialogDescription>
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
          <Field label="Imagem da atividade">
            <div className="space-y-3">
              <ProtectedActivityImageFrame
                activityId={activity?.id ?? "preview"}
                title={activityImageLabel}
                hasImage={hasCurrentImage}
                imageUpdatedAt={activity?.imageUpdatedAt}
                token={token}
                householdId={householdId}
                previewUrl={previewUrl}
                className="rounded-[20px]"
              />
              <Input
                key={fileInputKey}
                type="file"
                accept="image/jpeg,image/png,image/webp"
                onChange={handleImageChange}
              />
              <p className="text-xs text-muted-foreground">JPG, PNG ou WEBP, com até 5 MB.</p>
              <div className="flex flex-wrap gap-2">
                {imageFile ? (
                  <Button variant="ghost" type="button" onClick={discardSelectedImage}>
                    Descartar nova imagem
                  </Button>
                ) : null}
                {!imageFile && activity && !removeImage && (activity.hasImage || activity.imageUpdatedAt) ? (
                  <Button variant="ghost" type="button" onClick={removeCurrentImage}>
                    Remover imagem atual
                  </Button>
                ) : null}
                {!imageFile && activity && removeImage ? (
                  <Button variant="ghost" type="button" onClick={restoreCurrentImage}>
                    Manter imagem atual
                  </Button>
                ) : null}
              </div>
            </div>
          </Field>
          <Field label="Prazo esperado">
            <Input type="date" value={dueDate} onChange={(event) => setDueDate(event.target.value)} />
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

export function ActivityDetailsSheet({
  activity,
  token,
  householdId,
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
  token?: string;
  householdId?: string;
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
                  universeId={activity.universeId}
                  name={activity.universeName}
                  imageUrl={activity.universeImageUrl}
                  hasImage={activity.universeHasImage}
                  imageUpdatedAt={activity.universeImageUpdatedAt}
                  token={token}
                  householdId={householdId}
                  className="size-6"
                />
                <span className="text-xs font-semibold text-foreground">{activity.universeName}</span>
              </div>
              <Badge variant="neutral">{activity.projectName}</Badge>
            </div>
            <div>
              <SheetTitle>{activity.title}</SheetTitle>
              <SheetDescription className="mt-2 whitespace-pre-wrap break-words">
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
            {activity.hasImage ? (
              <ProtectedActivityImageFrame
                activityId={activity.id}
                title={activity.title}
                hasImage={activity.hasImage}
                imageUpdatedAt={activity.imageUpdatedAt}
                token={token}
                householdId={householdId}
                className="rounded-[24px]"
              />
            ) : null}
          </div>
        </SheetHeader>

        <div className="space-y-4 p-5 pt-4">
          <div className="grid gap-3 sm:grid-cols-2">
            <DetailCard label="Status" value={activityColumns.find((column) => column.status === activity.status)?.label ?? activity.status} />
            <DetailCard label="Prioridade" value={priorityLabels[activity.priority]} />
            <DetailCard label="Responsável" value={activity.responsibleName ?? "Sem responsável"} />
            <DetailCard label="Tamanho" value={activity.size != null ? `${activity.size} pts` : "Sem tamanho"} />
            <DetailCard label="Prazo esperado" value={formatDateOnly(activity.dueDate)} />
            <DetailCard label="Criada em" value={formatDateTime(activity.createdAt)} />
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

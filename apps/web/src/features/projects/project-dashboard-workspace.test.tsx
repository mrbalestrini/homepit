import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { Activity, SpaceMember, Project, Core } from "@/lib/api";
import * as api from "@/lib/api";
import { ActivityImageViewerDialog, clampActivityImageZoom, stepActivityImageZoom } from "./activity-image-viewer";
import {
  ActivityCard,
  ActivityDialog,
  ActivityDetailsSheet,
  ActivityDragPreview,
  ActivityListView,
  KanbanColumnFrame,
  ProjectDashboardWorkspace,
} from "./project-dashboard-workspace";
import { defaultActivityFilters } from "./project-dashboard.constants";

vi.mock("@/lib/api", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api")>("@/lib/api");

  return {
    ...actual,
    apiFetchBlob: vi.fn(),
  };
});

vi.mock("@/features/workspace/protected-core-avatar", () => ({
  ProtectedCoreAvatar: ({ name, className }: { name: string; className?: string }) => (
    <div data-testid="core-avatar" className={className}>
      {name}
    </div>
  ),
  useProtectedCoreImage: () => null,
}));

vi.mock("@/features/workspace/organiza-club-workspace-shell", () => ({
  OrganizaClubWorkspaceShell: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  EmptyState: ({ title }: { title: string }) => <div>{title}</div>,
  Field: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  LoadingState: ({ title }: { title: string }) => <div>{title}</div>,
  Notice: ({ children }: { children: ReactNode }) => <div>{children}</div>,
}));

function buildActivity(overrides: Partial<Activity> & Pick<Activity, "id" | "title">): Activity {
  return {
    id: overrides.id,
    projectId: "project-1",
    projectName: "Projeto Alfa",
    coreId: "core-1",
    coreName: "Núcleo Alfa",
    coreImageUrl: null,
    coreHasImage: false,
    coreImageUpdatedAt: null,
    createdByMemberId: null,
    createdAt: "2026-06-20T12:00:00.000Z",
    title: overrides.title,
    description: "Descrição de apoio para o card de teste.",
    hasImage: false,
    imageUpdatedAt: null,
    dueDate: "2026-06-30",
    completedAt: null,
    status: "NaoIniciada",
    priority: "Media",
    size: 3,
    responsibleMemberId: null,
    responsibleName: null,
    pendingCount: 2,
    commentCount: 4,
    canEdit: true,
    canDelete: true,
    ...overrides,
  };
}

function buildMember(overrides: Partial<SpaceMember> & Pick<SpaceMember, "id" | "userId" | "displayName">): SpaceMember {
  return {
    id: overrides.id,
    userId: overrides.userId,
    displayName: overrides.displayName,
    email: "member@organiza.club",
    phoneNumber: null,
    hasProfilePhoto: false,
    profilePhotoUpdatedAt: null,
    role: "Member",
    isCurrentUser: false,
    ...overrides,
  };
}

function buildCore(overrides: Partial<Core> & Pick<Core, "id" | "name">): Core {
  return {
    id: overrides.id,
    name: overrides.name,
    imageUrl: null,
    hasImage: false,
    imageUpdatedAt: null,
    createdByMemberId: null,
    projectCount: 1,
    isOutOfPlan: false,
    canEdit: true,
    canDelete: true,
    ...overrides,
  };
}

function buildProject(overrides: Partial<Project> & Pick<Project, "id" | "name">): Project {
  return {
    id: overrides.id,
    coreId: "core-1",
    coreName: "Núcleo Alfa",
    coreImageUrl: null,
    coreHasImage: false,
    coreImageUpdatedAt: null,
    name: overrides.name,
    createdByMemberId: null,
    activityCount: 1,
    isOutOfPlan: false,
    canEdit: true,
    canDelete: true,
    ...overrides,
  };
}

function buildDashboard(overrides?: {
  cores?: Core[];
  projects?: Project[];
}) {
  const cores = overrides?.cores ?? [];
  const projects = overrides?.projects ?? [];

  return {
    session: { accessToken: "token", user: { id: "user-1", accountState: "Active" } },
    activeSpaceId: "space-1",
    activeSpace: null,
    members: [],
    theme: "dark",
    sidebarCollapsed: false,
    loading: false,
    error: null,
    canShareSpace: false,
    canManageSpace: false,
    canAssignActivityToMe: () => false,
    editingSpace: null,
    editingCore: null,
    editingProject: null,
    editingActivity: null,
    activeModal: null,
    setError: () => undefined,
    setSidebarCollapsed: () => undefined,
    setTheme: () => undefined,
    handleSpaceChange: () => undefined,
    handleLogout: () => undefined,
    refreshSpaces: async () => undefined,
    loadWorkspace: async () => undefined,
    openCreateSpace: () => undefined,
    openEditSpace: () => undefined,
    openShareSpace: () => undefined,
    closeModal: () => undefined,
    createSpace: async () => undefined,
    updateSpace: async () => undefined,
    deleteSpace: async () => undefined,
    shareSpace: async () => undefined,
    selectedScopeLabel: "Todos os projetos",
    visibleActivities: [],
    cores,
    projects,
    activities: [],
    selectedCoreId: "",
    selectedProjectId: "",
    selectAllScopes: () => undefined,
    selectCoreScope: () => undefined,
    selectProjectScope: () => undefined,
    openCreateCore: () => undefined,
    openCreateProject: () => undefined,
    openEditCore: () => undefined,
    openEditProject: () => undefined,
    deleteCore: async () => undefined,
    deleteProject: async () => undefined,
    filters: defaultActivityFilters,
    updateFilter: () => undefined,
    resetFilters: () => undefined,
    viewMode: "kanban",
    setViewMode: () => undefined,
    groupedActivities: [],
    openCreateActivity: () => undefined,
    openActivity: () => undefined,
    openEditActivity: () => undefined,
    deleteActivity: async () => undefined,
    updateActivityStatusOptimistic: async () => undefined,
    createCore: async () => undefined,
    updateCore: async () => undefined,
    createProject: async () => undefined,
    updateProject: async () => undefined,
    activityDialogProjects: [],
    activityDraftProjectId: "",
    selectedActivity: null,
    activityComments: [],
    commentsLoading: false,
    createComment: async () => undefined,
    updateComment: async () => undefined,
    deleteComment: async () => undefined,
    moveActivity: async () => undefined,
    closeActivity: () => undefined,
    createActivity: async () => undefined,
    updateActivity: async () => undefined,
    assignActivityToMe: async () => undefined,
  } as never;
}

describe("project dashboard kanban drag states", () => {
  beforeEach(() => {
    let objectUrlCounter = 0;
    vi.stubGlobal(
      "URL",
      Object.assign(URL, {
        createObjectURL: vi.fn(() => `blob:activity-${++objectUrlCounter}`),
        revokeObjectURL: vi.fn(),
      }),
    );
    Object.defineProperty(HTMLElement.prototype, "setPointerCapture", {
      configurable: true,
      value: vi.fn(),
    });
    Object.defineProperty(HTMLElement.prototype, "releasePointerCapture", {
      configurable: true,
      value: vi.fn(),
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("renders a compact drag preview with core avatar and minimal metadata", () => {
    const activity = buildActivity({ id: "activity-1", title: "Comprar tinta" });

    const { container } = render(<ActivityDragPreview activity={activity} />);
    const root = container.firstElementChild as HTMLElement;

    expect(root).toHaveAttribute("data-drag-preview", "true");
    expect(root).toHaveClass("rounded-full");
    expect(screen.getByTestId("core-avatar")).toHaveTextContent("Núcleo Alfa");
    expect(screen.getByText("Comprar tinta")).toBeInTheDocument();
    expect(screen.getByText("Núcleo Alfa / Projeto Alfa")).toBeInTheDocument();
    expect(screen.getByText("Média")).toBeInTheDocument();
    expect(screen.queryByText("Prazo 30/06/2026")).not.toBeInTheDocument();
  });

  it("ghosts the dragged source card and highlights the drop target card", () => {
    const dragged = buildActivity({ id: "activity-1", title: "Card arrastado" });
    const target = buildActivity({ id: "activity-2", title: "Card destino" });

    const { container: draggedContainer } = render(
      <ActivityCard
        activity={dragged}
        members={[]}
        onOpen={() => undefined}
        dragging
        token="token"
        spaceId="space-1"
      />,
    );
    const draggedRoot = draggedContainer.firstElementChild as HTMLElement;

    expect(draggedRoot).toHaveAttribute("data-dragging", "true");
    expect(draggedRoot).toHaveClass("opacity-20");

    const { container: targetContainer } = render(
      <ActivityCard
        activity={target}
        members={[]}
        onOpen={() => undefined}
        token="token"
        spaceId="space-1"
        isDropTarget
      />,
    );
    const targetRoot = targetContainer.firstElementChild as HTMLElement;

    expect(targetRoot).toHaveAttribute("data-drop-target", "true");
    expect(targetRoot).toHaveClass("ring-1");
    expect(targetRoot).toHaveClass("bg-highlight");
    expect(screen.getAllByText("Prazo 30/06/2026").length).toBeGreaterThan(0);
  });

  it("renders the due date in the list and the creation timestamp in the details sheet", () => {
    const activity = buildActivity({ id: "activity-1", title: "Montar prateleira" });

    render(
      <ActivityListView
        dashboard={
          {
            groupedActivities: [
              {
                status: "NaoIniciada",
                label: "Nao iniciadas",
                hint: "Aguardando acao",
                items: [activity],
              },
            ],
            members: [],
            session: { accessToken: "token" },
            activeSpaceId: "space-1",
            openActivity: () => undefined,
            canAssignActivityToMe: () => false,
            assignActivityToMe: async () => undefined,
            openEditActivity: () => undefined,
            deleteActivity: async () => undefined,
          } as never
        }
      />,
    );

    expect(screen.getByText("30/06/2026")).toBeInTheDocument();

    render(
      <ActivityDetailsSheet
        activity={activity}
        token="token"
        spaceId="space-1"
        comments={[]}
        commentsLoading={false}
        onClose={() => undefined}
        onCreateComment={async () => undefined}
        onUpdateComment={async () => undefined}
        onDeleteComment={async () => undefined}
        onMove={async () => undefined}
        onEditActivity={() => undefined}
        onDeleteActivity={async () => undefined}
        onOpenImage={() => undefined}
      />,
    );

    expect(screen.getByText("Prazo esperado")).toBeInTheDocument();
    expect(screen.getByText("Data concluída")).toBeInTheDocument();
    expect(screen.getByText("Criada em")).toBeInTheDocument();
    expect(screen.getAllByText("30/06/2026").length).toBeGreaterThan(0);
  });

  it("renders the activity image preview on the kanban card when an attachment exists", async () => {
    const mockedApiFetchBlob = vi.mocked(api.apiFetchBlob);
    mockedApiFetchBlob.mockResolvedValue(new Blob([1, 2, 3, 4], { type: "image/png" }));
    const activity = buildActivity({
      id: "activity-1",
      title: "Card com imagem",
      hasImage: true,
      imageUpdatedAt: "2026-06-20T12:00:00.000Z",
    });

    render(
      <ActivityCard
        activity={activity}
        members={[]}
        onOpen={() => undefined}
        onOpenImage={() => undefined}
        token="token"
        spaceId="space-1"
      />,
    );

    await waitFor(() => expect(screen.getByRole("button", { name: "Abrir imagem de Card com imagem" })).toBeInTheDocument());
    expect(screen.getByText("Imagem")).toBeInTheDocument();
  });

  it("renders the comment author photo when the profile photo exists", async () => {
    const mockedApiFetchBlob = vi.mocked(api.apiFetchBlob);
    mockedApiFetchBlob.mockResolvedValue(new Blob([1, 2, 3, 4], { type: "image/png" }));
    const activity = buildActivity({ id: "activity-1", title: "Montar prateleira" });

    render(
      <ActivityDetailsSheet
        activity={activity}
        members={[]}
        token="token"
        spaceId="space-1"
        comments={[
          {
            id: "comment-1",
            activityId: activity.id,
            authorMemberId: "member-1",
            authorUserId: "user-1",
            authorName: "Ana Teste",
            authorHasProfilePhoto: true,
            authorProfilePhotoUpdatedAt: "2026-06-20T12:00:00.000Z",
            body: "Comentário com foto.",
            createdAt: "2026-06-20T12:00:00.000Z",
            isEdited: false,
            canEdit: true,
            canDelete: true,
          },
        ]}
        commentsLoading={false}
        onClose={() => undefined}
        onCreateComment={async () => undefined}
        onUpdateComment={async () => undefined}
        onDeleteComment={async () => undefined}
        onMove={async () => undefined}
        onEditActivity={() => undefined}
        onDeleteActivity={async () => undefined}
        onOpenImage={() => undefined}
      />,
    );

    await waitFor(() => expect(screen.getByAltText("Ana Teste")).toHaveAttribute("src", "blob:activity-1"));
  });

  it("renders the compact image controls in the editor when an attachment exists", async () => {
    const mockedApiFetchBlob = vi.mocked(api.apiFetchBlob);
    mockedApiFetchBlob.mockResolvedValue(new Blob([1, 2, 3, 4], { type: "image/png" }));
    const activity = buildActivity({
      id: "activity-1",
      title: "Montar prateleira",
      hasImage: true,
      imageUpdatedAt: "2026-06-20T12:00:00.000Z",
    });

    render(
      <ActivityDialog
        open
        activity={activity}
        projects={[
          {
            id: "project-1",
            coreId: "core-1",
            coreName: "Núcleo Alfa",
            coreImageUrl: null,
            coreHasImage: false,
            coreImageUpdatedAt: null,
            name: "Projeto Alfa",
            createdByMemberId: null,
            activityCount: 1,
            isOutOfPlan: false,
            canEdit: true,
            canDelete: true,
          },
        ]}
        members={[
          {
            id: "member-1",
            userId: "user-1",
            displayName: "Ana Teste",
            email: "ana@example.com",
            phoneNumber: null,
            hasProfilePhoto: false,
            profilePhotoUpdatedAt: null,
            role: "Owner",
            isCurrentUser: true,
          },
        ]}
        defaultProjectId="project-1"
        token="token"
        spaceId="space-1"
        onOpenImage={() => undefined}
        onOpenChange={() => undefined}
        onSave={async () => undefined}
      />,
    );

    await waitFor(() => expect(screen.getByRole("button", { name: "Abrir imagem de Montar prateleira" })).toBeInTheDocument());
    expect(screen.getByText("Remover imagem atual")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Trocar imagem" })).toBeInTheDocument();
    expect(document.querySelector('input[type="file"]')).toBeNull();
  });

  it("renders only the file input when the editor has no image", () => {
    render(
      <ActivityDialog
        open
        activity={null}
        projects={[
          {
            id: "project-1",
            coreId: "core-1",
            coreName: "Núcleo Alfa",
            coreImageUrl: null,
            coreHasImage: false,
            coreImageUpdatedAt: null,
            name: "Projeto Alfa",
            createdByMemberId: null,
            activityCount: 1,
            isOutOfPlan: false,
            canEdit: true,
            canDelete: true,
          },
        ]}
        members={[]}
        defaultProjectId="project-1"
        token="token"
        spaceId="space-1"
        onOpenImage={() => undefined}
        onOpenChange={() => undefined}
        onSave={async () => undefined}
      />,
    );

    expect(screen.queryByRole("button", { name: /Abrir imagem de/ })).not.toBeInTheDocument();
    expect(document.querySelector('input[type="file"]')).not.toBeNull();
  });

  it("opens the image viewer from the card and the detail sheet", async () => {
    const mockedApiFetchBlob = vi.mocked(api.apiFetchBlob);
    mockedApiFetchBlob.mockResolvedValue(new Blob([1, 2, 3, 4], { type: "image/png" }));
    const activity = buildActivity({
      id: "activity-1",
      title: "Card com imagem",
      hasImage: true,
      imageUpdatedAt: "2026-06-20T12:00:00.000Z",
    });
    const openImage = vi.fn();

    render(
      <ActivityCard
        activity={activity}
        members={[]}
        onOpen={() => undefined}
        onOpenImage={openImage}
        token="token"
        spaceId="space-1"
      />,
    );

    await waitFor(() => screen.getByRole("button", { name: "Abrir imagem de Card com imagem" }));
    fireEvent.click(screen.getByRole("button", { name: "Abrir imagem de Card com imagem" }));
    expect(openImage).toHaveBeenCalledWith("Card com imagem", expect.stringContaining("blob:activity-"));

    const detailOpenImage = vi.fn();
    render(
      <ActivityDetailsSheet
        activity={activity}
        members={[]}
        token="token"
        spaceId="space-1"
        comments={[]}
        commentsLoading={false}
        onClose={() => undefined}
        onCreateComment={async () => undefined}
        onUpdateComment={async () => undefined}
        onDeleteComment={async () => undefined}
        onMove={async () => undefined}
        onEditActivity={() => undefined}
        onDeleteActivity={async () => undefined}
        onOpenImage={detailOpenImage}
      />,
    );

    await waitFor(() => screen.getByRole("button", { name: "Abrir imagem de Card com imagem" }));
    fireEvent.click(screen.getByRole("button", { name: "Abrir imagem de Card com imagem" }));
    expect(detailOpenImage).toHaveBeenCalledWith("Card com imagem", expect.stringContaining("blob:activity-"));
  });

  it("renders and manipulates the activity image viewer", async () => {
    render(
      <ActivityImageViewerDialog
        open
        title="Imagem da atividade"
        imageUrl="blob:viewer-1"
        onOpenChange={() => undefined}
      />,
    );

    const stage = screen.getByLabelText("Área de visualização da imagem");
    const image = screen.getByAltText("Imagem da atividade");

    expect(image.style.transform).toBe("translate3d(0px, 0px, 0) scale(1)");

    fireEvent.click(screen.getByRole("button", { name: "Aumentar zoom" }));
    await waitFor(() => expect(image.style.transform).toContain("scale(1.2)"));

    fireEvent.pointerDown(stage, { clientX: 20, clientY: 20, pointerId: 1 });
    fireEvent.pointerMove(stage, { clientX: 40, clientY: 35, pointerId: 1 });
    fireEvent.pointerUp(stage, { clientX: 40, clientY: 35, pointerId: 1 });

    expect(image.style.transform).toContain("scale(1.2)");
    fireEvent.click(screen.getByRole("button", { name: "Redefinir" }));
    expect(image.style.transform).toBe("translate3d(0px, 0px, 0) scale(1)");
  });

  it("clamps the viewer zoom helpers", () => {
    expect(clampActivityImageZoom(0.5)).toBe(1);
    expect(clampActivityImageZoom(7)).toBe(4);
    expect(stepActivityImageZoom(1, -1)).toBe(1);
    expect(stepActivityImageZoom(1, 1)).toBeCloseTo(1.2);
  });

  it("renders the responsible member photo in the table when it exists", async () => {
    const mockedApiFetchBlob = vi.mocked(api.apiFetchBlob);
    mockedApiFetchBlob.mockResolvedValue(new Blob([1, 2, 3], { type: "image/png" }));
    const activity = buildActivity({
      id: "activity-responsible-table",
      title: "Atividade com responsável",
      responsibleMemberId: "member-1",
      responsibleName: "Ana Responsável",
    });
    const member = buildMember({
      id: "member-1",
      userId: "user-1",
      displayName: "Ana Responsável",
      hasProfilePhoto: true,
      profilePhotoUpdatedAt: "2026-06-26T12:00:00.000Z",
    });

    render(
      <ActivityListView
        dashboard={
          {
            groupedActivities: [
              {
                status: "NaoIniciada",
                label: "Nao iniciadas",
                hint: "Aguardando acao",
                items: [activity],
              },
            ],
            members: [member],
            session: { accessToken: "token" },
            activeSpaceId: "space-1",
            openActivity: () => undefined,
            canAssignActivityToMe: () => false,
            assignActivityToMe: async () => undefined,
            openEditActivity: () => undefined,
            deleteActivity: async () => undefined,
          } as never
        }
      />,
    );

    await waitFor(() => expect(screen.getByAltText("Ana Responsável")).toHaveAttribute("src", "blob:activity-1"));
  });

  it("renders a subtle responsible avatar on the kanban card without the text badge", async () => {
    const mockedApiFetchBlob = vi.mocked(api.apiFetchBlob);
    mockedApiFetchBlob.mockResolvedValue(new Blob([1, 2, 3], { type: "image/png" }));
    const activity = buildActivity({
      id: "activity-responsible-kanban",
      title: "Card com responsável",
      responsibleMemberId: "member-2",
      responsibleName: "Paula Responsável",
    });
    const member = buildMember({
      id: "member-2",
      userId: "user-2",
      displayName: "Paula Responsável",
      hasProfilePhoto: true,
      profilePhotoUpdatedAt: "2026-06-26T12:05:00.000Z",
    });

    render(
      <ActivityCard
        activity={activity}
        members={[member]}
        onOpen={() => undefined}
        token="token"
        spaceId="space-1"
      />,
    );

    await waitFor(() => expect(screen.getByTitle("Paula Responsável")).toBeInTheDocument());
    expect(screen.getByAltText("Paula Responsável")).toHaveAttribute("src", "blob:activity-1");
    expect(screen.queryByText("Paula Responsável")).not.toBeInTheDocument();
  });

  it("highlights the column frame when it is the active drop zone", () => {
    const { container } = render(
      <KanbanColumnFrame
        group={{
          status: "EmAndamento",
          label: "Em andamento",
          hint: "Atividades em execução",
          items: [buildActivity({ id: "activity-1", title: "Tarefa" })],
        }}
        isDropTarget
        setNodeRef={() => undefined}
      >
        <div>Conteúdo da coluna</div>
      </KanbanColumnFrame>,
    );

    const root = container.firstElementChild as HTMLElement;
    expect(root).toHaveAttribute("data-drop-target", "true");
    expect(root).toHaveClass("border-primary/35");
    expect(screen.getByText("Conteúdo da coluna")).toBeInTheDocument();
  });

  it("shows the out-of-plan badge only for users who can manage the item", () => {
    const { rerender } = render(
      <ProjectDashboardWorkspace
        dashboard={buildDashboard({
          cores: [buildCore({ id: "core-1", name: "Núcleo Alfa", isOutOfPlan: true, canEdit: false })],
          projects: [
            buildProject({
              id: "project-1",
              name: "Projeto Alfa",
              coreId: "core-1",
              isOutOfPlan: true,
              canEdit: false,
            }),
          ],
        })}
      />,
    );

    expect(screen.queryByText("Fora do plano")).not.toBeInTheDocument();

    rerender(
      <ProjectDashboardWorkspace
        dashboard={buildDashboard({
          cores: [buildCore({ id: "core-2", name: "Núcleo Beta", isOutOfPlan: true, canEdit: true })],
          projects: [
            buildProject({
              id: "project-2",
              name: "Projeto Beta",
              coreId: "core-2",
              isOutOfPlan: true,
              canEdit: true,
            }),
          ],
        })}
      />,
    );

    expect(screen.getAllByText("Fora do plano")).toHaveLength(2);
  });
});

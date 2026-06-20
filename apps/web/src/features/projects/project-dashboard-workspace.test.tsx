import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { Activity } from "@/lib/api";
import {
  ActivityCard,
  ActivityDetailsSheet,
  ActivityDragPreview,
  ActivityListView,
  KanbanColumnFrame,
} from "./project-dashboard-workspace";

vi.mock("@/features/workspace/protected-universe-avatar", () => ({
  ProtectedUniverseAvatar: ({ name, className }: { name: string; className?: string }) => (
    <div data-testid="universe-avatar" className={className}>
      {name}
    </div>
  ),
  useProtectedUniverseImage: () => null,
}));

function buildActivity(overrides: Partial<Activity> & Pick<Activity, "id" | "title">): Activity {
  return {
    id: overrides.id,
    projectId: "project-1",
    projectName: "Projeto Alfa",
    universeId: "universe-1",
    universeName: "Universo Alfa",
    universeImageUrl: null,
    universeHasImage: false,
    universeImageUpdatedAt: null,
    createdByMemberId: null,
    createdAt: "2026-06-20T12:00:00.000Z",
    title: overrides.title,
    description: "Descrição de apoio para o card de teste.",
    dueDate: "2026-06-30",
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

describe("project dashboard kanban drag states", () => {
  it("renders a compact drag preview with universe avatar and minimal metadata", () => {
    const activity = buildActivity({ id: "activity-1", title: "Comprar tinta" });

    const { container } = render(<ActivityDragPreview activity={activity} />);
    const root = container.firstElementChild as HTMLElement;

    expect(root).toHaveAttribute("data-drag-preview", "true");
    expect(root).toHaveClass("rounded-full");
    expect(screen.getByTestId("universe-avatar")).toHaveTextContent("Universo Alfa");
    expect(screen.getByText("Comprar tinta")).toBeInTheDocument();
    expect(screen.getByText("Universo Alfa / Projeto Alfa")).toBeInTheDocument();
    expect(screen.getByText("Média")).toBeInTheDocument();
    expect(screen.queryByText("Prazo 30/06/2026")).not.toBeInTheDocument();
  });

  it("ghosts the dragged source card and highlights the drop target card", () => {
    const dragged = buildActivity({ id: "activity-1", title: "Card arrastado" });
    const target = buildActivity({ id: "activity-2", title: "Card destino" });

    const { container: draggedContainer } = render(
      <ActivityCard
        activity={dragged}
        onOpen={() => undefined}
        dragging
        token="token"
        householdId="household-1"
      />,
    );
    const draggedRoot = draggedContainer.firstElementChild as HTMLElement;

    expect(draggedRoot).toHaveAttribute("data-dragging", "true");
    expect(draggedRoot).toHaveClass("opacity-20");

    const { container: targetContainer } = render(
      <ActivityCard
        activity={target}
        onOpen={() => undefined}
        token="token"
        householdId="household-1"
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
            session: { accessToken: "token" },
            activeHouseholdId: "household-1",
            openActivity: () => undefined,
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
        householdId="household-1"
        comments={[]}
        commentsLoading={false}
        onClose={() => undefined}
        onCreateComment={async () => undefined}
        onUpdateComment={async () => undefined}
        onDeleteComment={async () => undefined}
        onMove={async () => undefined}
        onEditActivity={() => undefined}
        onDeleteActivity={async () => undefined}
      />,
    );

    expect(screen.getByText("Prazo esperado")).toBeInTheDocument();
    expect(screen.getByText("Criada em")).toBeInTheDocument();
    expect(screen.getAllByText("30/06/2026").length).toBeGreaterThan(0);
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
});

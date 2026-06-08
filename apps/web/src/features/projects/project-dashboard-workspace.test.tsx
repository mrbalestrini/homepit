import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { Activity } from "@/lib/api";
import { ActivityCard, ActivityDragPreview, KanbanColumnFrame } from "./project-dashboard-workspace";

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
    title: overrides.title,
    description: "Descrição de apoio para o card de teste.",
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

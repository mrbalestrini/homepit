import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { CategoryDeleteDialog, PromptCard, PromptDetailDialog } from "./prompt-bank-workspace";

describe("PromptCard", () => {
  it("renders previews with 4:5 image frame and truncates noisy text", () => {
    const longDescription = "A".repeat(180);
    const longPrompt = "B".repeat(260);
    const { container } = render(
      <PromptCard
        prompt={{
          id: "prompt-1",
          universeId: null,
          universeName: null,
          title: "Prompt de teste",
          description: longDescription,
          promptText: longPrompt,
          categories: [{ id: "cat-1", name: "Categoria" }],
          linkUrl: null,
          linkTitle: null,
          createdByMemberId: null,
          hasImage: false,
          imageUpdatedAt: null,
          updatedAt: "2026-06-03T12:00:00Z",
          canEdit: true,
          canDelete: true,
        }}
        token=""
        onOpen={() => undefined}
        onEdit={() => undefined}
        onDelete={() => undefined}
      />,
    );

    const imageFrame = container.querySelector('[class*="aspect-[4/5]"]');
    expect(imageFrame).not.toBeNull();
    expect(screen.getByText(/A{20}/)).toHaveTextContent(/\.\.\.$/);
    expect(screen.getByText(/B{20}/)).toHaveTextContent(/\.\.\.$/);
  });
});

describe("PromptDetailDialog", () => {
  it("shows full prompt details and external link", () => {
    render(
      <PromptDetailDialog
        open
        prompt={{
          id: "prompt-1",
          universeId: "uni-1",
          universeName: "Universo",
          title: "Prompt detalhado",
          description: "Descrição completa",
          promptText: "Texto integral do prompt sem truncamento.",
          categories: [{ id: "cat-1", name: "Categoria" }],
          linkUrl: "https://homepit.dev",
          linkTitle: "Referência oficial",
          createdByMemberId: null,
          hasImage: false,
          imageUpdatedAt: null,
          createdAt: "2026-06-03T12:00:00Z",
          updatedAt: "2026-06-03T12:00:00Z",
          canEdit: true,
          canDelete: true,
        }}
        loading={false}
        token=""
        onOpenChange={() => undefined}
        onEdit={() => undefined}
        onDelete={() => undefined}
      />,
    );

    expect(screen.getByRole("heading", { name: "Prompt detalhado" })).toBeInTheDocument();
    expect(screen.getByText("Texto integral do prompt sem truncamento.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Referência oficial" })).toHaveAttribute("href", "https://homepit.dev");
  });
});

describe("CategoryDeleteDialog", () => {
  it("passes the replacement category when prompts would be left without category", async () => {
    const onDelete = vi.fn(() => Promise.resolve());

    render(
      <CategoryDeleteDialog
        open
        category={{
          id: "cat-a",
          name: "Categoria A",
          createdByMemberId: null,
          usageCount: 3,
          replacementRequiredCount: 2,
          canEdit: true,
          canDelete: true,
        }}
        categories={[
          {
            id: "cat-a",
            name: "Categoria A",
            createdByMemberId: null,
            usageCount: 3,
            replacementRequiredCount: 2,
            canEdit: true,
            canDelete: true,
          },
          {
            id: "cat-b",
            name: "Categoria B",
            createdByMemberId: null,
            usageCount: 1,
            replacementRequiredCount: 0,
            canEdit: true,
            canDelete: true,
          },
        ]}
        onOpenChange={() => undefined}
        onDelete={onDelete}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Excluir categoria" }));

    expect(onDelete).toHaveBeenCalledWith("cat-a", "cat-b");
  });
});

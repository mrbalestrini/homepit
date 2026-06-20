import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { toast } from "sonner";
import { ApiError, apiFetchBlob } from "@/lib/api";
import { CategoryDeleteDialog, PromptCard, PromptDetailDialog, buildPromptMasonryLayout } from "./prompt-bank-workspace";

vi.mock("@/lib/api", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api")>("@/lib/api");
  return {
    ...actual,
    apiFetchBlob: vi.fn(),
  };
});

vi.mock("sonner", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

beforeEach(() => {
  vi.mocked(apiFetchBlob).mockReset();
});

describe("PromptCard", () => {
  it("renders a compact card when the prompt has no image and truncates noisy text", () => {
    const longDescription = "A".repeat(180);
    const longPrompt = "B".repeat(260);
    const { container } = render(
      <PromptCard
        prompt={{
          id: "prompt-1",
          universeId: null,
          universeName: null,
          universeImageUrl: null,
          universeHasImage: false,
          universeImageUpdatedAt: null,
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
    expect(imageFrame).toBeNull();
    expect(container.firstChild).toHaveClass("w-full");
    expect(container.firstChild).not.toHaveClass("sm:w-[21rem]");
    expect(container.firstChild).toHaveClass("cursor-pointer");
    expect(screen.getByText(/A{20}/)).toHaveTextContent(/\.\.\.$/);
    expect(screen.getByText(/B{20}/)).toHaveTextContent(/\.\.\.$/);
  });

  it("keeps the 4:5 frame when the prompt has an image", () => {
    const { container } = render(
      <PromptCard
        prompt={{
          id: "prompt-1",
          universeId: null,
          universeName: null,
          universeImageUrl: null,
          universeHasImage: false,
          universeImageUpdatedAt: null,
          title: "Prompt visual",
          description: null,
          promptText: "Texto do prompt.",
          categories: [{ id: "cat-1", name: "Categoria" }],
          linkUrl: null,
          linkTitle: null,
          createdByMemberId: null,
          hasImage: true,
          imageUpdatedAt: "2026-06-03T12:00:00Z",
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

    expect(container.querySelector('[class*="aspect-[4/5]"]')).not.toBeNull();
  });

  it("loads the prompt image with the active household", async () => {
    vi.mocked(apiFetchBlob).mockRejectedValueOnce(new ApiError("Arquivo não encontrado.", 404));

    render(
      <PromptCard
        prompt={{
          id: "prompt-1",
          universeId: null,
          universeName: null,
          universeImageUrl: null,
          universeHasImage: false,
          universeImageUpdatedAt: null,
          title: "Prompt visual",
          description: null,
          promptText: "Texto do prompt.",
          categories: [{ id: "cat-1", name: "Categoria" }],
          linkUrl: null,
          linkTitle: null,
          createdByMemberId: null,
          hasImage: true,
          imageUpdatedAt: "2026-06-03T12:00:00Z",
          updatedAt: "2026-06-03T12:00:00Z",
          canEdit: true,
          canDelete: true,
        }}
        token="token-1"
        householdId="household-1"
        onOpen={() => undefined}
        onEdit={() => undefined}
        onDelete={() => undefined}
      />,
    );

    await waitFor(() => {
      expect(apiFetchBlob).toHaveBeenCalledWith("/api/prompts/prompt-1/image", {
        token: "token-1",
        householdId: "household-1",
      });
    });
  });

  it("renders the universe chip with avatar when the universe has an image", () => {
    render(
      <PromptCard
        prompt={{
          id: "prompt-2",
          universeId: "uni-1",
          universeName: "Universo Visual",
          universeImageUrl: "https://cdn.homepit.dev/universo-visual.png",
          universeHasImage: false,
          universeImageUpdatedAt: null,
          title: "Prompt com universo",
          description: null,
          promptText: "Texto do prompt.",
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

    expect(screen.getByAltText("Universo Visual")).toHaveAttribute("src", "https://cdn.homepit.dev/universo-visual.png");
  });
});

describe("buildPromptMasonryLayout", () => {
  it("distributes cards into the shortest column while preserving consistent widths", () => {
    const layout = buildPromptMasonryLayout({
      containerWidth: 700,
      items: [
        { id: "a", height: 420 },
        { id: "b", height: 220 },
        { id: "c", height: 200 },
        { id: "d", height: 180 },
      ],
    });

    expect(layout.columnCount).toBe(2);
    expect(layout.items.map((item) => ({ id: item.id, column: item.column, top: item.top }))).toEqual([
      { id: "a", column: 0, top: 0 },
      { id: "b", column: 1, top: 0 },
      { id: "c", column: 1, top: 236 },
      { id: "d", column: 0, top: 436 },
    ]);
    expect(layout.items.every((item) => item.width === layout.columnWidth)).toBe(true);
    expect(layout.columnWidth).toBe(342);
    expect(layout.height).toBe(616);
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
          universeImageUrl: "https://cdn.homepit.dev/universo.png",
          universeHasImage: false,
          universeImageUpdatedAt: null,
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
    expect(screen.getByAltText("Universo")).toHaveAttribute("src", "https://cdn.homepit.dev/universo.png");
    expect(screen.getByRole("button", { name: "Copiar prompt" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Referência oficial" })).toHaveAttribute("href", "https://homepit.dev");
  });

  it("copies the full prompt text from the detail modal", async () => {
    const writeText = vi.fn(() => Promise.resolve());
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: { writeText },
    });

    render(
      <PromptDetailDialog
        open
        prompt={{
          id: "prompt-1",
          universeId: "uni-1",
          universeName: "Universo",
          universeImageUrl: null,
          universeHasImage: false,
          universeImageUpdatedAt: null,
          title: "Prompt detalhado",
          description: "Descrição completa",
          promptText: "Texto integral do prompt sem truncamento.",
          categories: [{ id: "cat-1", name: "Categoria" }],
          linkUrl: null,
          linkTitle: null,
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

    fireEvent.click(screen.getByRole("button", { name: "Copiar prompt" }));

    await waitFor(() => {
      expect(writeText).toHaveBeenCalledWith("Texto integral do prompt sem truncamento.");
    });
    expect(toast.success).toHaveBeenCalledWith("Prompt copiado.");
  });

  it("loads the detail image with the active household", async () => {
    vi.mocked(apiFetchBlob).mockRejectedValueOnce(new ApiError("Arquivo não encontrado.", 404));

    render(
      <PromptDetailDialog
        open
        prompt={{
          id: "prompt-1",
          universeId: "uni-1",
          universeName: "Universo",
          universeImageUrl: null,
          universeHasImage: false,
          universeImageUpdatedAt: null,
          title: "Prompt detalhado",
          description: "Descrição completa",
          promptText: "Texto integral do prompt sem truncamento.",
          categories: [{ id: "cat-1", name: "Categoria" }],
          linkUrl: null,
          linkTitle: null,
          createdByMemberId: null,
          hasImage: true,
          imageUpdatedAt: "2026-06-03T12:00:00Z",
          createdAt: "2026-06-03T12:00:00Z",
          updatedAt: "2026-06-03T12:00:00Z",
          canEdit: true,
          canDelete: true,
        }}
        loading={false}
        token="token-1"
        householdId="household-1"
        onOpenChange={() => undefined}
        onEdit={() => undefined}
        onDelete={() => undefined}
      />,
    );

    await waitFor(() => {
      expect(apiFetchBlob).toHaveBeenCalledWith("/api/prompts/prompt-1/image", {
        token: "token-1",
        householdId: "household-1",
      });
    });
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

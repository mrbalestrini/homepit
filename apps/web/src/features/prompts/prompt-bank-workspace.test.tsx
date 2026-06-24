import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { toast } from "sonner";
import { ApiError, apiFetchBlob, type PromptDetail, type PromptListItem } from "@/lib/api";
import { uiStorageKeys } from "@/features/projects/project-dashboard.constants";
import {
  CategoryDeleteDialog,
  PromptCard,
  PromptDetailDialog,
  buildPromptMasonryLayout,
  estimatePromptCardHeight,
} from "./prompt-bank-workspace";
import { readStoredPromptImagesHidden, storePromptImagesHidden } from "./use-prompt-bank";

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

vi.mock("@/components/ui/dropdown-menu", () => ({
  DropdownMenu: ({ children }: { children: any }) => <div>{children}</div>,
  DropdownMenuTrigger: ({ children }: { children: any }) => <>{children}</>,
  DropdownMenuContent: ({ children }: { children: any }) => <div>{children}</div>,
  DropdownMenuItem: ({ children, onClick, disabled, className }: any) => (
    <button type="button" role="menuitem" className={className} disabled={disabled} onClick={onClick}>
      {children}
    </button>
  ),
  DropdownMenuLabel: ({ children }: { children: any }) => <div>{children}</div>,
  DropdownMenuSeparator: () => <hr />,
}));

beforeEach(() => {
  cleanup();
  vi.mocked(apiFetchBlob).mockReset();
  window.localStorage.clear();
});

function createPromptListItem(overrides: Partial<PromptListItem> = {}): PromptListItem {
  return {
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
    isArchived: false,
    hasImage: false,
    imageUpdatedAt: null,
    updatedAt: "2026-06-03T12:00:00Z",
    canEdit: true,
    canDelete: true,
    ...overrides,
  };
}

function createPromptDetail(overrides: Partial<PromptDetail> = {}): PromptDetail {
  return {
    ...createPromptListItem(),
    createdAt: "2026-06-03T12:00:00Z",
    ...overrides,
  };
}

describe("PromptCard", () => {
  it("renders a compact card when the prompt has no image and truncates noisy text", () => {
    const longDescription = "A".repeat(180);
    const longPrompt = "B".repeat(260);
    const { container } = render(
      <PromptCard
        prompt={createPromptListItem({
          title: "Prompt de teste",
          description: longDescription,
          promptText: longPrompt,
        })}
        token=""
        onOpen={() => undefined}
        onEdit={() => undefined}
        onToggleArchive={() => undefined}
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
        prompt={createPromptListItem({
          hasImage: true,
          imageUpdatedAt: "2026-06-03T12:00:00Z",
        })}
        token=""
        onOpen={() => undefined}
        onEdit={() => undefined}
        onToggleArchive={() => undefined}
        onDelete={() => undefined}
      />,
    );

    expect(container.querySelector('[class*="aspect-[4/5]"]')).not.toBeNull();
  });

  it("loads the prompt image with the active household", async () => {
    vi.mocked(apiFetchBlob).mockRejectedValueOnce(new ApiError("Arquivo não encontrado.", 404));

    render(
      <PromptCard
        prompt={createPromptListItem({
          hasImage: true,
          imageUpdatedAt: "2026-06-03T12:00:00Z",
        })}
        token="token-1"
        householdId="household-1"
        onOpen={() => undefined}
        onEdit={() => undefined}
        onToggleArchive={() => undefined}
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
        prompt={createPromptListItem({
          id: "prompt-2",
          universeId: "uni-1",
          universeName: "Universo Visual",
          universeImageUrl: "https://cdn.homepit.dev/universo-visual.png",
        })}
        token=""
        onOpen={() => undefined}
        onEdit={() => undefined}
        onToggleArchive={() => undefined}
        onDelete={() => undefined}
      />,
    );

    expect(screen.getByAltText("Universo Visual")).toHaveAttribute("src", "https://cdn.homepit.dev/universo-visual.png");
  });

  it("exposes archive actions in the card menu", async () => {
    const onToggleArchive = vi.fn();

    render(
      <PromptCard
        prompt={createPromptListItem()}
        token=""
        onOpen={() => undefined}
        onEdit={() => undefined}
        onToggleArchive={onToggleArchive}
        onDelete={() => undefined}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Ações do prompt Prompt visual" }));
    fireEvent.click(await screen.findByRole("menuitem", { name: "Arquivar" }));

    expect(onToggleArchive).toHaveBeenCalledTimes(1);
  });

  it("does not fetch the protected image when images are hidden", async () => {
    const { container } = render(
      <PromptCard
        prompt={createPromptListItem({
          hasImage: true,
          imageUpdatedAt: "2026-06-03T12:00:00Z",
        })}
        showImages={false}
        token="token-1"
        householdId="household-1"
        onOpen={() => undefined}
        onEdit={() => undefined}
        onToggleArchive={() => undefined}
        onDelete={() => undefined}
      />,
    );

    expect(container.querySelector('[class*="aspect-[4/5]"]')).toBeNull();
    await waitFor(() => {
      expect(apiFetchBlob).not.toHaveBeenCalled();
    });
    expect(screen.getAllByText("Imagem oculta").length).toBeGreaterThan(0);
  });

  it("renders the restore action for archived prompts", async () => {
    const onToggleArchive = vi.fn();

    render(
      <PromptCard
        prompt={createPromptListItem({ isArchived: true })}
        token=""
        onOpen={() => undefined}
        onEdit={() => undefined}
        onToggleArchive={onToggleArchive}
        onDelete={() => undefined}
      />,
    );

    expect(screen.getAllByText("Arquivado").length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole("button", { name: "Ações do prompt Prompt visual" }));
    fireEvent.click(await screen.findByRole("menuitem", { name: "Desarquivar" }));

    expect(onToggleArchive).toHaveBeenCalledTimes(1);
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

  it("produces shorter height estimates when images are hidden", () => {
    const prompt = createPromptListItem({
      hasImage: true,
      imageUpdatedAt: "2026-06-03T12:00:00Z",
    });

    const visibleHeight = estimatePromptCardHeight(prompt, 342, true);
    const hiddenHeight = estimatePromptCardHeight(prompt, 342, false);

    expect(hiddenHeight).toBeLessThan(visibleHeight);
  });
});

describe("PromptDetailDialog", () => {
  it("shows full prompt details and external link", () => {
    render(
      <PromptDetailDialog
        open
        prompt={createPromptDetail({
          id: "prompt-1",
          universeId: "uni-1",
          universeName: "Universo",
          universeImageUrl: "https://cdn.homepit.dev/universo.png",
          title: "Prompt detalhado",
          description: "Descrição completa",
          promptText: "Texto integral do prompt sem truncamento.",
          categories: [{ id: "cat-1", name: "Categoria" }],
          linkUrl: "https://homepit.dev",
          linkTitle: "Referência oficial",
        })}
        loading={false}
        token=""
        onOpenChange={() => undefined}
        onEdit={() => undefined}
        onToggleArchive={() => undefined}
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
        prompt={createPromptDetail({
          id: "prompt-1",
          universeId: "uni-1",
          universeName: "Universo",
          title: "Prompt detalhado",
          description: "Descrição completa",
          promptText: "Texto integral do prompt sem truncamento.",
          categories: [{ id: "cat-1", name: "Categoria" }],
        })}
        loading={false}
        token=""
        onOpenChange={() => undefined}
        onEdit={() => undefined}
        onToggleArchive={() => undefined}
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
        prompt={createPromptDetail({
          id: "prompt-1",
          universeId: "uni-1",
          universeName: "Universo",
          title: "Prompt detalhado",
          description: "Descrição completa",
          promptText: "Texto integral do prompt sem truncamento.",
          categories: [{ id: "cat-1", name: "Categoria" }],
          hasImage: true,
          imageUpdatedAt: "2026-06-03T12:00:00Z",
        })}
        loading={false}
        token="token-1"
        householdId="household-1"
        onOpenChange={() => undefined}
        onEdit={() => undefined}
        onToggleArchive={() => undefined}
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

  it("does not fetch the detail image when images are hidden", async () => {
    const { container } = render(
      <PromptDetailDialog
        open
        prompt={createPromptDetail({
          id: "prompt-1",
          universeId: "uni-1",
          universeName: "Universo",
          title: "Prompt detalhado",
          description: "Descrição completa",
          promptText: "Texto integral do prompt sem truncamento.",
          categories: [{ id: "cat-1", name: "Categoria" }],
          hasImage: true,
          imageUpdatedAt: "2026-06-03T12:00:00Z",
        })}
        showImages={false}
        loading={false}
        token="token-1"
        householdId="household-1"
        onOpenChange={() => undefined}
        onEdit={() => undefined}
        onToggleArchive={() => undefined}
        onDelete={() => undefined}
      />,
    );

    expect(container.querySelector('[class*="aspect-[4/5]"]')).toBeNull();
    await waitFor(() => {
      expect(apiFetchBlob).not.toHaveBeenCalled();
    });
    expect(screen.getAllByText("Imagem oculta").length).toBeGreaterThan(0);
  });

  it("shows the restore action for archived prompts", () => {
    const onToggleArchive = vi.fn();

    render(
      <PromptDetailDialog
        open
        prompt={createPromptDetail({
          id: "prompt-1",
          universeId: "uni-1",
          universeName: "Universo",
          title: "Prompt arquivado",
          categories: [{ id: "cat-1", name: "Categoria" }],
          isArchived: true,
        })}
        loading={false}
        token=""
        onOpenChange={() => undefined}
        onEdit={() => undefined}
        onToggleArchive={onToggleArchive}
        onDelete={() => undefined}
      />,
    );

    expect(screen.getAllByText("Arquivado").length).toBeGreaterThan(0);
    fireEvent.click(screen.getAllByRole("button", { name: "Desarquivar" })[0]);

    expect(onToggleArchive).toHaveBeenCalledTimes(1);
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

describe("prompt image preference", () => {
  it("stores only the hidden preference and removes it when images are shown again", () => {
    expect(readStoredPromptImagesHidden()).toBe(false);

    storePromptImagesHidden(true);
    expect(window.localStorage.getItem(uiStorageKeys.promptImagesHidden)).toBe("true");
    expect(readStoredPromptImagesHidden()).toBe(true);

    storePromptImagesHidden(false);
    expect(window.localStorage.getItem(uiStorageKeys.promptImagesHidden)).toBeNull();
    expect(readStoredPromptImagesHidden()).toBe(false);
  });
});

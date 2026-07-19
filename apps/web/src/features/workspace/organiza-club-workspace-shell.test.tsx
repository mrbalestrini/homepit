import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import * as api from "@/lib/api";
import { OrganizaClubWorkspaceShell } from "./organiza-club-workspace-shell";

vi.mock("sonner", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api")>();
  return {
    ...actual,
    apiFetch: vi.fn(),
  };
});

vi.mock("@/components/ui/dropdown-menu", () => ({
  DropdownMenu: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  DropdownMenuTrigger: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  DropdownMenuContent: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  DropdownMenuLabel: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  DropdownMenuSeparator: () => <hr />,
  DropdownMenuItem: ({
    children,
    onClick,
    className,
  }: {
    children: ReactNode;
    onClick?: () => void;
    className?: string;
  }) => (
    <button type="button" onClick={onClick} className={className}>
      {children}
    </button>
  ),
}));

describe("OrganizaClubWorkspaceShell header", () => {
  const fetchMock = vi.fn();
  const mockedApiFetch = vi.mocked(api.apiFetch);
  let objectUrlCounter = 0;

  beforeEach(() => {
    objectUrlCounter = 0;
    fetchMock.mockReset();
    mockedApiFetch.mockReset();
    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal(
      "URL",
      Object.assign(URL, {
        createObjectURL: vi.fn(() => `blob:shell-avatar-${++objectUrlCounter}`),
        revokeObjectURL: vi.fn(),
      }),
    );
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("shows the institutional CMS shortcut only to SuperAdmin", () => {
    const baseController = {
      activeSpaceId: "",
      activeSpace: null,
      members: [],
      theme: "light" as const,
      sidebarCollapsed: false,
      loading: false,
      error: null,
      canShareSpace: false,
      canManageSpace: false,
      editingSpace: null,
      isSpaceDialogOpen: false,
      isShareDialogOpen: false,
      setError: () => undefined,
      setSidebarCollapsed: () => undefined,
      setTheme: () => undefined,
      handleSpaceChange: () => undefined,
      handleLogout: () => undefined,
      refreshSpaces: async () => undefined,
      refreshWorkspace: async () => undefined,
      openCreateSpace: () => undefined,
      openEditSpace: () => undefined,
      openShareSpace: () => undefined,
      closeCommonModal: () => undefined,
      createSpace: async () => undefined,
      updateSpace: async () => undefined,
      deleteSpace: async () => undefined,
      shareSpace: async () => undefined,
    };
    const { rerender, unmount } = render(
      <OrganizaClubWorkspaceShell
        controller={{
          ...baseController,
          session: {
            accessToken: "token",
            refreshToken: "refresh",
            expiresAt: "2026-06-15T18:00:00Z",
            user: {
              id: "user-1",
              email: "user@organiza.club",
              displayName: "User",
              systemRole: "User",
              hasProfilePhoto: false,
            },
            spaces: [],
          },
        }}
        activeModule="projects"
        subtitle="Escopo atual"
        visibleCount={0}
        headerStats={[]}
      >
        <div>Conteúdo</div>
      </OrganizaClubWorkspaceShell>,
    );

    expect(screen.queryByRole("link", { name: "Site institucional" })).not.toBeInTheDocument();

    rerender(
      <OrganizaClubWorkspaceShell
        controller={{
          ...baseController,
          session: {
            accessToken: "token",
            refreshToken: "refresh",
            expiresAt: "2026-06-15T18:00:00Z",
            user: {
              id: "superadmin-1",
              email: "superadmin@organiza.club",
              displayName: "SuperAdmin",
              systemRole: "SuperAdmin",
              hasProfilePhoto: false,
            },
            spaces: [],
          },
        }}
        activeModule="projects"
        subtitle="Escopo atual"
        visibleCount={0}
        headerStats={[]}
      >
        <div>Conteúdo</div>
      </OrganizaClubWorkspaceShell>,
    );

    expect(screen.getByRole("link", { name: "Site institucional" })).toHaveAttribute("href", "/admin/institutional");
    unmount();
  });

  it("renders compact header stats without card containers", () => {
    render(
      <OrganizaClubWorkspaceShell
        controller={{
          session: null,
          activeSpaceId: "space-1",
          activeSpace: {
            id: "space-1",
            name: "Espaço Teste",
            role: "Owner",
          },
          members: [],
          theme: "light",
          sidebarCollapsed: false,
          loading: false,
          error: null,
          canShareSpace: false,
          canManageSpace: true,
          editingSpace: null,
          isSpaceDialogOpen: false,
          isShareDialogOpen: false,
          setError: () => undefined,
          setSidebarCollapsed: () => undefined,
          setTheme: () => undefined,
          handleSpaceChange: () => undefined,
          handleLogout: () => undefined,
          refreshSpaces: async () => undefined,
          refreshWorkspace: async () => undefined,
          openCreateSpace: () => undefined,
          openEditSpace: () => undefined,
          openShareSpace: () => undefined,
          closeCommonModal: () => undefined,
          createSpace: async () => undefined,
          updateSpace: async () => undefined,
          deleteSpace: vi.fn(async () => undefined),
          shareSpace: async () => undefined,
        }}
        activeModule="projects"
        subtitle="Escopo atual"
        visibleCount={7}
        headerStats={[
          { label: "Prompts", value: 24 },
          { label: "Categorias", value: 8 },
          { label: "Núcleos", value: 3 },
        ]}
      >
        <div>Conteúdo</div>
      </OrganizaClubWorkspaceShell>,
    );

    const header = screen.getByRole("banner");

    expect(header).toHaveTextContent("24 prompts");
    expect(header).toHaveTextContent("8 categorias");
    expect(header).toHaveTextContent("3 núcleos");
    expect(within(header).getByText("7 visíveis")).toBeInTheDocument();
    expect(screen.getAllByRole("link", { name: "Espaços" }).some((link) => link.getAttribute("href") === "/spaces")).toBe(true);
    expect(screen.getByRole("link", { name: "GSM" })).toHaveAttribute("href", "/gsm");
    expect(header.textContent).toContain("•");
    expect(header.innerHTML).not.toContain("rounded-[22px]");
  });

  it("requires typing the space name before deleting the current space", async () => {
    const deleteSpace = vi.fn(async () => undefined);

    render(
      <OrganizaClubWorkspaceShell
        controller={{
          session: null,
          activeSpaceId: "space-1",
          activeSpace: {
            id: "space-1",
            name: "Espaço Teste",
            role: "Owner",
          },
          members: [],
          theme: "light",
          sidebarCollapsed: false,
          loading: false,
          error: null,
          canShareSpace: false,
          canManageSpace: true,
          editingSpace: null,
          isSpaceDialogOpen: false,
          isShareDialogOpen: false,
          setError: () => undefined,
          setSidebarCollapsed: () => undefined,
          setTheme: () => undefined,
          handleSpaceChange: () => undefined,
          handleLogout: () => undefined,
          refreshSpaces: async () => undefined,
          refreshWorkspace: async () => undefined,
          openCreateSpace: () => undefined,
          openEditSpace: () => undefined,
          openShareSpace: () => undefined,
          closeCommonModal: () => undefined,
          createSpace: async () => undefined,
          updateSpace: async () => undefined,
          deleteSpace,
          shareSpace: async () => undefined,
        }}
        activeModule="projects"
        subtitle="Escopo atual"
        visibleCount={7}
        headerStats={[]}
      >
        <div>Conteúdo</div>
      </OrganizaClubWorkspaceShell>,
    );

    fireEvent.click(screen.getAllByRole("button", { name: "Excluir" })[0]);

    expect(await screen.findByRole("heading", { name: "Excluir espaço" })).toBeInTheDocument();
    expect(screen.getByText("Todos os núcleos, projetos, atividades e pendências vinculados ao espaço.")).toBeInTheDocument();

    const confirmButton = screen.getByRole("button", { name: "Excluir espaço" });
    expect(confirmButton).toBeDisabled();

    fireEvent.change(screen.getByLabelText(/Digite o nome do espaço/), { target: { value: "Espaço Teste" } });

    expect(confirmButton).not.toBeDisabled();
    expect(deleteSpace).not.toHaveBeenCalled();
  });

  it("renders the real member avatar inside the share dialog when the photo exists", async () => {
    fetchMock.mockImplementation(() =>
      Promise.resolve(
        new Response(new Blob([Uint8Array.from([1, 2, 3])], { type: "image/png" }), {
          status: 200,
          headers: { "Content-Type": "image/png" },
        }),
      ),
    );

    render(
      <OrganizaClubWorkspaceShell
        controller={{
          session: {
            accessToken: "token",
            refreshToken: "refresh",
            expiresAt: "2026-06-26T18:00:00Z",
            user: {
              id: "user-1",
              email: "user@organiza.club",
              displayName: "User",
              systemRole: "User",
              hasProfilePhoto: false,
            },
            spaces: [],
          },
          activeSpaceId: "space-1",
          activeSpace: {
            id: "space-1",
            name: "Espaço Teste",
            role: "Owner",
          },
          members: [
            {
              id: "member-1",
              userId: "user-2",
              displayName: "Paula Balestrini",
              email: "paula@organiza.club",
              phoneNumber: null,
              hasProfilePhoto: true,
              profilePhotoUpdatedAt: "2026-06-26T12:00:00Z",
              role: "Admin",
              isCurrentUser: false,
            },
          ],
          theme: "light",
          sidebarCollapsed: false,
          loading: false,
          error: null,
          canShareSpace: true,
          canManageSpace: true,
          editingSpace: null,
          isSpaceDialogOpen: false,
          isShareDialogOpen: true,
          setError: () => undefined,
          setSidebarCollapsed: () => undefined,
          setTheme: () => undefined,
          handleSpaceChange: () => undefined,
          handleLogout: () => undefined,
          refreshSpaces: async () => undefined,
          refreshWorkspace: async () => undefined,
          openCreateSpace: () => undefined,
          openEditSpace: () => undefined,
          openShareSpace: () => undefined,
          closeCommonModal: () => undefined,
          createSpace: async () => undefined,
          updateSpace: async () => undefined,
          deleteSpace: async () => undefined,
          shareSpace: async () => undefined,
        }}
        activeModule="projects"
        subtitle="Escopo atual"
        visibleCount={0}
        headerStats={[]}
      >
        <div>Conteúdo</div>
      </OrganizaClubWorkspaceShell>,
    );

    await waitFor(() =>
      expect(screen.getAllByAltText("Paula Balestrini").every((image) => image.getAttribute("src") === "blob:shell-avatar-1")).toBe(true),
    );
  });

  it("opens the improvement suggestion modal from the user menu and submits it", async () => {
    mockedApiFetch.mockResolvedValue({
      id: "suggestion-1",
      userId: "user-1",
      userDisplayName: "User",
      userEmail: "user@organiza.club",
      submittedAt: "2026-07-10T12:00:00Z",
      suggestionText: "Melhorar os filtros.",
      status: "NaoLido",
      priority: "Media",
      internalComment: null,
      lastReviewedAt: null,
      lastReviewedByUserId: null,
      lastReviewedByDisplayName: null,
    });

    render(
      <OrganizaClubWorkspaceShell
        controller={{
          session: {
            accessToken: "token",
            refreshToken: "refresh",
            expiresAt: "2026-06-15T18:00:00Z",
            user: {
              id: "user-1",
              email: "user@organiza.club",
              displayName: "User",
              systemRole: "User",
              hasProfilePhoto: false,
            },
            spaces: [],
          },
          activeSpaceId: "",
          activeSpace: null,
          members: [],
          theme: "light",
          sidebarCollapsed: false,
          loading: false,
          error: null,
          canShareSpace: false,
          canManageSpace: false,
          editingSpace: null,
          isSpaceDialogOpen: false,
          isShareDialogOpen: false,
          setError: () => undefined,
          setSidebarCollapsed: () => undefined,
          setTheme: () => undefined,
          handleSpaceChange: () => undefined,
          handleLogout: () => undefined,
          refreshSpaces: async () => undefined,
          refreshWorkspace: async () => undefined,
          openCreateSpace: () => undefined,
          openEditSpace: () => undefined,
          openShareSpace: () => undefined,
          closeCommonModal: () => undefined,
          createSpace: async () => undefined,
          updateSpace: async () => undefined,
          deleteSpace: async () => undefined,
          shareSpace: async () => undefined,
        }}
        activeModule="projects"
        subtitle="Escopo atual"
        visibleCount={0}
        headerStats={[]}
      >
        <div>Conteúdo</div>
      </OrganizaClubWorkspaceShell>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Menu do usuário" }));
    fireEvent.click((await screen.findAllByRole("button", { name: /Sugestão melhoria ferramenta/i }))[0]);

    expect(await screen.findByRole("heading", { name: "Sugestão de melhoria" })).toBeInTheDocument();
    const submitButton = screen.getByRole("button", { name: "Enviar sugestão" });
    expect(submitButton).toBeDisabled();

    fireEvent.change(screen.getByLabelText("Sua sugestão"), { target: { value: "Melhorar os filtros." } });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(mockedApiFetch).toHaveBeenCalledWith(
        "/api/users/me/tool-improvement-suggestions",
        expect.objectContaining({
          method: "POST",
          token: "token",
          body: JSON.stringify({ suggestionText: "Melhorar os filtros." }),
        }),
      );
    });

    await waitFor(() =>
      expect(screen.queryByRole("heading", { name: "Sugestão de melhoria" })).not.toBeInTheDocument(),
    );
  });
});

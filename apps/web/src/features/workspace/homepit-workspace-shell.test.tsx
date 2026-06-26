import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { HomePitWorkspaceShell } from "./homepit-workspace-shell";

describe("HomePitWorkspaceShell header", () => {
  const fetchMock = vi.fn();
  let objectUrlCounter = 0;

  beforeEach(() => {
    objectUrlCounter = 0;
    fetchMock.mockReset();
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
      activeHouseholdId: "",
      activeHousehold: null,
      members: [],
      theme: "cozy" as const,
      sidebarCollapsed: false,
      loading: false,
      error: null,
      canShareHousehold: false,
      canManageHousehold: false,
      editingHousehold: null,
      isHouseholdDialogOpen: false,
      isShareDialogOpen: false,
      setError: () => undefined,
      setSidebarCollapsed: () => undefined,
      setTheme: () => undefined,
      handleHouseholdChange: () => undefined,
      handleLogout: () => undefined,
      refreshHouseholds: async () => undefined,
      refreshWorkspace: async () => undefined,
      openCreateHousehold: () => undefined,
      openEditHousehold: () => undefined,
      openShareHousehold: () => undefined,
      closeCommonModal: () => undefined,
      createHousehold: async () => undefined,
      updateHousehold: async () => undefined,
      deleteHousehold: async () => undefined,
      shareHousehold: async () => undefined,
      updateProfile: async () => undefined,
    };
    const { rerender, unmount } = render(
      <HomePitWorkspaceShell
        controller={{
          ...baseController,
          session: {
            accessToken: "token",
            refreshToken: "refresh",
            expiresAt: "2026-06-15T18:00:00Z",
            user: {
              id: "user-1",
              email: "user@homepit.dev",
              displayName: "User",
              systemRole: "User",
              hasProfilePhoto: false,
            },
            households: [],
          },
        }}
        activeModule="projects"
        subtitle="Escopo atual"
        visibleCount={0}
        headerStats={[]}
      >
        <div>Conteúdo</div>
      </HomePitWorkspaceShell>,
    );

    expect(screen.queryByRole("link", { name: "Site institucional" })).not.toBeInTheDocument();

    rerender(
      <HomePitWorkspaceShell
        controller={{
          ...baseController,
          session: {
            accessToken: "token",
            refreshToken: "refresh",
            expiresAt: "2026-06-15T18:00:00Z",
            user: {
              id: "superadmin-1",
              email: "superadmin@homepit.dev",
              displayName: "SuperAdmin",
              systemRole: "SuperAdmin",
              hasProfilePhoto: false,
            },
            households: [],
          },
        }}
        activeModule="projects"
        subtitle="Escopo atual"
        visibleCount={0}
        headerStats={[]}
      >
        <div>Conteúdo</div>
      </HomePitWorkspaceShell>,
    );

    expect(screen.getByRole("link", { name: "Site institucional" })).toHaveAttribute("href", "/admin/institutional");
    unmount();
  });

  it("renders compact header stats without card containers", () => {
    render(
      <HomePitWorkspaceShell
        controller={{
          session: null,
          activeHouseholdId: "household-1",
          activeHousehold: {
            id: "household-1",
            name: "Casa Teste",
            role: "Owner",
          },
          members: [],
          theme: "cozy",
          sidebarCollapsed: false,
          loading: false,
          error: null,
          canShareHousehold: false,
          canManageHousehold: true,
          editingHousehold: null,
          isHouseholdDialogOpen: false,
          isShareDialogOpen: false,
          setError: () => undefined,
          setSidebarCollapsed: () => undefined,
          setTheme: () => undefined,
          handleHouseholdChange: () => undefined,
          handleLogout: () => undefined,
          refreshHouseholds: async () => undefined,
          refreshWorkspace: async () => undefined,
          openCreateHousehold: () => undefined,
          openEditHousehold: () => undefined,
          openShareHousehold: () => undefined,
          closeCommonModal: () => undefined,
          createHousehold: async () => undefined,
          updateHousehold: async () => undefined,
          deleteHousehold: vi.fn(async () => undefined),
          shareHousehold: async () => undefined,
          updateProfile: async () => undefined,
        }}
        activeModule="projects"
        subtitle="Escopo atual"
        visibleCount={7}
        headerStats={[
          { label: "Prompts", value: 24 },
          { label: "Categorias", value: 8 },
          { label: "Universos", value: 3 },
        ]}
      >
        <div>Conteúdo</div>
      </HomePitWorkspaceShell>,
    );

    const header = screen.getByRole("banner");

    expect(header).toHaveTextContent("24 prompts");
    expect(header).toHaveTextContent("8 categorias");
    expect(header).toHaveTextContent("3 universos");
    expect(within(header).getByText("7 visíveis")).toBeInTheDocument();
    expect(screen.getAllByRole("link", { name: "Casa" }).some((link) => link.getAttribute("href") === "/household")).toBe(true);
    expect(screen.getByRole("link", { name: "GSM" })).toHaveAttribute("href", "/gsm");
    expect(header.textContent).toContain("•");
    expect(header.innerHTML).not.toContain("rounded-[22px]");
  });

  it("requires typing the household name before deleting the current house", async () => {
    const deleteHousehold = vi.fn(async () => undefined);

    render(
      <HomePitWorkspaceShell
        controller={{
          session: null,
          activeHouseholdId: "household-1",
          activeHousehold: {
            id: "household-1",
            name: "Casa Teste",
            role: "Owner",
          },
          members: [],
          theme: "cozy",
          sidebarCollapsed: false,
          loading: false,
          error: null,
          canShareHousehold: false,
          canManageHousehold: true,
          editingHousehold: null,
          isHouseholdDialogOpen: false,
          isShareDialogOpen: false,
          setError: () => undefined,
          setSidebarCollapsed: () => undefined,
          setTheme: () => undefined,
          handleHouseholdChange: () => undefined,
          handleLogout: () => undefined,
          refreshHouseholds: async () => undefined,
          refreshWorkspace: async () => undefined,
          openCreateHousehold: () => undefined,
          openEditHousehold: () => undefined,
          openShareHousehold: () => undefined,
          closeCommonModal: () => undefined,
          createHousehold: async () => undefined,
          updateHousehold: async () => undefined,
          deleteHousehold,
          shareHousehold: async () => undefined,
          updateProfile: async () => undefined,
        }}
        activeModule="projects"
        subtitle="Escopo atual"
        visibleCount={7}
        headerStats={[]}
      >
        <div>Conteúdo</div>
      </HomePitWorkspaceShell>,
    );

    fireEvent.click(screen.getAllByRole("button", { name: "Excluir" })[0]);

    expect(await screen.findByRole("heading", { name: "Excluir casa" })).toBeInTheDocument();
    expect(screen.getByText("Todos os universos, projetos, atividades e pendências vinculados à casa.")).toBeInTheDocument();

    const confirmButton = screen.getByRole("button", { name: "Excluir casa" });
    expect(confirmButton).toBeDisabled();

    fireEvent.change(screen.getByLabelText(/Digite o nome da casa/), { target: { value: "Casa Teste" } });

    expect(confirmButton).not.toBeDisabled();
    expect(deleteHousehold).not.toHaveBeenCalled();
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
      <HomePitWorkspaceShell
        controller={{
          session: {
            accessToken: "token",
            refreshToken: "refresh",
            expiresAt: "2026-06-26T18:00:00Z",
            user: {
              id: "user-1",
              email: "user@homepit.dev",
              displayName: "User",
              systemRole: "User",
              hasProfilePhoto: false,
            },
            households: [],
          },
          activeHouseholdId: "household-1",
          activeHousehold: {
            id: "household-1",
            name: "Casa Teste",
            role: "Owner",
          },
          members: [
            {
              id: "member-1",
              userId: "user-2",
              displayName: "Paula Balestrini",
              email: "paula@homepit.dev",
              phoneNumber: null,
              hasProfilePhoto: true,
              profilePhotoUpdatedAt: "2026-06-26T12:00:00Z",
              role: "Admin",
              isCurrentUser: false,
            },
          ],
          theme: "cozy",
          sidebarCollapsed: false,
          loading: false,
          error: null,
          canShareHousehold: true,
          canManageHousehold: true,
          editingHousehold: null,
          isHouseholdDialogOpen: false,
          isShareDialogOpen: true,
          setError: () => undefined,
          setSidebarCollapsed: () => undefined,
          setTheme: () => undefined,
          handleHouseholdChange: () => undefined,
          handleLogout: () => undefined,
          refreshHouseholds: async () => undefined,
          refreshWorkspace: async () => undefined,
          openCreateHousehold: () => undefined,
          openEditHousehold: () => undefined,
          openShareHousehold: () => undefined,
          closeCommonModal: () => undefined,
          createHousehold: async () => undefined,
          updateHousehold: async () => undefined,
          deleteHousehold: async () => undefined,
          shareHousehold: async () => undefined,
          updateProfile: async () => undefined,
        }}
        activeModule="projects"
        subtitle="Escopo atual"
        visibleCount={0}
        headerStats={[]}
      >
        <div>Conteúdo</div>
      </HomePitWorkspaceShell>,
    );

    await waitFor(() =>
      expect(screen.getAllByAltText("Paula Balestrini").every((image) => image.getAttribute("src") === "blob:shell-avatar-1")).toBe(true),
    );
  });
});

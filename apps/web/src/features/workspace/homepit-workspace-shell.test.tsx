import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { HomePitWorkspaceShell } from "./homepit-workspace-shell";

describe("HomePitWorkspaceShell header", () => {
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
          deleteHousehold: async () => undefined,
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
    expect(header.textContent).toContain("•");
    expect(header.innerHTML).not.toContain("rounded-[22px]");
  });
});

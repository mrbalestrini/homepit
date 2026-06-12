import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { DeleteConfirmationDialog } from "./delete-confirmation-dialog";

describe("DeleteConfirmationDialog", () => {
  it("requires the exact target name before confirming a destructive action", async () => {
    const onConfirm = vi.fn(async () => undefined);

    render(
      <DeleteConfirmationDialog
        open
        title="Excluir universo"
        description="Confirme o impacto antes de continuar."
        impactItems={["Todos os projetos deste universo."]}
        confirmationTarget="Universo Teste"
        confirmationLabel="Digite o nome do universo para confirmar"
        confirmLabel="Excluir universo"
        onOpenChange={() => undefined}
        onConfirm={onConfirm}
      />,
    );

    const confirmButton = screen.getByRole("button", { name: "Excluir universo" });
    expect(confirmButton).toBeDisabled();

    fireEvent.change(screen.getByLabelText("Digite o nome do universo para confirmar"), {
      target: { value: "Outro nome" },
    });
    expect(confirmButton).toBeDisabled();

    fireEvent.change(screen.getByLabelText("Digite o nome do universo para confirmar"), {
      target: { value: "Universo Teste" },
    });
    expect(confirmButton).not.toBeDisabled();

    fireEvent.click(confirmButton);

    await waitFor(() => {
      expect(onConfirm).toHaveBeenCalledTimes(1);
    });
  });
});

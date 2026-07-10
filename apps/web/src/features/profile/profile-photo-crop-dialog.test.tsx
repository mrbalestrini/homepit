import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { useEffect, useRef } from "react";
import { describe, expect, it, vi } from "vitest";
import { ProfilePhotoCropDialog } from "./profile-photo-crop-dialog";

vi.mock("react-easy-crop", () => ({
  default: ({
    onCropComplete,
  }: {
    onCropComplete?: (crop: unknown, area: { x: number; y: number; width: number; height: number }) => void;
  }) => {
    const firedRef = useRef(false);

    useEffect(() => {
      if (firedRef.current) {
        return;
      }

      firedRef.current = true;
      onCropComplete?.(null, { x: 0, y: 0, width: 100, height: 100 });
    }, [onCropComplete]);

    return <div data-testid="cropper" />;
  },
}));

describe("ProfilePhotoCropDialog", () => {
  it("keeps the crop controls accessible and confirms the crop", async () => {
    const onCancel = vi.fn();
    const onConfirm = vi.fn(async () => undefined);

    render(
      <ProfilePhotoCropDialog
        draft={{ file: new File(["photo"], "photo.png", { type: "image/png" }), previewUrl: "blob:preview" }}
        onCancel={onCancel}
        onConfirm={onConfirm}
      />,
    );

    expect(screen.getByText("Ajustar foto de perfil")).toBeInTheDocument();
    expect(screen.getByText("Zoom")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Aplicar foto" })).toBeEnabled();

    fireEvent.click(screen.getByRole("button", { name: "Aplicar foto" }));

    await waitFor(() => {
      expect(onConfirm).toHaveBeenCalledWith({ x: 0, y: 0, width: 100, height: 100 });
    });
  });
});

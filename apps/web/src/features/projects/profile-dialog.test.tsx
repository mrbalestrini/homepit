import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ProfileDialog } from "./project-dashboard-workspace";

describe("ProfileDialog", () => {
  beforeEach(() => {
    let objectUrlCounter = 0;
    vi.stubGlobal(
      "URL",
      Object.assign(URL, {
        createObjectURL: vi.fn(() => `blob:preview-${++objectUrlCounter}`),
        revokeObjectURL: vi.fn(),
      }),
    );
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("shows a local preview as soon as a new photo is selected", async () => {
    render(
      <ProfileDialog
        open
        user={{
          id: "user-1",
          email: "ana@homepit.dev",
          displayName: "Ana Teste",
          phoneNumber: null,
          systemRole: "User",
          hasProfilePhoto: false,
          profilePhotoUpdatedAt: null,
        }}
        token="token"
        onOpenChange={() => undefined}
        onSave={async () => undefined}
      />,
    );

    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement | null;
    expect(fileInput).not.toBeNull();
    const file = new File([Uint8Array.from([1, 2, 3])], "avatar.png", { type: "image/png" });
    fireEvent.change(fileInput!, { target: { files: [file] } });

    await waitFor(() => expect(screen.getByText("Nova foto selecionada")).toBeInTheDocument());
    await waitFor(() => expect(screen.getByAltText("Ana Teste")).toHaveAttribute("src", "blob:preview-1"));
  });
});

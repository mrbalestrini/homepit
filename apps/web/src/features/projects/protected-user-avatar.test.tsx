import { render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ProtectedUserAvatar } from "./protected-user-avatar";

describe("ProtectedUserAvatar", () => {
  const fetchMock = vi.fn();
  let objectUrlCounter = 0;

  beforeEach(() => {
    objectUrlCounter = 0;
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal(
      "URL",
      Object.assign(URL, {
        createObjectURL: vi.fn(() => `blob:avatar-${++objectUrlCounter}`),
        revokeObjectURL: vi.fn(),
      }),
    );
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("falls back to initials when the user has no profile photo", () => {
    render(
      <ProtectedUserAvatar
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
        className="size-10"
      />,
    );

    expect(screen.getByText("AT")).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("refetches the protected image when the update timestamp changes", async () => {
    fetchMock.mockImplementation(() =>
      Promise.resolve(
        new Response(new Blob([Uint8Array.from([1, 2, 3])], { type: "image/png" }), {
          status: 200,
          headers: { "Content-Type": "image/png" },
        }),
      ),
    );

    const user = {
      id: "user-1",
      email: "ana@homepit.dev",
      displayName: "Ana Teste",
      phoneNumber: null,
      systemRole: "User" as const,
      hasProfilePhoto: true,
      profilePhotoUpdatedAt: "2026-06-01T10:00:00Z",
    };

    const { rerender } = render(<ProtectedUserAvatar user={user} token="token" className="size-10" />);

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.getByAltText("Ana Teste")).toHaveAttribute("src", "blob:avatar-1"));

    rerender(
      <ProtectedUserAvatar
        user={{ ...user, profilePhotoUpdatedAt: "2026-06-01T10:05:00Z" }}
        token="token"
        className="size-10"
      />,
    );

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(screen.getByAltText("Ana Teste")).toHaveAttribute("src", "blob:avatar-2"));
  });
});

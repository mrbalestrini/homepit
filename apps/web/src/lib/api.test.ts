import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { apiFetch, clearSession, readSession, storeSession, subscribeToSessionChanges, type AuthResponse } from "./api";

function createSession(overrides: Partial<AuthResponse> = {}): AuthResponse {
  return {
    accessToken: "access-token-1",
    refreshToken: "refresh-token-1",
    expiresAt: new Date(Date.now() + 10 * 60 * 1000).toISOString(),
    user: {
      id: "user-1",
      email: "ana@organiza.club",
      displayName: "Ana Teste",
      phoneNumber: null,
      systemRole: "User",
      hasProfilePhoto: false,
      profilePhotoUpdatedAt: null,
    },
    spaces: [
      {
        id: "house-1",
        name: "Espaço Principal",
        role: "Owner",
      },
    ],
    ...overrides,
  };
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "Content-Type": "application/json",
    },
  });
}

describe("api session handling", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    window.localStorage.clear();
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    window.localStorage.clear();
    vi.unstubAllGlobals();
  });

  it("refreshes the stored session before protected requests when the access token is near expiry", async () => {
    storeSession(
      createSession({
        expiresAt: new Date(Date.now() + 30 * 1000).toISOString(),
      }),
    );

    fetchMock.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);

      if (url.endsWith("/api/auth/refresh")) {
        return Promise.resolve(
          jsonResponse(
            createSession({
              accessToken: "access-token-2",
              refreshToken: "refresh-token-2",
              expiresAt: new Date(Date.now() + 30 * 60 * 1000).toISOString(),
            }),
          ),
        );
      }

      if (url.endsWith("/api/cores")) {
        expect(new Headers(init?.headers).get("Authorization")).toBe("Bearer access-token-2");
        expect(new Headers(init?.headers).get("X-Space-Id")).toBe("house-1");
        return Promise.resolve(jsonResponse([{ id: "core-1", name: "Espaço", projectCount: 0, canEdit: true, canDelete: true }]));
      }

      throw new Error(`Unexpected request: ${url}`);
    });

    const response = await apiFetch<Array<{ id: string; name: string }>>("/api/cores", {
      token: "access-token-1",
      spaceId: "house-1",
    });

    expect(response).toEqual([{ id: "core-1", name: "Espaço", projectCount: 0, canEdit: true, canDelete: true }]);
    expect(readSession()?.accessToken).toBe("access-token-2");
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("refreshes and retries once after a 401 response", async () => {
    storeSession(createSession());

    fetchMock.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const authorization = new Headers(init?.headers).get("Authorization");

      if (url.endsWith("/api/projects") && authorization === "Bearer access-token-1") {
        return Promise.resolve(new Response(null, { status: 401, statusText: "Unauthorized" }));
      }

      if (url.endsWith("/api/auth/refresh")) {
        return Promise.resolve(
          jsonResponse(
            createSession({
              accessToken: "access-token-2",
              refreshToken: "refresh-token-2",
            }),
          ),
        );
      }

      if (url.endsWith("/api/projects") && authorization === "Bearer access-token-2") {
        return Promise.resolve(jsonResponse([{ id: "project-1", name: "Reforma" }]));
      }

      throw new Error(`Unexpected request: ${url} (${authorization ?? "no auth"})`);
    });

    const response = await apiFetch<Array<{ id: string; name: string }>>("/api/projects", {
      token: "access-token-1",
      spaceId: "house-1",
    });

    expect(response).toEqual([{ id: "project-1", name: "Reforma" }]);
    expect(readSession()?.refreshToken).toBe("refresh-token-2");
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it("clears the stored session when refresh fails after a 401", async () => {
    storeSession(createSession());

    fetchMock.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const authorization = new Headers(init?.headers).get("Authorization");

      if (url.endsWith("/api/projects") && authorization === "Bearer access-token-1") {
        return Promise.resolve(new Response(null, { status: 401, statusText: "Unauthorized" }));
      }

      if (url.endsWith("/api/auth/refresh")) {
        return Promise.resolve(
          jsonResponse(
            {
              detail: "Sessao expirada",
            },
            403,
          ),
        );
      }

      throw new Error(`Unexpected request: ${url}`);
    });

    await expect(
      apiFetch<Array<{ id: string; name: string }>>("/api/projects", {
        token: "access-token-1",
        spaceId: "house-1",
      }),
    ).rejects.toMatchObject({
      message: "Sessão expirada. Faça login novamente.",
      status: 401,
    });

    expect(readSession()).toBeNull();
  });

  it("notifies listeners when the session changes in the same tab", () => {
    const events: Array<AuthResponse | null> = [];
    const unsubscribe = subscribeToSessionChanges((session) => {
      events.push(session);
    });

    const session = createSession();
    storeSession(session);
    clearSession();
    unsubscribe();

    expect(events).toEqual([session, null]);
  });
});

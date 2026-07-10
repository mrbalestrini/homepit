import { vi } from "vitest";
import "@testing-library/jest-dom/vitest";

vi.mock("next/navigation", () => {
  const router = {
    back: vi.fn(),
    forward: vi.fn(),
    prefetch: vi.fn(),
    push: vi.fn(),
    refresh: vi.fn(),
    replace: vi.fn(),
  };

  (globalThis as any).__nextNavigationMock = router;

  return {
    usePathname: () => "/projects",
    useRouter: () => router,
  };
});

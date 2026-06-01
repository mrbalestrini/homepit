"use client";

import { Toaster as SonnerToaster } from "sonner";

export function Toaster() {
  return (
    <SonnerToaster
      position="top-right"
      richColors
      closeButton
      toastOptions={{
        style: {
          background: "var(--surface-strong)",
          color: "var(--foreground)",
          border: "1px solid var(--border)",
          boxShadow: "var(--shadow-md)",
          borderRadius: "16px",
        },
      }}
    />
  );
}

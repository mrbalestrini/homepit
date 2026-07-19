import type { MetadataRoute } from "next";

export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "Organiza Club",
    short_name: "Organiza Club",
    description: "Seu espaço para organizar finanças, projetos, estudos, vida e muito mais.",
    start_url: "/projects",
    display: "standalone",
    background_color: "#F7F3E8",
    theme_color: "#18223A",
    icons: [
      { src: "/brand/pwa-192.png", sizes: "192x192", type: "image/png", purpose: "any" },
      { src: "/brand/pwa-512.png", sizes: "512x512", type: "image/png", purpose: "any" },
      { src: "/brand/pwa-maskable-512.png", sizes: "512x512", type: "image/png", purpose: "maskable" },
    ],
  };
}

"use client";

export const SEO_IMAGE_WIDTH = 1200;
export const SEO_IMAGE_HEIGHT = 630;
export const SEO_IMAGE_MAX_BYTES = 600 * 1024;
export const SEO_IMAGE_IDEAL_BYTES = 100 * 1024;

export type PixelCrop = {
  x: number;
  y: number;
  width: number;
  height: number;
};

const SEO_IMAGE_QUALITY_STEPS = [0.9, 0.82, 0.74, 0.66, 0.58];

export async function readImageDimensions(file: Blob) {
  const image = await loadImage(file);
  return { width: image.naturalWidth, height: image.naturalHeight };
}

export async function optimizeSeoImageFile(file: File) {
  return await renderSeoImageFile(file);
}

export async function cropSeoImageFile(file: File, crop: PixelCrop) {
  return await renderSeoImageFile(file, crop);
}

async function renderSeoImageFile(file: File, crop?: PixelCrop) {
  const image = await loadImage(file);
  const canvas = document.createElement("canvas");
  canvas.width = SEO_IMAGE_WIDTH;
  canvas.height = SEO_IMAGE_HEIGHT;

  const context = canvas.getContext("2d");
  if (!context) {
    throw new Error("Não foi possível preparar a imagem SEO.");
  }

  const sourceX = crop ? Math.max(0, crop.x) : 0;
  const sourceY = crop ? Math.max(0, crop.y) : 0;
  const sourceWidth = crop ? Math.max(1, crop.width) : image.naturalWidth;
  const sourceHeight = crop ? Math.max(1, crop.height) : image.naturalHeight;

  context.drawImage(
    image,
    sourceX,
    sourceY,
    sourceWidth,
    sourceHeight,
    0,
    0,
    SEO_IMAGE_WIDTH,
    SEO_IMAGE_HEIGHT,
  );

  let fallbackBlob: Blob | null = null;
  for (const quality of SEO_IMAGE_QUALITY_STEPS) {
    const blob = await canvasToBlob(canvas, quality);
    if (!blob) {
      continue;
    }

    if (blob.size <= SEO_IMAGE_IDEAL_BYTES) {
      return new File([blob], "seo-image.webp", { type: "image/webp" });
    }

    if (!fallbackBlob && blob.size <= SEO_IMAGE_MAX_BYTES) {
      fallbackBlob = blob;
    }
  }

  if (fallbackBlob) {
    return new File([fallbackBlob], "seo-image.webp", { type: "image/webp" });
  }

  throw new Error("A imagem SEO final ultrapassou 600 KB. Ajuste o recorte ou use uma imagem mais leve.");
}

async function loadImage(file: Blob) {
  const objectUrl = URL.createObjectURL(file);

  try {
    return await new Promise<HTMLImageElement>((resolve, reject) => {
      const image = new Image();
      image.onload = () => resolve(image);
      image.onerror = () => reject(new Error("Não foi possível ler a imagem selecionada."));
      image.src = objectUrl;
    });
  } finally {
    URL.revokeObjectURL(objectUrl);
  }
}

function canvasToBlob(canvas: HTMLCanvasElement, quality: number) {
  return new Promise<Blob | null>((resolve) => {
    canvas.toBlob((blob) => resolve(blob), "image/webp", quality);
  });
}

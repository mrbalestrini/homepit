"use client";

export const PROFILE_PHOTO_MAX_SIDE = 1200;
export const PROFILE_PHOTO_MAX_BYTES = 5 * 1024 * 1024;
export const PROFILE_PHOTO_IDEAL_BYTES = 320 * 1024;

export type PixelCrop = {
  x: number;
  y: number;
  width: number;
  height: number;
};

const PROFILE_PHOTO_QUALITY_STEPS = [0.92, 0.84, 0.76, 0.68, 0.6];

export async function cropProfilePhotoFile(file: File, crop: PixelCrop) {
  const image = await loadImage(file);
  const canvas = document.createElement("canvas");
  const cropWidth = Math.max(1, Math.round(crop.width));
  const cropHeight = Math.max(1, Math.round(crop.height));
  const outputSize = Math.max(1, Math.min(PROFILE_PHOTO_MAX_SIDE, cropWidth, cropHeight));

  canvas.width = outputSize;
  canvas.height = outputSize;

  const context = canvas.getContext("2d");
  if (!context) {
    throw new Error("Não foi possível preparar a foto de perfil.");
  }

  context.drawImage(
    image,
    Math.max(0, Math.round(crop.x)),
    Math.max(0, Math.round(crop.y)),
    cropWidth,
    cropHeight,
    0,
    0,
    outputSize,
    outputSize,
  );

  let fallbackBlob: Blob | null = null;
  for (const quality of PROFILE_PHOTO_QUALITY_STEPS) {
    const blob = await canvasToBlob(canvas, quality);
    if (!blob) {
      continue;
    }

    if (blob.size <= PROFILE_PHOTO_IDEAL_BYTES) {
      return new File([blob], "profile-photo.webp", { type: "image/webp" });
    }

    if (!fallbackBlob && blob.size <= PROFILE_PHOTO_MAX_BYTES) {
      fallbackBlob = blob;
    }
  }

  if (!fallbackBlob) {
    throw new Error("Não foi possível gerar a foto de perfil.");
  }

  return new File([fallbackBlob], "profile-photo.webp", { type: "image/webp" });
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

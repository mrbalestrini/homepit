"use client";

import { useEffect, useState } from "react";
import { ImageIcon } from "lucide-react";
import { ApiError, apiFetchBlob } from "@/lib/api";
import { cn } from "@/lib/utils";

const activityImageCache = new Map<string, string>();
const activityImageRequests = new Map<string, Promise<string>>();

export function ProtectedActivityImageFrame({
  activityId,
  title,
  hasImage,
  imageUpdatedAt,
  token,
  householdId,
  className,
  previewUrl,
  onOpenImage,
}: {
  activityId: string;
  title: string;
  hasImage: boolean;
  imageUpdatedAt?: string | null;
  token?: string;
  householdId?: string;
  className?: string;
  previewUrl?: string | null;
  onOpenImage?: (imageUrl: string) => void;
}) {
  const protectedImageUrl = useProtectedActivityImage({
    activityId,
    hasImage,
    imageUpdatedAt,
    token,
    householdId,
  });
  const imageUrl = previewUrl ?? protectedImageUrl;

  const frame = (
    <div
      className={cn(
        "aspect-[4/3] overflow-hidden border border-border/60 bg-[radial-gradient(circle_at_top_left,rgba(138,106,84,0.16),transparent_52%),linear-gradient(180deg,rgba(255,255,255,0.6),rgba(237,227,213,0.78))]",
        className,
      )}
    >
      {imageUrl ? (
        // eslint-disable-next-line @next/next/no-img-element
        <img alt={title} className="h-full w-full object-cover" src={imageUrl} />
      ) : (
        <div className="flex h-full flex-col items-center justify-center gap-3 px-4 text-center">
          <div className="grid size-14 place-items-center rounded-[20px] bg-surface-strong text-accent-foreground shadow-xs">
            <ImageIcon className="size-6" />
          </div>
          <div className="max-w-[14rem]">
            <p className="text-sm font-semibold text-foreground">{title}</p>
            <p className="mt-1 text-xs leading-5 text-muted-foreground">
              Imagem opcional para contextualizar a atividade.
            </p>
          </div>
        </div>
      )}
    </div>
  );

  if (imageUrl && onOpenImage) {
    return (
      <button
        className="block h-full w-full text-left transition focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-ring/70"
        type="button"
        aria-label={`Abrir imagem de ${title}`}
        onClick={() => onOpenImage(imageUrl)}
      >
        {frame}
      </button>
    );
  }

  return frame;
}

export function useProtectedActivityImage({
  activityId,
  hasImage,
  imageUpdatedAt,
  token,
  householdId,
}: {
  activityId: string;
  hasImage: boolean;
  imageUpdatedAt?: string | null;
  token?: string;
  householdId?: string;
}) {
  const [fetchedImage, setFetchedImage] = useState<{ cacheKey: string; imageUrl: string | null } | null>(null);
  const cacheKey = hasImage ? `${activityId}:${imageUpdatedAt ?? ""}` : null;

  useEffect(() => {
    let cancelled = false;

    if (!activityId || !hasImage || !token || !householdId || !cacheKey) {
      return () => {
        cancelled = true;
      };
    }

    const cachedImageUrl = activityImageCache.get(cacheKey);
    if (cachedImageUrl) {
      return () => {
        cancelled = true;
      };
    }

    const pendingRequest =
      activityImageRequests.get(cacheKey) ??
      apiFetchBlob(`/api/activities/${activityId}/image`, { token, householdId })
        .then((blob) => {
          const objectUrl = URL.createObjectURL(blob);
          activityImageCache.set(cacheKey, objectUrl);
          return objectUrl;
        })
        .finally(() => {
          activityImageRequests.delete(cacheKey);
        });

    activityImageRequests.set(cacheKey, pendingRequest);

    pendingRequest
      .then((nextImageUrl) => {
        if (cancelled) {
          return;
        }

        setFetchedImage({ cacheKey, imageUrl: nextImageUrl });
      })
      .catch((exception) => {
        if (cancelled) {
          return;
        }

        setFetchedImage({ cacheKey, imageUrl: null });
        if (!(exception instanceof ApiError && exception.status === 404)) {
          console.error(exception);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [activityId, cacheKey, hasImage, householdId, token]);

  if (!cacheKey) {
    return null;
  }

  return activityImageCache.get(cacheKey) ?? (fetchedImage?.cacheKey === cacheKey ? fetchedImage.imageUrl : null);
}

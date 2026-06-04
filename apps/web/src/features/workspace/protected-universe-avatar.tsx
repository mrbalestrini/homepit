"use client";

import { useEffect, useState } from "react";
import { ApiError, apiFetchBlob } from "@/lib/api";
import { AvatarCircle } from "@/features/workspace/protected-user-avatar";

const universeImageCache = new Map<string, string>();
const universeImageRequests = new Map<string, Promise<string>>();

export function ProtectedUniverseAvatar({
  universeId,
  name,
  imageUrl,
  hasImage,
  imageUpdatedAt,
  token,
  householdId,
  className,
  previewUrl,
}: {
  universeId?: string | null;
  name: string;
  imageUrl?: string | null;
  hasImage?: boolean;
  imageUpdatedAt?: string | null;
  token?: string;
  householdId?: string;
  className?: string;
  previewUrl?: string | null;
}) {
  const protectedImageUrl = useProtectedUniverseImage({
    universeId,
    imageUrl,
    hasImage,
    imageUpdatedAt,
    token,
    householdId,
  });

  return <AvatarCircle name={name} imageUrl={previewUrl ?? protectedImageUrl} className={className} />;
}

export function useProtectedUniverseImage({
  universeId,
  imageUrl,
  hasImage,
  imageUpdatedAt,
  token,
  householdId,
}: {
  universeId?: string | null;
  imageUrl?: string | null;
  hasImage?: boolean;
  imageUpdatedAt?: string | null;
  token?: string;
  householdId?: string;
}) {
  const [fetchedImage, setFetchedImage] = useState<{ cacheKey: string; imageUrl: string | null } | null>(null);
  const cacheKey = universeId && hasImage ? `${universeId}:${imageUpdatedAt ?? ""}` : null;

  useEffect(() => {
    let cancelled = false;

    if (imageUrl || !universeId || !hasImage || !token || !householdId || !cacheKey) {
      return () => {
        cancelled = true;
      };
    }

    const cachedImageUrl = universeImageCache.get(cacheKey);
    if (cachedImageUrl) {
      return () => {
        cancelled = true;
      };
    }

    const pendingRequest =
      universeImageRequests.get(cacheKey) ??
      apiFetchBlob(`/api/universes/${universeId}/image`, { token, householdId })
        .then((blob) => {
          const objectUrl = URL.createObjectURL(blob);
          universeImageCache.set(cacheKey, objectUrl);
          return objectUrl;
        })
        .finally(() => {
          universeImageRequests.delete(cacheKey);
        });

    universeImageRequests.set(cacheKey, pendingRequest);

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
  }, [cacheKey, hasImage, householdId, imageUrl, token, universeId]);

  if (imageUrl) {
    return imageUrl;
  }

  if (!cacheKey) {
    return null;
  }

  return universeImageCache.get(cacheKey) ?? (fetchedImage?.cacheKey === cacheKey ? fetchedImage.imageUrl : null);
}

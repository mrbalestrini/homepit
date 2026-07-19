"use client";

import { useEffect, useState } from "react";
import { ApiError, apiFetchBlob } from "@/lib/api";
import { AvatarCircle } from "@/features/workspace/protected-user-avatar";

const coreImageCache = new Map<string, string>();
const coreImageRequests = new Map<string, Promise<string>>();

export function ProtectedCoreAvatar({
  coreId,
  name,
  imageUrl,
  hasImage,
  imageUpdatedAt,
  token,
  spaceId,
  className,
  previewUrl,
}: {
  coreId?: string | null;
  name: string;
  imageUrl?: string | null;
  hasImage?: boolean;
  imageUpdatedAt?: string | null;
  token?: string;
  spaceId?: string;
  className?: string;
  previewUrl?: string | null;
}) {
  const protectedImageUrl = useProtectedCoreImage({
    coreId,
    imageUrl,
    hasImage,
    imageUpdatedAt,
    token,
    spaceId,
  });

  return <AvatarCircle name={name} imageUrl={previewUrl ?? protectedImageUrl} className={className} />;
}

export function useProtectedCoreImage({
  coreId,
  imageUrl,
  hasImage,
  imageUpdatedAt,
  token,
  spaceId,
}: {
  coreId?: string | null;
  imageUrl?: string | null;
  hasImage?: boolean;
  imageUpdatedAt?: string | null;
  token?: string;
  spaceId?: string;
}) {
  const [fetchedImage, setFetchedImage] = useState<{ cacheKey: string; imageUrl: string | null } | null>(null);
  const cacheKey = coreId && hasImage ? `${coreId}:${imageUpdatedAt ?? ""}` : null;

  useEffect(() => {
    let cancelled = false;

    if (imageUrl || !coreId || !hasImage || !token || !spaceId || !cacheKey) {
      return () => {
        cancelled = true;
      };
    }

    const cachedImageUrl = coreImageCache.get(cacheKey);
    if (cachedImageUrl) {
      return () => {
        cancelled = true;
      };
    }

    const pendingRequest =
      coreImageRequests.get(cacheKey) ??
      apiFetchBlob(`/api/cores/${coreId}/image`, { token, spaceId })
        .then((blob) => {
          const objectUrl = URL.createObjectURL(blob);
          coreImageCache.set(cacheKey, objectUrl);
          return objectUrl;
        })
        .finally(() => {
          coreImageRequests.delete(cacheKey);
        });

    coreImageRequests.set(cacheKey, pendingRequest);

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
  }, [cacheKey, hasImage, spaceId, imageUrl, token, coreId]);

  if (imageUrl) {
    return imageUrl;
  }

  if (!cacheKey) {
    return null;
  }

  return coreImageCache.get(cacheKey) ?? (fetchedImage?.cacheKey === cacheKey ? fetchedImage.imageUrl : null);
}

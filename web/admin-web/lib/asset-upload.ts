"use client";

import { api } from "./api";
import { generatePreviewGlb } from "./gltf-preview";
import {
  ADMIN_ASSET_PREVIEW_BUNDLE_MAX_BYTES,
  type AdminAssetDetail,
  type AdminAssetUploadUrl,
} from "./types";

// 브라우저 (특히 Windows) 가 .glb / .bundle 같은 확장자의 MIME type 을 모르고 빈 string 으로
// 반환하는 경우가 있어 파일명 확장자로 fallback 매핑한다. BE 의 ALLOWED_BUNDLE_TYPES /
// ALLOWED_PREVIEW_IMAGE_TYPES 에 맞춰 같은 키를 돌려준다.
export function resolveContentType(file: File): string {
  if (file.type) return file.type;
  const lower = file.name.toLowerCase();
  if (lower.endsWith(".glb")) return "model/gltf-binary";
  if (lower.endsWith(".zip")) return "application/zip";
  if (lower.endsWith(".bundle")) return "application/octet-stream";
  if (lower.endsWith(".jpg") || lower.endsWith(".jpeg")) return "image/jpeg";
  if (lower.endsWith(".png")) return "image/png";
  if (lower.endsWith(".webp")) return "image/webp";
  return "";
}

export function putToS3(
  url: string,
  file: File,
  contentType: string,
  onProgress: (percent: number) => void,
): Promise<void> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("PUT", url);
    xhr.setRequestHeader("Content-Type", contentType);
    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable) {
        const percent = Math.round((event.loaded / event.total) * 100);
        onProgress(percent);
      }
    };
    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        onProgress(100);
        resolve();
      } else {
        reject(new Error(`S3 PUT 실패 (HTTP ${xhr.status})`));
      }
    };
    xhr.onerror = () => reject(new Error("S3 PUT 네트워크 오류"));
    xhr.send(file);
  });
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024)
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(1)} GB`;
}

// 본체 / 프리뷰 이미지 업로드의 3-step 시퀀스 (presigned URL 발급 → S3 PUT → BE confirm).
// 검증(허용 type, byteLength)은 호출자가 사전에 해두는 책임.
export async function uploadAndConfirmAsset(opts: {
  assetId: number;
  kind: "bundle" | "preview-image";
  file: File;
  contentType: string;
  onProgress?: (percent: number) => void;
}): Promise<void> {
  const { assetId, kind, file, contentType, onProgress } = opts;
  const uploadPath =
    kind === "bundle"
      ? `/assets/${assetId}/bundle/upload-url`
      : `/assets/${assetId}/preview-image/upload-url`;
  const confirmPath =
    kind === "bundle"
      ? `/assets/${assetId}/bundle`
      : `/assets/${assetId}/preview-image`;

  const presigned = await api<AdminAssetUploadUrl>(uploadPath, {
    method: "POST",
    body: JSON.stringify({ contentType, byteLength: file.size }),
  });
  await putToS3(presigned.uploadUrl, file, contentType, onProgress ?? (() => {}));
  await api<AdminAssetDetail>(confirmPath, {
    method: "PATCH",
    body: JSON.stringify({ key: presigned.key }),
  });
}

/**
 * 본체 .glb 를 wasm 으로 저폴리화 → preview-bundle 엔드포인트 두 번(URL 발급 / confirm)
 * 호출해서 S3 에 같이 올린다. 본체가 .glb 일 때만 호출하면 된다.
 * 결과 사이즈가 BE 한도(ADMIN_ASSET_PREVIEW_BUNDLE_MAX_BYTES) 를 넘으면 자체적으로 throw.
 */
export async function generateAndUploadPreviewBundle(
  assetId: number,
  bodyFile: File,
): Promise<number> {
  const bytes = new Uint8Array(await bodyFile.arrayBuffer());
  const previewBytes = await generatePreviewGlb(bytes);

  if (previewBytes.byteLength > ADMIN_ASSET_PREVIEW_BUNDLE_MAX_BYTES) {
    throw new Error(
      `프리뷰 생성 결과가 너무 큽니다 (${formatBytes(previewBytes.byteLength)}). 본체에서 폴리곤 수를 더 줄여야 합니다.`,
    );
  }

  // TS lib 가 Uint8Array<ArrayBufferLike> 를 BlobPart 로 직접 못 받아서 ArrayBuffer 슬라이스 경유.
  const previewBuffer = previewBytes.buffer.slice(
    previewBytes.byteOffset,
    previewBytes.byteOffset + previewBytes.byteLength,
  ) as ArrayBuffer;
  const previewFile = new File([previewBuffer], "preview.glb", {
    type: "model/gltf-binary",
  });

  const presigned = await api<AdminAssetUploadUrl>(
    `/assets/${assetId}/preview-bundle/upload-url`,
    {
      method: "POST",
      body: JSON.stringify({
        contentType: previewFile.type,
        byteLength: previewFile.size,
      }),
    },
  );
  await putToS3(presigned.uploadUrl, previewFile, previewFile.type, () => {});
  await api<AdminAssetDetail>(`/assets/${assetId}/preview-bundle`, {
    method: "PATCH",
    body: JSON.stringify({ key: presigned.key }),
  });
  return previewBytes.byteLength;
}

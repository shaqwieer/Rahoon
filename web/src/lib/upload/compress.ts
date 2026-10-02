/**
 * Smart compression before upload (browser side). Photos and scanned documents taken with a phone are often 4–12 MB; they are
 * resized to a sensible long edge and re-encoded, which also drops embedded metadata (GPS). PDFs are never touched. A result is
 * used only when it is actually smaller; otherwise the original goes up unchanged. The server still validates type and size.
 */

export type UploadPurpose = "photo" | "document";

export interface PreparedFile {
  file: File;
  originalSize: number;
  /** The file was re-encoded and is smaller than the original. */
  compressed: boolean;
  /** Width × height after preparation (images only). */
  dimensions?: { width: number; height: number };
}

export class UploadRejected extends Error {}

const RULES = {
  photo: { maxEdge: 2560, quality: 0.82, maxBytes: 10 * 1024 * 1024, types: ["image/jpeg", "image/png", "image/webp"] },
  // Documents keep more pixels so small print stays legible; the API accepts JPEG/PNG/PDF only (no WebP).
  document: { maxEdge: 3200, quality: 0.86, maxBytes: 20 * 1024 * 1024, types: ["application/pdf", "image/jpeg", "image/png"] },
} as const;

/** Below this size and within the edge limit, an image is uploaded as is. */
const SMALL_ENOUGH = 350 * 1024;

export const formatBytes = (n: number) =>
  n >= 1024 * 1024 ? `${(n / 1024 / 1024).toFixed(1)} م.ب` : n >= 1024 ? `${Math.round(n / 1024)} ك.ب` : `${n} بايت`;

const isImage = (f: File) => f.type.startsWith("image/") || /\.(heic|heif|jpe?g|png|webp)$/i.test(f.name);
const isPdf = (f: File) => f.type === "application/pdf" || /\.pdf$/i.test(f.name);

let webpSupport: boolean | null = null;
function canEncodeWebp() {
  if (webpSupport !== null) return webpSupport;
  try {
    const c = document.createElement("canvas");
    c.width = c.height = 1;
    webpSupport = c.toDataURL("image/webp").startsWith("data:image/webp");
  } catch {
    webpSupport = false;
  }
  return webpSupport;
}

async function decode(file: File): Promise<ImageBitmap> {
  try {
    return await createImageBitmap(file, { imageOrientation: "from-image" });
  } catch {
    throw new UploadRejected("تعذّر قراءة الصورة. جرّب صورة بصيغة JPG أو PNG.");
  }
}

function encode(bitmap: ImageBitmap, width: number, height: number, type: string, quality: number): Promise<Blob> {
  const canvas = document.createElement("canvas");
  canvas.width = width;
  canvas.height = height;
  const ctx = canvas.getContext("2d");
  if (!ctx) return Promise.reject(new UploadRejected("المتصفح لا يدعم تجهيز الصور."));
  // JPEG has no transparency: a white background instead of black.
  if (type === "image/jpeg") {
    ctx.fillStyle = "#ffffff";
    ctx.fillRect(0, 0, width, height);
  }
  ctx.imageSmoothingQuality = "high";
  ctx.drawImage(bitmap, 0, 0, width, height);
  return new Promise((resolve, reject) => canvas.toBlob((b) => (b ? resolve(b) : reject(new UploadRejected("تعذّر ضغط الصورة."))), type, quality));
}

const rename = (name: string, type: string) => name.replace(/\.[^.]+$/, "") + (type === "image/webp" ? ".webp" : type === "image/png" ? ".png" : ".jpg");

export async function prepareUpload(file: File, purpose: UploadPurpose): Promise<PreparedFile> {
  const rule = RULES[purpose];
  if (isPdf(file)) {
    if (purpose === "photo") throw new UploadRejected("الصور فقط هنا (JPG أو PNG أو WebP). المستندات في قسم المستندات.");
    if (file.size > rule.maxBytes) throw new UploadRejected(`ملف PDF أكبر من ${formatBytes(rule.maxBytes)}. قسّمه أو صوّره بدقة أقل.`);
    return { file, originalSize: file.size, compressed: false };
  }
  if (!isImage(file)) throw new UploadRejected(purpose === "photo" ? "اختر صورة بصيغة JPG أو PNG أو WebP." : "اختر ملف PDF أو صورة JPG أو PNG.");

  const bitmap = await decode(file);
  try {
    const { width: w0, height: h0 } = bitmap;
    const scale = Math.min(1, rule.maxEdge / Math.max(w0, h0));
    const width = Math.round(w0 * scale);
    const height = Math.round(h0 * scale);
    const accepted = (rule.types as readonly string[]).includes(file.type);
    if (accepted && scale === 1 && file.size <= SMALL_ENOUGH)
      return { file, originalSize: file.size, compressed: false, dimensions: { width, height } };

    // PNG screenshots of documents compress best as JPEG; photos as WebP where the browser can encode it.
    const target = purpose === "photo" && canEncodeWebp() ? "image/webp" : "image/jpeg";
    let blob = await encode(bitmap, width, height, target, rule.quality);
    if (blob.size > rule.maxBytes) blob = await encode(bitmap, width, height, target, 0.7);
    const useNew = !accepted || blob.size < file.size;
    const out = useNew ? new File([blob], rename(file.name, target), { type: target, lastModified: file.lastModified }) : file;
    if (out.size > rule.maxBytes) throw new UploadRejected(`الملف أكبر من ${formatBytes(rule.maxBytes)} حتى بعد الضغط.`);
    return { file: out, originalSize: file.size, compressed: useNew && out.size < file.size, dimensions: { width, height } };
  } finally {
    bitmap.close();
  }
}

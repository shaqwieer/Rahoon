"use client";

import { useState } from "react";
import { Badge } from "@/components/market/ui";
import { Alert } from "@/components/ui/Alert";
import { Icon } from "@/components/ui/Icon";
import { FileDropzone, UploadList, useUploadQueue } from "@/components/ui/Uploader";
import { apiSend, isApiError } from "@/lib/api/client";
import { cn } from "@/lib/cn";
import type { Photo } from "@/lib/market/types";
import { uploadWithProgress } from "@/lib/upload/send";

/**
 * Listing photos, stored apart from private documents. Not required to send the request; the team needs a cover and at
 * least one suitable photo before publishing. Several photos go up at once, each compressed in the browser first and shown
 * with its own progress; a failed one can be retried on its own.
 */
export function PhotoManager({ reference, photos: initial, editable, onChanged }: { reference: string; photos: Photo[]; editable: boolean; onChanged?: (p: Photo[]) => void }) {
  const [photos, setPhotos] = useState(initial);
  const [error, setError] = useState<string | null>(null);

  const commit = (next: Photo[]) => {
    setPhotos(next);
    onChanged?.(next);
  };

  const uploads = useUploadQueue<Photo>({
    purpose: "photo",
    send: (file, opts) => {
      const fd = new FormData();
      fd.append("file", file);
      return uploadWithProgress<Photo>(`/market/sale-requests/${reference}/photos`, fd, opts);
    },
    onUploaded: (photo) =>
      setPhotos((cur) => {
        const next = [...cur, photo];
        onChanged?.(next);
        return next;
      }),
  });

  const order = async (ids: string[], coverId?: string) => {
    setError(null);
    try {
      const res = await apiSend<{ photos: Photo[] }>("POST", `/market/sale-requests/${reference}/photos/order`, { ids, coverId });
      commit(res.photos);
    } catch (err) {
      setError(isApiError(err) ? err.title : "تعذّر الحفظ.");
    }
  };

  const move = (i: number, dir: -1 | 1) => {
    const ids = sorted.map((p) => p.id);
    const j = i + dir;
    if (j < 0 || j >= ids.length) return;
    [ids[i], ids[j]] = [ids[j], ids[i]];
    void order(ids);
  };

  const remove = async (id: string) => {
    setError(null);
    try {
      await apiSend("POST", `/market/sale-requests/${reference}/photos/${id}/remove`);
      const rest = photos.filter((p) => p.id !== id);
      if (photos.find((p) => p.id === id)?.isCover && rest[0]) rest[0] = { ...rest[0], isCover: true };
      commit(rest);
    } catch (err) {
      setError(isApiError(err) ? err.title : "تعذّر الحذف.");
    }
  };

  const sorted = [...photos].sort((a, b) => a.sortOrder - b.sortOrder);
  return (
    <div className="flex flex-col gap-3">
      {error ? <Alert tone="err" compact>{error}</Alert> : null}
      {sorted.length === 0 && uploads.items.length === 0 ? (
        <p className="m-0 text-14 text-muted">لا توجد صور بعد. الصور تساعد المشتري، ونحتاج صورة غلاف وصورة واحدة مناسبة على الأقل قبل النشر.</p>
      ) : null}
      {sorted.length ? (
        <ul className="m-0 grid list-none grid-cols-2 gap-3 p-0 sm:grid-cols-3 lg:grid-cols-4">
          {sorted.map((p, i) => (
            <li key={p.id} className={cn("flex flex-col overflow-hidden rounded-md border bg-white", p.isCover ? "border-rust ring-1 ring-rust" : "border-line")}>
              <div className="relative aspect-[4/3] bg-subtle">
                {/* eslint-disable-next-line @next/next/no-img-element -- private, authorised image route */}
                <img src={p.url} alt={`صورة ${i + 1}`} className="size-full object-cover" loading="lazy" />
                <span className="absolute start-2 top-2 flex flex-col gap-1">
                  {p.isCover ? <Badge tone="rust" icon="star" className="bg-white/95">الغلاف</Badge> : null}
                  {p.reviewStatus === "accepted" ? <Badge tone="ok" className="bg-white/95">مقبولة</Badge> : p.reviewStatus === "rejected" ? <Badge tone="err" className="bg-white/95">غير مقبولة</Badge> : null}
                </span>
              </div>
              {p.reviewStatus === "rejected" && p.reviewNote ? <p className="m-0 px-2 pt-2 text-12 text-err">{p.reviewNote}</p> : null}
              {editable ? (
                <div className="flex items-center justify-between gap-1 border-t border-divider p-1.5">
                  <span className="flex gap-0.5">
                    <button type="button" onClick={() => move(i, -1)} disabled={i === 0} aria-label="تقديم الصورة" className="inline-flex size-9 items-center justify-center rounded-sm hover:bg-subtle disabled:opacity-30">
                      <Icon name="chevron_right" size={20} />
                    </button>
                    <button type="button" onClick={() => move(i, 1)} disabled={i === sorted.length - 1} aria-label="تأخير الصورة" className="inline-flex size-9 items-center justify-center rounded-sm hover:bg-subtle disabled:opacity-30">
                      <Icon name="chevron_left" size={20} />
                    </button>
                  </span>
                  {!p.isCover ? (
                    <button type="button" onClick={() => void order(sorted.map((x) => x.id), p.id)} className="inline-flex min-h-9 items-center gap-1 rounded-sm px-2 text-12 font-semibold hover:bg-subtle">
                      <Icon name="star" size={16} />
                      اجعلها الغلاف
                    </button>
                  ) : null}
                  <button type="button" onClick={() => void remove(p.id)} aria-label="حذف الصورة" className="inline-flex size-9 items-center justify-center rounded-sm text-err hover:bg-err-bg">
                    <Icon name="delete" size={18} />
                  </button>
                </div>
              ) : null}
            </li>
          ))}
        </ul>
      ) : null}
      {editable ? (
        <>
          <UploadList items={uploads.items} onRetry={uploads.retry} onDismiss={uploads.dismiss} />
          <FileDropzone accept="image/jpeg,image/png,image/webp,image/heic,image/heif" icon="add_photo_alternate" buttonLabel="اختيار صور" onFiles={uploads.add}
            title="اسحب صور العقار هنا أو اخترها من جهازك"
            hint="يمكنك اختيار عدة صور معًا. نضغط الصور الكبيرة تلقائيًا قبل الرفع ونزيل منها بيانات الموقع. JPG أو PNG أو WebP." />
        </>
      ) : null}
    </div>
  );
}

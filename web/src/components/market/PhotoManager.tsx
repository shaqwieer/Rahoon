"use client";

import { useRef, useState } from "react";
import { Badge } from "@/components/market/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { apiSend, apiUpload, isApiError } from "@/lib/api/client";
import { cn } from "@/lib/cn";
import type { Photo } from "@/lib/market/types";

interface Pending {
  id: string;
  name: string;
  file: File;
  state: "uploading" | "failed";
  error?: string;
}

/**
 * Listing photos, stored apart from private documents. Not required to send the request; the team needs a cover and at
 * least one suitable photo before publishing. Each failed upload can be retried on its own.
 */
export function PhotoManager({ reference, photos: initial, editable, onChanged }: { reference: string; photos: Photo[]; editable: boolean; onChanged?: (p: Photo[]) => void }) {
  const [photos, setPhotos] = useState(initial);
  const [pending, setPending] = useState<Pending[]>([]);
  const [error, setError] = useState<string | null>(null);
  const input = useRef<HTMLInputElement>(null);

  const commit = (next: Photo[]) => {
    setPhotos(next);
    onChanged?.(next);
  };

  const upload = async (p: Pending) => {
    setPending((xs) => xs.map((x) => (x.id === p.id ? { ...x, state: "uploading", error: undefined } : x)));
    const fd = new FormData();
    fd.append("file", p.file);
    try {
      const photo = await apiUpload<Photo>(`/market/sale-requests/${reference}/photos`, fd);
      setPending((xs) => xs.filter((x) => x.id !== p.id));
      setPhotos((cur) => {
        const next = [...cur, photo];
        onChanged?.(next);
        return next;
      });
    } catch (err) {
      setPending((xs) => xs.map((x) => (x.id === p.id ? { ...x, state: "failed", error: isApiError(err) && err.title ? err.title : "تعذّر الرفع. تحقق من الاتصال." } : x)));
    }
  };

  const pick = (files: FileList | null) => {
    if (!files) return;
    const items: Pending[] = Array.from(files).map((f) => ({ id: `${f.name}-${f.size}-${Math.random()}`, name: f.name, file: f, state: "uploading" }));
    setPending((xs) => [...xs, ...items]);
    items.forEach((i) => void upload(i));
    if (input.current) input.current.value = "";
  };

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
    const ids = photos.map((p) => p.id);
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
      {sorted.length === 0 && pending.length === 0 ? (
        <p className="m-0 text-14 text-muted">لا توجد صور بعد. الصور تساعد المشتري، ونحتاج صورة غلاف وصورة واحدة مناسبة على الأقل قبل النشر.</p>
      ) : null}
      <ul className="m-0 grid list-none grid-cols-2 gap-3 p-0 sm:grid-cols-3 lg:grid-cols-4">
        {sorted.map((p, i) => (
          <li key={p.id} className={cn("flex flex-col overflow-hidden rounded-md border bg-white", p.isCover ? "border-rust" : "border-line")}>
            <div className="relative aspect-[4/3] bg-subtle">
              {/* eslint-disable-next-line @next/next/no-img-element -- private, authorised image route */}
              <img src={p.url} alt={`صورة ${i + 1}`} className="size-full object-cover" loading="lazy" />
              <span className="absolute start-2 top-2 flex flex-col gap-1">
                {p.isCover ? <Badge tone="rust" icon="star">الغلاف</Badge> : null}
                {p.reviewStatus === "accepted" ? <Badge tone="ok">مقبولة</Badge> : p.reviewStatus === "rejected" ? <Badge tone="err">غير مقبولة</Badge> : null}
              </span>
            </div>
            {p.reviewStatus === "rejected" && p.reviewNote ? <p className="m-0 px-2 pt-2 text-12 text-err">{p.reviewNote}</p> : null}
            {editable ? (
              <div className="flex items-center justify-between gap-1 p-1.5">
                <span className="flex gap-0.5">
                  <button type="button" onClick={() => move(i, -1)} disabled={i === 0} aria-label="تقديم الصورة" className="inline-flex size-9 items-center justify-center rounded-sm hover:bg-subtle disabled:opacity-30">
                    <Icon name="chevron_right" size={20} mirror />
                  </button>
                  <button type="button" onClick={() => move(i, 1)} disabled={i === sorted.length - 1} aria-label="تأخير الصورة" className="inline-flex size-9 items-center justify-center rounded-sm hover:bg-subtle disabled:opacity-30">
                    <Icon name="chevron_left" size={20} mirror />
                  </button>
                </span>
                {!p.isCover ? (
                  <button type="button" onClick={() => void order(sorted.map((x) => x.id), p.id)} className="inline-flex min-h-9 items-center gap-1 rounded-sm px-2 text-12 hover:bg-subtle">
                    <Icon name="star" size={16} />
                    غلاف
                  </button>
                ) : null}
                <button type="button" onClick={() => void remove(p.id)} aria-label="حذف الصورة" className="inline-flex size-9 items-center justify-center rounded-sm text-err hover:bg-err-bg">
                  <Icon name="delete" size={18} />
                </button>
              </div>
            ) : null}
          </li>
        ))}
        {pending.map((p) => (
          <li key={p.id} className="flex aspect-[4/3] flex-col items-center justify-center gap-2 rounded-md border border-dashed border-line-strong bg-white p-2 text-center">
            {p.state === "uploading" ? (
              <>
                <Icon name="progress_activity" size={24} className="animate-rh-spin text-muted" />
                <span className="line-clamp-1 text-12 text-muted">{p.name}</span>
              </>
            ) : (
              <>
                <Icon name="error" size={22} className="text-err" />
                <span className="text-12 text-err">{p.error}</span>
                <span className="flex gap-1">
                  <Button size="sm" variant="secondary" onClick={() => void upload(p)}>إعادة المحاولة</Button>
                  <Button size="sm" variant="text" onClick={() => setPending((xs) => xs.filter((x) => x.id !== p.id))}>إلغاء</Button>
                </span>
              </>
            )}
          </li>
        ))}
      </ul>
      {editable ? (
        <div>
          <input ref={input} type="file" accept="image/jpeg,image/png,image/webp" multiple className="sr-only" id={`photos-${reference}`} onChange={(e) => pick(e.target.files)} />
          <label htmlFor={`photos-${reference}`} className="inline-flex min-h-11 cursor-pointer items-center gap-2 rounded-sm border border-line-strong bg-white px-4 text-15 font-semibold hover:bg-subtle">
            <Icon name="add_photo_alternate" size={20} />
            إضافة صور
          </label>
          <span className="ms-3 text-13 text-muted">JPG أو PNG أو WebP، حتى 10 م.ب للصورة.</span>
        </div>
      ) : null}
    </div>
  );
}

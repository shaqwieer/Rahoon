"use client";

import { useState } from "react";
import { Icon } from "@/components/ui/Icon";
import { cn } from "@/lib/cn";

/** Large photo with thumbnails; arrow keys move between photos. Listing photos only — never documents. */
export function Gallery({ photos, title }: { photos: { id: string; url: string }[]; title: string }) {
  const [i, setI] = useState(0);
  if (photos.length === 0)
    return (
      <div className="flex aspect-[16/10] items-center justify-center rounded-lg bg-subtle text-muted">
        <Icon name="image_not_supported" size={48} />
      </div>
    );
  const go = (d: number) => setI((x) => (x + d + photos.length) % photos.length);
  return (
    <div className="flex flex-col gap-2" role="region" aria-roledescription="معرض الصور" aria-label={`صور ${title}`}
      onKeyDown={(e) => { if (e.key === "ArrowLeft") go(1); if (e.key === "ArrowRight") go(-1); }}>
      <div className="relative aspect-[16/10] overflow-hidden rounded-lg bg-subtle">
        {/* eslint-disable-next-line @next/next/no-img-element -- API-served listing photo */}
        <img src={photos[i].url} alt={`${title} — صورة ${i + 1} من ${photos.length}`} className="size-full object-cover" />
        {photos.length > 1 ? (
          <>
            <button type="button" onClick={() => go(-1)} aria-label="الصورة السابقة" className="absolute start-3 top-1/2 inline-flex size-11 -translate-y-1/2 items-center justify-center rounded-full bg-white/90 shadow-2">
              <Icon name="chevron_right" size={24} mirror />
            </button>
            <button type="button" onClick={() => go(1)} aria-label="الصورة التالية" className="absolute end-3 top-1/2 inline-flex size-11 -translate-y-1/2 items-center justify-center rounded-full bg-white/90 shadow-2">
              <Icon name="chevron_left" size={24} mirror />
            </button>
            <span className="absolute end-3 bottom-3 rounded-pill bg-ink/70 px-2.5 py-0.5 text-12 text-white" aria-live="polite">
              {i + 1} / {photos.length}
            </span>
          </>
        ) : null}
      </div>
      {photos.length > 1 ? (
        <div className="flex gap-2 overflow-x-auto pb-1">
          {photos.map((p, n) => (
            <button key={p.id} type="button" onClick={() => setI(n)} aria-label={`عرض الصورة ${n + 1}`} aria-current={n === i}
              className={cn("h-16 w-24 flex-none overflow-hidden rounded-sm border-2", n === i ? "border-rust" : "border-transparent opacity-80 hover:opacity-100")}>
              {/* eslint-disable-next-line @next/next/no-img-element -- thumbnail */}
              <img src={p.url} alt="" className="size-full object-cover" loading="lazy" />
            </button>
          ))}
        </div>
      ) : null}
    </div>
  );
}

"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState, useTransition, type FormEvent } from "react";
import { Chips } from "@/components/market/DynamicField";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { label } from "@/lib/market/catalog";
import { sar } from "@/lib/market/format";
import { toLatinDigits } from "@/lib/market/numbers";
import type { Catalog } from "@/lib/market/types";

const KEYS = ["city", "types", "maxNow", "maxInstallment", "freq", "maxTotal", "readiness", "minArea", "bedrooms", "track", "sort"] as const;
type Key = (typeof KEYS)[number];
type State = Record<Key, string>;

const empty: State = { city: "", types: "", maxNow: "", maxInstallment: "", freq: "monthly", maxTotal: "", readiness: "", minArea: "", bedrooms: "", track: "", sort: "" };
const inputCls = "min-h-11 w-full rounded-sm border border-line-strong bg-white px-3 text-15";
const amount = (v: string) => toLatinDigits(v).replace(/[^\d.]/g, "");

/**
 * Search built around «what can I pay now, and what can I commit to later». The state lives in the URL (shareable); the
 * server filters and paginates. Unknown amounts never pass a budget filter. Nothing is widened silently.
 */
export function SearchFilters({ catalog, total, excluded }: { catalog: Catalog; total: number; excluded: number }) {
  const router = useRouter();
  const pathname = usePathname();
  const sp = useSearchParams();
  const [pending, start] = useTransition();
  const fromUrl = (): State => Object.fromEntries(KEYS.map((k) => [k, sp.get(k) ?? empty[k]])) as State;
  const [s, setS] = useState<State>(fromUrl);
  const [more, setMore] = useState(() => Boolean(sp.get("maxTotal") || sp.get("readiness") || sp.get("minArea") || sp.get("bedrooms") || sp.get("track")));

  // eslint-disable-next-line react-hooks/set-state-in-effect -- keep the form in step with back/forward navigation
  useEffect(() => setS(fromUrl()), [sp]); // eslint-disable-line react-hooks/exhaustive-deps

  const push = (next: State) => {
    const q = new URLSearchParams();
    for (const k of KEYS) {
      const v = next[k];
      if (!v) continue;
      if (k === "freq" && (!next.maxInstallment || v === "monthly")) continue;
      q.set(k, v);
    }
    start(() => router.push(q.toString() ? `${pathname}?${q}` : pathname, { scroll: false }));
  };

  const submit = (e: FormEvent) => {
    e.preventDefault();
    push(s);
  };
  const set = (k: Key, v: string) => setS((x) => ({ ...x, [k]: v }));
  const types = s.types.split(",").filter(Boolean);

  const active: { key: Key; text: string }[] = [];
  const cur = fromUrl();
  if (cur.city) active.push({ key: "city", text: catalog.cities.find((c) => c.key === cur.city)?.label ?? cur.city });
  if (cur.types) active.push({ key: "types", text: cur.types.split(",").map((t) => label(catalog.propertyTypes, t)).join("، ") });
  if (cur.maxNow) active.push({ key: "maxNow", text: `الآن حتى ${sar(Number(cur.maxNow))} ر.س` });
  if (cur.maxInstallment) active.push({ key: "maxInstallment", text: `قسط حتى ${sar(Number(cur.maxInstallment))} ر.س ${label(catalog.frequencies, cur.freq || "monthly")}` });
  if (cur.maxTotal) active.push({ key: "maxTotal", text: `الإجمالي حتى ${sar(Number(cur.maxTotal))} ر.س` });
  if (cur.readiness) active.push({ key: "readiness", text: cur.readiness === "ready" ? "جاهز" : "تحت الإنشاء" });
  if (cur.minArea) active.push({ key: "minArea", text: `من ${cur.minArea} م²` });
  if (cur.bedrooms) active.push({ key: "bedrooms", text: `${cur.bedrooms}+ غرف` });
  if (cur.track) active.push({ key: "track", text: cur.track === "developer" ? "التزام لدى مطور" : cur.track === "financier" ? "عقار مموّل" : "مطور وجهة تمويل" });

  return (
    <form onSubmit={submit} role="search" aria-label="البحث عن فرصة" className="flex flex-col gap-4 rounded-lg border border-line bg-white p-4 shadow-1 md:p-5">
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <div className="flex flex-col gap-1">
          <label htmlFor="sf-maxNow" className="text-14 font-semibold">ما تستطيع دفعه الآن</label>
          <div className="relative">
            <input id="sf-maxNow" inputMode="numeric" dir="ltr" className={`${inputCls} ps-12 text-end`} value={s.maxNow} onChange={(e) => set("maxNow", amount(e.target.value))} placeholder="مثال 400000" />
            <span className="pointer-events-none absolute inset-y-0 left-3 flex items-center text-13 text-muted">ر.س</span>
          </div>
        </div>
        <div className="flex flex-col gap-1">
          <label htmlFor="sf-inst" className="text-14 font-semibold">القسط المريح لك</label>
          <div className="flex gap-2">
            <div className="relative min-w-0 flex-1">
              <input id="sf-inst" inputMode="numeric" dir="ltr" className={`${inputCls} ps-12 text-end`} value={s.maxInstallment} onChange={(e) => set("maxInstallment", amount(e.target.value))} />
              <span className="pointer-events-none absolute inset-y-0 left-3 flex items-center text-13 text-muted">ر.س</span>
            </div>
            <select aria-label="دورية القسط" className={`${inputCls} w-[7.5rem] flex-none`} value={s.freq} onChange={(e) => set("freq", e.target.value)}>
              {catalog.frequencies.map((f) => <option key={f.value} value={f.value}>{f.label}</option>)}
            </select>
          </div>
        </div>
        <div className="flex flex-col gap-1">
          <label htmlFor="sf-city" className="text-14 font-semibold">المدينة</label>
          <select id="sf-city" className={inputCls} value={s.city} onChange={(e) => set("city", e.target.value)}>
            <option value="">كل المدن</option>
            {catalog.cities.map((c) => <option key={c.key} value={c.key}>{c.label}</option>)}
          </select>
        </div>
        <div className="flex flex-col gap-1">
          <label htmlFor="sf-sort" className="text-14 font-semibold">الترتيب</label>
          <select id="sf-sort" className={inputCls} value={s.sort} onChange={(e) => set("sort", e.target.value)}>
            <option value="">{s.maxNow ? "الأنسب لقدرتك" : "الأحدث"}</option>
            <option value="fit">الأنسب لقدرتك</option>
            <option value="now">الأقل احتياجًا للدفع الآن</option>
            <option value="price">سعر الشراء / الإجمالي</option>
            <option value="newest">الأحدث</option>
          </select>
        </div>
      </div>
      <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
        <legend className="mb-1 text-14 font-semibold">نوع العقار</legend>
        <Chips name="types" multiple options={catalog.propertyTypes} value={types}
          onChange={(v) => set("types", (types.includes(v) ? types.filter((t) => t !== v) : [...types, v]).join(","))} />
      </fieldset>
      <button type="button" aria-expanded={more} onClick={() => setMore((m) => !m)} className="inline-flex w-fit items-center gap-1 text-14 font-semibold text-rust">
        <Icon name={more ? "expand_less" : "tune"} size={18} />
        {more ? "إخفاء الفلاتر الإضافية" : "المزيد من الفلاتر"}
      </button>
      {more ? (
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
          <div className="flex flex-col gap-1">
            <label htmlFor="sf-total" className="text-14 font-semibold">الحد الأقصى للإجمالي</label>
            <input id="sf-total" inputMode="numeric" dir="ltr" className={`${inputCls} text-end`} value={s.maxTotal} onChange={(e) => set("maxTotal", amount(e.target.value))} />
          </div>
          <div className="flex flex-col gap-1">
            <label htmlFor="sf-ready" className="text-14 font-semibold">الحالة</label>
            <select id="sf-ready" className={inputCls} value={s.readiness} onChange={(e) => set("readiness", e.target.value)}>
              <option value="">الكل</option>
              <option value="ready">جاهز</option>
              <option value="under_construction">تحت الإنشاء</option>
            </select>
          </div>
          <div className="flex flex-col gap-1">
            <label htmlFor="sf-area" className="text-14 font-semibold">أقل مساحة (م²)</label>
            <input id="sf-area" inputMode="numeric" dir="ltr" className={`${inputCls} text-end`} value={s.minArea} onChange={(e) => set("minArea", amount(e.target.value))} />
          </div>
          <div className="flex flex-col gap-1">
            <label htmlFor="sf-beds" className="text-14 font-semibold">غرف النوم (على الأقل)</label>
            <select id="sf-beds" className={inputCls} value={s.bedrooms} onChange={(e) => set("bedrooms", e.target.value)}>
              <option value="">أي عدد</option>
              {[1, 2, 3, 4, 5].map((n) => <option key={n} value={n}>{n}+</option>)}
            </select>
          </div>
          <div className="flex flex-col gap-1">
            <label htmlFor="sf-track" className="text-14 font-semibold">نوع الالتزام</label>
            <select id="sf-track" className={inputCls} value={s.track} onChange={(e) => set("track", e.target.value)}>
              <option value="">الكل</option>
              <option value="developer">التزام لدى مطور</option>
              <option value="financier">عقار مموّل</option>
              <option value="mixed">مطور وجهة تمويل</option>
            </select>
          </div>
        </div>
      ) : null}
      <div className="flex flex-wrap items-center gap-3">
        <Button type="submit" loading={pending}>
          <Icon name="search" size={18} />
          عرض النتائج
        </Button>
        <span role="status" aria-live="polite" className="text-14 text-charcoal">
          <strong>{total}</strong> {total === 1 ? "فرصة" : "فرص"}
          {excluded > 0 ? <span className="text-muted"> · استبعدنا {excluded} فرص أرقامها غير مكتملة للمقارنة</span> : null}
        </span>
      </div>
      {active.length ? (
        <div className="flex flex-wrap items-center gap-2 border-t border-divider pt-3">
          {active.map((a) => (
            <button key={a.key} type="button" onClick={() => push({ ...cur, [a.key]: "", ...(a.key === "maxInstallment" ? { freq: "monthly" } : {}) })}
              className="inline-flex min-h-9 items-center gap-1 rounded-pill border border-rust-200 bg-rust-50 px-3 text-13 text-rust-700" aria-label={`إزالة: ${a.text}`}>
              {a.text}
              <Icon name="close" size={16} />
            </button>
          ))}
          <button type="button" onClick={() => push(empty)} className="text-13 font-semibold text-muted underline">مسح الفلاتر</button>
        </div>
      ) : null}
    </form>
  );
}

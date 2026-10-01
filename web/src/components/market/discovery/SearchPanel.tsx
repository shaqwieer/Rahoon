"use client";

import Link from "next/link";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { Chips } from "@/components/market/DynamicField";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { toLatinDigits } from "@/lib/market/numbers";
import { ADVANCED, chips, EMPTY, filtersKey, SORTS, type SearchKey, type SearchState } from "@/lib/market/search";
import type { Catalog } from "@/lib/market/types";

const inputCls = "min-h-11 w-full rounded-sm border border-line-strong bg-white px-3 text-15";
const amount = (v: string) => toLatinDigits(v).replace(/[^\d.]/g, "");
const DEBOUNCE_MS = 600;

function Money({ id, label, value, onChange }: { id: string; label: string; value: string; onChange: (v: string) => void }) {
  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="text-14 font-semibold">{label}</label>
      <div className="relative">
        <input id={id} inputMode="numeric" dir="ltr" className={`${inputCls} ps-12 text-end`} value={value} onChange={(e) => onChange(amount(e.target.value))} />
        <span className="pointer-events-none absolute inset-y-0 left-3 flex items-center text-13 text-muted">ر.س</span>
      </div>
    </div>
  );
}

/**
 * Filters for «what can I pay now, and what can I commit to later». The URL is the state: edits are applied after a short pause
 * (or at once with «عرض النتائج»), always back to page 1. Primary filters stay visible; the rest open in a panel that suits
 * phones. Active filters are removable chips; nothing is widened silently.
 */
export function SearchPanel({ value, onApply, catalog, total, excluded, loading, signedIn }: {
  value: SearchState;
  onApply: (next: SearchState) => void;
  catalog: Catalog;
  total: number | null;
  excluded: number;
  loading: boolean;
  signedIn: boolean;
}) {
  const [draft, setDraft] = useState<SearchState>(value);
  const [more, setMore] = useState(() => ADVANCED.some((k) => value[k]));
  const applied = useRef(filtersKey(value));
  const onApplyRef = useRef(onApply);
  useEffect(() => {
    onApplyRef.current = onApply;
  }, [onApply]);

  // Back/forward or a chip removal changed the URL: the form follows it.
  useEffect(() => {
    applied.current = filtersKey(value);
    // eslint-disable-next-line react-hooks/set-state-in-effect -- keep the form in step with the URL (the source of truth)
    setDraft(value);
  }, [value]);

  // Debounced apply of edits (typing an amount doesn't fire a request per key).
  useEffect(() => {
    const key = filtersKey(draft);
    if (key === applied.current && draft.sort === value.sort) return;
    const t = window.setTimeout(() => {
      applied.current = key;
      onApplyRef.current({ ...draft, page: "" });
    }, DEBOUNCE_MS);
    return () => window.clearTimeout(t);
  }, [draft, value.sort]);

  const set = (k: SearchKey, v: string) => setDraft((x) => ({ ...x, [k]: v }));
  const toggleCsv = (k: SearchKey, v: string) => {
    const cur = draft[k].split(",").filter(Boolean);
    set(k, (cur.includes(v) ? cur.filter((x) => x !== v) : [...cur, v]).join(","));
  };
  const submit = (e: FormEvent) => {
    e.preventDefault();
    applied.current = filtersKey(draft);
    onApply({ ...draft, page: "" });
  };
  const types = draft.types.split(",").filter(Boolean);
  const features = (catalog.fields.find((f) => f.key === "features")?.options ?? [])
    .filter((o) => !o.propertyTypes || types.length === 0 || o.propertyTypes.some((t) => types.includes(t)));
  const active = chips(value, catalog);
  const cityOptions = catalog.cities;
  const singleCity = draft.city.includes(",") ? "" : draft.city;

  return (
    <form onSubmit={submit} role="search" aria-label="البحث عن فرصة" className="flex flex-col gap-4 rounded-lg border border-line bg-white p-4 shadow-1 md:p-5">
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Money id="sf-maxNow" label="ما تستطيع دفعه الآن" value={draft.maxNow} onChange={(v) => set("maxNow", v)} />
        <div className="flex flex-col gap-1">
          <label htmlFor="sf-inst" className="text-14 font-semibold">القسط المريح لك</label>
          <div className="flex gap-2">
            <div className="relative min-w-0 flex-1">
              <input id="sf-inst" inputMode="numeric" dir="ltr" className={`${inputCls} ps-12 text-end`} value={draft.maxInstallment} onChange={(e) => set("maxInstallment", amount(e.target.value))} />
              <span className="pointer-events-none absolute inset-y-0 left-3 flex items-center text-13 text-muted">ر.س</span>
            </div>
            <select aria-label="دورية القسط" className={`${inputCls} w-[7.5rem] flex-none`} value={draft.freq || "monthly"} onChange={(e) => set("freq", e.target.value)}>
              {catalog.frequencies.map((f) => <option key={f.value} value={f.value}>{f.label}</option>)}
            </select>
          </div>
        </div>
        <div className="flex flex-col gap-1">
          <label htmlFor="sf-city" className="text-14 font-semibold">المدينة</label>
          <select id="sf-city" className={inputCls} value={singleCity} onChange={(e) => set("city", e.target.value)}>
            <option value="">{draft.city.includes(",") ? "عدة مدن" : "كل المدن"}</option>
            {cityOptions.map((c) => <option key={c.key} value={c.key}>{c.label}</option>)}
          </select>
        </div>
        <div className="flex flex-col gap-1">
          <label htmlFor="sf-sort" className="text-14 font-semibold">الترتيب</label>
          <select id="sf-sort" className={inputCls} value={draft.sort} onChange={(e) => set("sort", e.target.value)}>
            <option value="">{draft.maxNow || draft.maxInstallment || draft.maxTotal || draft.match ? "الأنسب (افتراضي)" : "الأحدث (افتراضي)"}</option>
            {SORTS.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
          </select>
        </div>
      </div>

      <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
        <legend className="mb-1 text-14 font-semibold">نوع العقار</legend>
        <Chips name="types" multiple options={catalog.propertyTypes} value={types} onChange={(v) => toggleCsv("types", v)} />
      </fieldset>

      <div className="flex flex-wrap items-center gap-x-5 gap-y-2">
        <label className="inline-flex min-h-10 items-center gap-2 text-14">
          <select aria-label="غرف النوم" className="min-h-10 rounded-sm border border-line-strong bg-white px-2 text-14" value={draft.bedrooms} onChange={(e) => set("bedrooms", e.target.value)}>
            <option value="">غرف النوم: أي عدد</option>
            {[1, 2, 3, 4, 5].map((n) => <option key={n} value={n}>{n}+ غرف</option>)}
          </select>
        </label>
        <label className="inline-flex min-h-10 items-center gap-2 text-14">
          <select aria-label="حالة العقار" className="min-h-10 rounded-sm border border-line-strong bg-white px-2 text-14" value={draft.readiness} onChange={(e) => set("readiness", e.target.value)}>
            <option value="">جاهز وتحت الإنشاء</option>
            <option value="ready">جاهز</option>
            <option value="under_construction">تحت الإنشاء</option>
          </select>
        </label>
        {signedIn ? (
          <label className="inline-flex min-h-10 cursor-pointer items-center gap-2 text-14">
            <input type="checkbox" className="size-5 accent-[var(--color-rust)]" checked={draft.match === "me"} onChange={(e) => set("match", e.target.checked ? "me" : "")} />
            طابق مع قدرتي الشرائية المسجلة
          </label>
        ) : (
          <Link href={`/signin?next=${encodeURIComponent("/opportunities?match=me")}`} className="text-14">ادخل لمطابقة الفرص مع قدرتك الشرائية</Link>
        )}
      </div>

      <button type="button" aria-expanded={more} aria-controls="sf-more" onClick={() => setMore((m) => !m)} className="inline-flex w-fit items-center gap-1 text-14 font-semibold text-rust">
        <Icon name={more ? "expand_less" : "tune"} size={18} />
        {more ? "إخفاء الفلاتر الإضافية" : "المزيد من الفلاتر"}
      </button>
      {more ? (
        <div id="sf-more" className="grid gap-3 border-t border-divider pt-4 sm:grid-cols-2 xl:grid-cols-4">
          <div className="flex flex-col gap-1">
            <label htmlFor="sf-district" className="text-14 font-semibold">الحي</label>
            <input id="sf-district" className={inputCls} value={draft.district} onChange={(e) => set("district", e.target.value)} placeholder="مثال: النرجس" />
          </div>
          <div className="flex flex-col gap-1">
            <label htmlFor="sf-project" className="text-14 font-semibold">المشروع أو المطور</label>
            <input id="sf-project" className={inputCls} value={draft.project} onChange={(e) => set("project", e.target.value)} />
          </div>
          <Money id="sf-total" label="الحد الأقصى للإجمالي" value={draft.maxTotal} onChange={(v) => set("maxTotal", v)} />
          <div className="flex flex-col gap-1">
            <label htmlFor="sf-term" className="text-14 font-semibold">أقصى مدة متبقية للأقساط (شهر)</label>
            <input id="sf-term" inputMode="numeric" dir="ltr" className={`${inputCls} text-end`} value={draft.maxTerm} onChange={(e) => set("maxTerm", amount(e.target.value))} />
          </div>
          <div className="flex flex-col gap-1">
            <span className="text-14 font-semibold">المساحة (م²)</span>
            <div className="flex gap-2">
              <input aria-label="أقل مساحة" inputMode="numeric" dir="ltr" placeholder="من" className={`${inputCls} text-end`} value={draft.minArea} onChange={(e) => set("minArea", amount(e.target.value))} />
              <input aria-label="أكبر مساحة" inputMode="numeric" dir="ltr" placeholder="إلى" className={`${inputCls} text-end`} value={draft.maxArea} onChange={(e) => set("maxArea", amount(e.target.value))} />
            </div>
          </div>
          <div className="flex flex-col gap-1">
            <label htmlFor="sf-baths" className="text-14 font-semibold">دورات المياه (على الأقل)</label>
            <select id="sf-baths" className={inputCls} value={draft.bathrooms} onChange={(e) => set("bathrooms", e.target.value)}>
              <option value="">أي عدد</option>
              {[1, 2, 3, 4].map((n) => <option key={n} value={n}>{n}+</option>)}
            </select>
          </div>
          <div className="flex flex-col gap-1">
            <span className="text-14 font-semibold">التسليم المتوقع</span>
            <div className="flex gap-2">
              <input aria-label="التسليم من" type="month" dir="ltr" className={inputCls} value={draft.deliveryFrom} onChange={(e) => set("deliveryFrom", e.target.value)} />
              <input aria-label="التسليم حتى" type="month" dir="ltr" className={inputCls} value={draft.deliveryTo} onChange={(e) => set("deliveryTo", e.target.value)} />
            </div>
          </div>
          <div className="flex flex-col gap-1">
            <label htmlFor="sf-track" className="text-14 font-semibold">نوع الالتزام</label>
            <select id="sf-track" className={inputCls} value={draft.track} onChange={(e) => set("track", e.target.value)}>
              <option value="">الكل</option>
              <option value="developer">التزام لدى مطور</option>
              <option value="financier">عقار مموّل</option>
              <option value="mixed">مطور وجهة تمويل</option>
            </select>
          </div>
          {features.length ? (
            <fieldset className="m-0 flex flex-col gap-2 border-0 p-0 sm:col-span-2 xl:col-span-4">
              <legend className="mb-1 text-14 font-semibold">المميزات (كلها)</legend>
              <Chips name="features" multiple options={features} value={draft.features.split(",").filter(Boolean)} onChange={(v) => toggleCsv("features", v)} />
            </fieldset>
          ) : null}
        </div>
      ) : null}

      <div className="flex flex-wrap items-center gap-3">
        <Button type="submit" loading={loading}>
          <Icon name="search" size={18} />
          عرض النتائج
        </Button>
        <span role="status" aria-live="polite" className="text-14 text-charcoal">
          {total === null ? "…" : <><strong>{total}</strong> {total === 1 ? "فرصة" : "فرص"}</>}
          {excluded > 0 ? <span className="text-muted"> · استبعدنا {excluded} {excluded === 1 ? "فرصة أرقامها" : "فرص أرقامها"} غير مكتملة للمقارنة</span> : null}
        </span>
      </div>
      {active.length ? (
        <div className="flex flex-wrap items-center gap-2 border-t border-divider pt-3">
          {active.map((a) => (
            <button key={a.key} type="button" onClick={() => onApply({ ...value, ...a.clear, page: "" })}
              className="inline-flex min-h-9 items-center gap-1 rounded-pill border border-rust-200 bg-rust-50 px-3 text-13 text-rust-700" aria-label={`إزالة: ${a.text}`}>
              {a.text}
              <Icon name="close" size={16} />
            </button>
          ))}
          <button type="button" onClick={() => onApply({ ...EMPTY })} className="min-h-9 text-13 font-semibold text-muted underline">مسح كل الفلاتر</button>
        </div>
      ) : null}
    </form>
  );
}

"use client";

import Link from "next/link";
import { useEffect, useId, useRef, useState, type FormEvent, type ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import { DatePicker } from "@/components/ui/DatePicker";
import { Drawer } from "@/components/ui/Dialog";
import { Dropdown, MultiDropdown } from "@/components/ui/Dropdown";
import { Icon } from "@/components/ui/Icon";
import { UnitInput } from "@/components/ui/UnitInput";
import { cn } from "@/lib/cn";
import { useMediaQuery } from "@/lib/hooks";
import { ADVANCED, chips, EMPTY, filtersKey, type SearchKey, type SearchState } from "@/lib/market/search";
import type { Catalog } from "@/lib/market/types";

const DEBOUNCE_MS = 600;

function Field({ label, htmlFor, children, className }: { label: string; htmlFor?: string; children: ReactNode; className?: string }) {
  return (
    <div className={cn("flex min-w-0 flex-col gap-1", className)}>
      <label htmlFor={htmlFor} className="text-13 font-semibold text-charcoal">{label}</label>
      {children}
    </div>
  );
}

/** Small toggles for short option sets (readiness, amenities). */
function MiniChips({ options, values, onToggle, label }: { options: { value: string; label: string }[]; values: string[]; onToggle: (v: string) => void; label: string }) {
  return (
    <div role="group" aria-label={label} className="flex flex-wrap gap-1.5">
      {options.map((o) => {
        const on = values.includes(o.value);
        return (
          <button key={o.value} type="button" aria-pressed={on} onClick={() => onToggle(o.value)}
            className={cn("inline-flex min-h-9 items-center gap-1 rounded-pill border px-3 text-13 transition-colors",
              on ? "border-rust bg-rust-50 font-semibold text-rust-700" : "border-line-strong bg-white text-charcoal hover:border-ink")}>
            {on ? <Icon name="check" size={14} /> : null}
            {o.label}
          </button>
        );
      })}
    </div>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-3 border-b border-divider px-5 py-4 last:border-b-0">
      <h3 className="m-0 text-14 font-bold">{title}</h3>
      {children}
    </section>
  );
}

/**
 * Filters for «what can I pay now, and what can I commit to later». A compact bar keeps the four questions that decide most
 * searches (cash now, installment, city, property type); everything else opens in «فلاتر إضافية» (a side sheet, a bottom sheet
 * on phones). The URL is the state: bar edits apply after a short pause; sheet edits apply with «عرض النتائج». Active filters
 * stay visible as removable chips; nothing is widened silently.
 */
export function SearchPanel({ value, onApply, catalog, signedIn }: {
  value: SearchState;
  onApply: (next: SearchState) => void;
  catalog: Catalog;
  signedIn: boolean;
}) {
  const ids = useId();
  const [draft, setDraft] = useState<SearchState>(value);
  const [sheet, setSheet] = useState(false);
  const desktop = useMediaQuery("(min-width: 640px)");
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

  // Debounced apply of bar edits (typing an amount doesn't fire a request per key). The sheet applies on its own button.
  useEffect(() => {
    if (sheet) return;
    const key = filtersKey(draft);
    if (key === applied.current) return;
    const t = window.setTimeout(() => {
      applied.current = key;
      onApplyRef.current({ ...draft, page: "" });
    }, DEBOUNCE_MS);
    return () => window.clearTimeout(t);
  }, [draft, sheet]);

  const set = (k: SearchKey, v: string) => setDraft((x) => ({ ...x, [k]: v }));
  const csv = (k: SearchKey) => draft[k].split(",").filter(Boolean);
  const toggleCsv = (k: SearchKey, v: string) => set(k, (csv(k).includes(v) ? csv(k).filter((x) => x !== v) : [...csv(k), v]).join(","));
  const apply = (next = draft) => {
    applied.current = filtersKey(next);
    onApply({ ...next, page: "" });
  };
  const submit = (e: FormEvent) => {
    e.preventDefault();
    apply();
  };
  const features = (catalog.fields.find((f) => f.key === "features")?.options ?? [])
    .filter((o) => !o.propertyTypes || csv("types").length === 0 || o.propertyTypes.some((t) => csv("types").includes(t)));
  const advancedCount = ADVANCED.filter((k) => value[k]).length + (desktop ? 0 : (["maxInstallment", "city", "types"] as const).filter((k) => value[k]).length);
  const active = chips(value, catalog);
  const cityOptions = [{ value: "", label: "كل المدن" }, ...catalog.cities.map((c) => ({ value: c.key, label: c.label }))];

  return (
    <>
      <form onSubmit={submit} role="search" aria-label="البحث عن فرصة" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-3 shadow-1 md:p-4">
        <div className="grid items-end gap-3 sm:grid-cols-2 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.35fr)_minmax(0,1fr)_minmax(0,1fr)_auto]">
          <Field label="ما تستطيع دفعه الآن" htmlFor={`${ids}-now`}>
            <UnitInput id={`${ids}-now`} value={draft.maxNow} onChange={(v) => set("maxNow", v)} placeholder="مثال 400000" />
          </Field>
          <Field label="القسط المريح لك" htmlFor={`${ids}-inst`} className="max-sm:hidden">
            <div className="flex gap-2">
              <UnitInput id={`${ids}-inst`} className="flex-1" value={draft.maxInstallment} onChange={(v) => set("maxInstallment", v)} placeholder="اختياري" />
              <Dropdown ariaLabel="دورية القسط" className="w-[7.25rem] flex-none" value={draft.freq || "monthly"} onChange={(v) => set("freq", v)} options={catalog.frequencies} />
            </div>
          </Field>
          <Field label="المدينة" htmlFor={`${ids}-city`} className="max-sm:hidden">
            <Dropdown id={`${ids}-city`} icon="location_on" value={draft.city.includes(",") ? "" : draft.city} onChange={(v) => set("city", v)} options={cityOptions}
              placeholder={draft.city.includes(",") ? "عدة مدن" : "كل المدن"} searchable />
          </Field>
          <Field label="نوع العقار" htmlFor={`${ids}-types`} className="max-sm:hidden">
            <MultiDropdown id={`${ids}-types`} icon="home_work" values={csv("types")} onChange={(v) => set("types", v.join(","))} options={catalog.propertyTypes} placeholder="كل الأنواع" />
          </Field>
          <div className="flex gap-2 sm:col-span-2 lg:col-span-1">
            <Button type="button" variant="secondary" icon="tune" className="min-h-11 flex-1 lg:flex-none" onClick={() => setSheet(true)} aria-haspopup="dialog">
              فلاتر{advancedCount ? ` (${advancedCount})` : ""}
            </Button>
            <Button type="submit" icon="search" className="min-h-11 flex-1 lg:flex-none">بحث</Button>
          </div>
        </div>

        {signedIn || active.length ? (
          <div className="flex flex-wrap items-center gap-2 border-t border-divider pt-3">
            {signedIn ? (
              <label className="me-1 inline-flex min-h-9 cursor-pointer items-center gap-2 text-13 font-semibold">
                <span className="relative inline-flex">
                  <input type="checkbox" className="peer sr-only" checked={draft.match === "me"} onChange={(e) => apply({ ...draft, match: e.target.checked ? "me" : "" })} />
                  <span aria-hidden="true" className="h-5 w-9 rounded-pill bg-track transition-colors peer-checked:bg-rust peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-ink" />
                  <span aria-hidden="true" className="absolute top-0.5 start-0.5 size-4 rounded-full bg-white shadow-1 transition-transform peer-checked:-translate-x-4" />
                </span>
                طابق مع قدرتي الشرائية المسجلة
              </label>
            ) : null}
            {active.filter((a) => a.key !== "match").map((a) => (
              <button key={a.key} type="button" onClick={() => apply({ ...value, ...a.clear })}
                className="inline-flex min-h-8 items-center gap-1 rounded-pill border border-rust-200 bg-rust-50 px-2.5 text-12 text-rust-700 hover:border-rust" aria-label={`إزالة: ${a.text}`}>
                {a.text}
                <Icon name="close" size={14} />
              </button>
            ))}
            {active.length ? (
              <button type="button" onClick={() => apply({ ...EMPTY })} className="min-h-8 px-1 text-12 font-semibold text-muted underline">مسح الكل</button>
            ) : null}
          </div>
        ) : (
          <Link href={`/signin?next=${encodeURIComponent("/opportunities?match=me")}`} className="w-fit text-13">ادخل لمطابقة الفرص مع قدرتك الشرائية</Link>
        )}
      </form>

      <Drawer open={sheet} onClose={() => { setSheet(false); setDraft(value); }} title="فلاتر إضافية" placement={desktop ? "end" : "bottom"}
        footer={
          <>
            <Button className="flex-1" onClick={() => { apply(); setSheet(false); }}>عرض النتائج</Button>
            <Button variant="secondary" onClick={() => setDraft((x) => ({ ...x, ...Object.fromEntries(ADVANCED.map((k) => [k, ""])) }))}>مسح هذه الفلاتر</Button>
          </>
        }>
        {!desktop ? (
          <Section title="الأساسية">
            <Field label="القسط المريح لك" htmlFor={`${ids}-m-inst`}>
              <div className="flex gap-2">
                <UnitInput id={`${ids}-m-inst`} className="flex-1" value={draft.maxInstallment} onChange={(v) => set("maxInstallment", v)} placeholder="اختياري" />
                <Dropdown ariaLabel="دورية القسط" className="w-[7.25rem] flex-none" value={draft.freq || "monthly"} onChange={(v) => set("freq", v)} options={catalog.frequencies} />
              </div>
            </Field>
            <Field label="المدينة" htmlFor={`${ids}-m-city`}>
              <Dropdown id={`${ids}-m-city`} icon="location_on" value={draft.city.includes(",") ? "" : draft.city} onChange={(v) => set("city", v)} options={cityOptions} searchable />
            </Field>
            <Field label="نوع العقار" htmlFor={`${ids}-m-types`}>
              <MultiDropdown id={`${ids}-m-types`} icon="home_work" values={csv("types")} onChange={(v) => set("types", v.join(","))} options={catalog.propertyTypes} placeholder="كل الأنواع" />
            </Field>
          </Section>
        ) : null}
        <Section title="العقار">
          <div className="grid grid-cols-2 gap-3">
            <Field label="غرف النوم" htmlFor={`${ids}-beds`}>
              <Dropdown id={`${ids}-beds`} value={draft.bedrooms} onChange={(v) => set("bedrooms", v)}
                options={[{ value: "", label: "أي عدد" }, ...[1, 2, 3, 4, 5].map((n) => ({ value: String(n), label: `${n}+ غرف` }))]} />
            </Field>
            <Field label="دورات المياه" htmlFor={`${ids}-baths`}>
              <Dropdown id={`${ids}-baths`} value={draft.bathrooms} onChange={(v) => set("bathrooms", v)}
                options={[{ value: "", label: "أي عدد" }, ...[1, 2, 3, 4].map((n) => ({ value: String(n), label: `${n}+` }))]} />
            </Field>
          </div>
          <Field label="المساحة (م²)">
            <div className="flex items-center gap-2">
              <UnitInput ariaLabel="أقل مساحة" unit="م²" value={draft.minArea} onChange={(v) => set("minArea", v)} placeholder="من" className="flex-1" />
              <span className="text-muted">–</span>
              <UnitInput ariaLabel="أكبر مساحة" unit="م²" value={draft.maxArea} onChange={(v) => set("maxArea", v)} placeholder="إلى" className="flex-1" />
            </div>
          </Field>
          <Field label="الحالة">
            <MiniChips label="الحالة" values={[draft.readiness || "any"]} onToggle={(v) => set("readiness", v === "any" ? "" : v)}
              options={[{ value: "any", label: "الكل" }, { value: "ready", label: "جاهز" }, { value: "under_construction", label: "تحت الإنشاء" }]} />
          </Field>
        </Section>
        <Section title="التسليم المتوقع">
          <div className="grid grid-cols-2 gap-3">
            <Field label="من" htmlFor={`${ids}-dfrom`}><DatePicker id={`${ids}-dfrom`} mode="month" value={draft.deliveryFrom} onChange={(v) => set("deliveryFrom", v)} max={draft.deliveryTo || undefined} placeholder="أي وقت" /></Field>
            <Field label="حتى" htmlFor={`${ids}-dto`}><DatePicker id={`${ids}-dto`} mode="month" value={draft.deliveryTo} onChange={(v) => set("deliveryTo", v)} min={draft.deliveryFrom || undefined} placeholder="أي وقت" /></Field>
          </div>
        </Section>
        <Section title="الموقع">
          <Field label="الحي" htmlFor={`${ids}-district`}>
            <input id={`${ids}-district`} className="min-h-11 rounded-sm border border-line-strong bg-white px-3 text-15 outline-none focus:border-rust" value={draft.district}
              onChange={(e) => set("district", e.target.value)} placeholder="مثال: النرجس" />
          </Field>
          <Field label="المشروع أو المطور" htmlFor={`${ids}-project`}>
            <input id={`${ids}-project`} className="min-h-11 rounded-sm border border-line-strong bg-white px-3 text-15 outline-none focus:border-rust" value={draft.project}
              onChange={(e) => set("project", e.target.value)} />
          </Field>
        </Section>
        <Section title="الالتزام والأقساط">
          <Field label="الحد الأقصى لإجمالي الالتزام" htmlFor={`${ids}-total`}>
            <UnitInput id={`${ids}-total`} value={draft.maxTotal} onChange={(v) => set("maxTotal", v)} />
          </Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label="أقصى مدة متبقية" htmlFor={`${ids}-term`}>
              <UnitInput id={`${ids}-term`} unit="شهر" value={draft.maxTerm} onChange={(v) => set("maxTerm", v)} />
            </Field>
            <Field label="نوع الالتزام" htmlFor={`${ids}-track`}>
              <Dropdown id={`${ids}-track`} value={draft.track} onChange={(v) => set("track", v)}
                options={[{ value: "", label: "الكل" }, { value: "developer", label: "التزام لدى مطور" }, { value: "financier", label: "عقار مموّل" }, { value: "mixed", label: "مطور وجهة تمويل" }]} />
            </Field>
          </div>
        </Section>
        {features.length ? (
          <Section title="المميزات (يجب أن تتوفر كلها)">
            <MiniChips label="المميزات" options={features} values={csv("features")} onToggle={(v) => toggleCsv("features", v)} />
          </Section>
        ) : null}
      </Drawer>
    </>
  );
}

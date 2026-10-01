import { label } from "@/lib/market/catalog";
import { monthLabel, sar } from "@/lib/market/format";
import type { Catalog } from "@/lib/market/types";

/**
 * Search state ↔ URL for /opportunities (Phase 2). The server's SearchCriteria is the authority (it validates and canonicalizes);
 * this mirrors its keys so the URL can be shared, reloaded and navigated with back/forward. A buyer's own profile is applied with
 * «match=me» only — never by copying their numbers into the URL.
 */
export const SEARCH_KEYS = [
  "city", "district", "project", "types", "minArea", "maxArea", "bedrooms", "bathrooms", "readiness", "deliveryFrom", "deliveryTo",
  "maxNow", "maxTotal", "maxInstallment", "freq", "maxTerm", "features", "track", "bbox", "match", "sort", "page",
] as const;
export type SearchKey = (typeof SEARCH_KEYS)[number];
export type SearchState = Record<SearchKey, string>;

export const PAGE_SIZE = 12;

export const EMPTY: SearchState = Object.fromEntries(SEARCH_KEYS.map((k) => [k, ""])) as SearchState;

/** Filters kept in the «more» panel (the primary ones stay visible). */
export const ADVANCED: SearchKey[] = ["district", "project", "minArea", "maxArea", "bathrooms", "deliveryFrom", "deliveryTo", "maxTotal", "maxTerm", "features", "track"];

export function fromParams(sp: URLSearchParams | { get(k: string): string | null }): SearchState {
  const s = { ...EMPTY };
  for (const k of SEARCH_KEYS) s[k] = sp.get(k) ?? "";
  // Phase 1 links
  if (s.sort === "fit") s.sort = "relevance";
  if (s.sort === "price") s.sort = "total";
  return s;
}

/** Sorted, defaults left out (freq only with an installment, page only after 1). */
export function toQuery(s: SearchState, { page = true }: { page?: boolean } = {}): string {
  const q = new URLSearchParams();
  for (const k of [...SEARCH_KEYS].sort()) {
    const v = s[k]?.trim();
    if (!v) continue;
    if (k === "freq" && (!s.maxInstallment || v === "monthly")) continue;
    if (k === "page" && (!page || v === "1")) continue;
    q.set(k, v);
  }
  return q.toString();
}

/** Same filters (page and sort excluded): a change of filters goes back to page 1. */
export const filtersKey = (s: SearchState) => toQuery({ ...s, page: "", sort: "" });

export const hasFilters = (s: SearchState) => filtersKey(s) !== "";

const n = (v: string) => sar(Number(v)) ?? v;

export function chips(s: SearchState, catalog: Catalog): { key: SearchKey; text: string; clear: Partial<SearchState> }[] {
  const out: { key: SearchKey; text: string; clear: Partial<SearchState> }[] = [];
  const features = catalog.fields.find((f) => f.key === "features")?.options ?? [];
  const push = (key: SearchKey, text: string, clear: Partial<SearchState> = {}) => out.push({ key, text, clear: { [key]: "", ...clear } });
  if (s.match === "me") push("match", "وفق قدرتي الشرائية المسجلة");
  if (s.city) push("city", s.city.split(",").map((c) => catalog.cities.find((x) => x.key === c)?.label ?? c).join("، "));
  if (s.district) push("district", `حي ${s.district}`);
  if (s.project) push("project", `مشروع أو مطور: ${s.project}`);
  if (s.types) push("types", s.types.split(",").map((t) => label(catalog.propertyTypes, t)).join("، "));
  if (s.maxNow) push("maxNow", `الآن حتى ${n(s.maxNow)} ر.س`);
  if (s.maxInstallment) push("maxInstallment", `قسط حتى ${n(s.maxInstallment)} ر.س ${label(catalog.frequencies, s.freq || "monthly")}`, { freq: "" });
  if (s.maxTotal) push("maxTotal", `الإجمالي حتى ${n(s.maxTotal)} ر.س`);
  if (s.minArea) push("minArea", `من ${s.minArea} م²`);
  if (s.maxArea) push("maxArea", `حتى ${s.maxArea} م²`);
  if (s.bedrooms) push("bedrooms", `${s.bedrooms}+ غرف`);
  if (s.bathrooms) push("bathrooms", `${s.bathrooms}+ دورات مياه`);
  if (s.readiness) push("readiness", s.readiness === "ready" ? "جاهز" : "تحت الإنشاء");
  if (s.deliveryFrom) push("deliveryFrom", `التسليم من ${monthLabel(s.deliveryFrom)}`);
  if (s.deliveryTo) push("deliveryTo", `التسليم حتى ${monthLabel(s.deliveryTo)}`);
  if (s.maxTerm) push("maxTerm", `مدة متبقية حتى ${s.maxTerm} شهرًا`);
  if (s.features) push("features", s.features.split(",").map((f) => features.find((o) => o.value === f)?.label ?? f).join("، "));
  if (s.track) push("track", s.track === "developer" ? "التزام لدى مطور" : s.track === "financier" ? "عقار مموّل" : "مطور وجهة تمويل");
  if (s.bbox) push("bbox", "منطقة محددة على الخريطة");
  return out;
}

export const SORTS = [
  { value: "relevance", label: "الأنسب" },
  { value: "now", label: "الأقل احتياجًا للدفع الآن" },
  { value: "total", label: "الأقل في الإجمالي" },
  { value: "newest", label: "الأحدث" },
];

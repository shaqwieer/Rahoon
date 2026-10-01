import type { Answers, Catalog, FieldDef, FieldOption } from "./types";

/**
 * Client view of the central field catalog served by GET /api/market/catalog. The server applies the very same rules on
 * every save (FieldCatalog.cs); this file only decides what to show, so a hidden field never blocks anything.
 */
export const UNKNOWN = "__unknown";

export function applies(f: FieldDef, propertyType: string | null | undefined, obligationKind: string | null | undefined, answers: Answers): boolean {
  if (f.scope === "property" && f.propertyTypes && (!propertyType || !f.propertyTypes.includes(propertyType))) return false;
  if (f.scope === "obligation" && f.obligationKinds && (!obligationKind || !f.obligationKinds.includes(obligationKind))) return false;
  if (f.when && !f.when.values.includes(answers[f.when.key] ?? "")) return false;
  return true;
}

export const propertyFields = (c: Catalog, type: string | null | undefined, answers: Answers) => c.fields.filter((f) => f.scope === "property" && applies(f, type, null, answers));
export const obligationFields = (c: Catalog, kind: string, answers: Answers) => c.fields.filter((f) => f.scope === "obligation" && applies(f, null, kind, answers));

export function optionsFor(f: FieldDef, propertyType: string | null | undefined): FieldOption[] {
  return (f.options ?? []).filter((o) => !o.propertyTypes || (propertyType ? o.propertyTypes.includes(propertyType) : false));
}

export const isAnswered = (a: Answers, key: string) => (a[key] ?? "").length > 0;
export const isKnown = (a: Answers, key: string) => isAnswered(a, key) && a[key] !== UNKNOWN && a[key] !== "unknown";

/** Drops answers that no longer apply (type or party switched, condition off) — the same pruning the server does. */
export function prune(c: Catalog, scope: "property" | "obligation", answers: Answers, propertyType: string | null | undefined, kind: string | null | undefined): Answers {
  const out: Answers = {};
  const fields = c.fields.filter((f) => f.scope === scope);
  for (const pass of [0, 1]) {
    for (const f of fields.filter((x) => (pass === 0 ? !x.when : Boolean(x.when)))) {
      const v = answers[f.key];
      if (v === undefined || v === "") continue;
      if (applies(f, propertyType, kind, out)) out[f.key] = v;
    }
  }
  return out;
}

export const label = (opts: FieldOption[], value: string | null | undefined) => opts.find((o) => o.value === value)?.label ?? value ?? "";
export const cityLabel = (c: Catalog, key: string | null | undefined) => c.cities.find((x) => x.key === key)?.label ?? key ?? "";

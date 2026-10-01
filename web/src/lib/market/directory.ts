import { apiSend } from "@/lib/api/client";

export { ORG_TYPE_LABELS } from "./orgTypes";

/** One organization of the directory as the forms see it (active records only). */
export interface DirectoryOrg {
  id: string;
  nameAr: string;
  nameEn: string | null;
  types: string[];
  website: string | null;
}

/** developer → developers; financier → banks and finance companies (the API decides). */
export type DirectoryKind = "developer" | "financier";

const cache = new Map<DirectoryKind, Promise<DirectoryOrg[]>>();

/** The directory for one kind, fetched once per page load. */
export function loadDirectory(kind: DirectoryKind): Promise<DirectoryOrg[]> {
  let p = cache.get(kind);
  if (!p) {
    p = apiSend<{ items: DirectoryOrg[] }>("GET", `/directory/organizations?kind=${kind}`).then((r) => r.items);
    p.catch(() => cache.delete(kind));
    cache.set(kind, p);
  }
  return p;
}

/** Same idea as the server's DirectoryNames.Normalize: diacritics, alef/hamza, taa marbuta, alef maqsura, case. */
export function normalizeName(value: string): string {
  return value
    .normalize("NFKC")
    .replace(/[ً-ٰٟـ]/g, "")
    .replace(/[أإآٱ]/g, "ا")
    .replace(/ة/g, "ه")
    .replace(/[ىئ]/g, "ي")
    .replace(/ؤ/g, "و")
    .toLowerCase()
    .replace(/[^\p{L}\p{N}]+/gu, " ")
    .trim();
}

/** Matches every word of the query against the Arabic and English names. */
export function matchesOrg(org: DirectoryOrg, query: string): boolean {
  const words = normalizeName(query).split(" ").filter(Boolean);
  if (words.length === 0) return true;
  const hay = `${normalizeName(org.nameAr)} ${normalizeName(org.nameEn ?? "")}`;
  return words.every((w) => hay.includes(w));
}

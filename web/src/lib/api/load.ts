import "server-only";
import { notFound, redirect } from "next/navigation";
import { apiFetch, currentPath, loginUrl } from "./server";
import { isApiError, parseResponse } from "./types";

/** Outcome of a tab-level read: data, or a state the page renders in place (C11) instead of leaving the case chrome. */
export type Loaded<T> =
  | { ok: true; data: T }
  | { ok: false; kind: "forbidden" | "error"; status: number; errorRef: string; title: string | null };

/**
 * GET for case tabs and list pages. Unlike `apiGet`, a 403 (the role lacks the tab permission, e.g. audit.view)
 * and any failure are returned as a state so the page keeps the CaseHeader and shows SystemState.
 * 401 still goes to login and 404 to notFound(). The API remains the authority.
 */
export async function apiLoad<T>(path: string): Promise<Loaded<T>> {
  let res: Response;
  try {
    res = await apiFetch(path);
  } catch {
    return { ok: false, kind: "error", status: 0, errorRef: "ERR-NET0", title: null };
  }
  if (res.status === 401) redirect(loginUrl(await currentPath()));
  if (res.status === 404) notFound();
  try {
    return { ok: true, data: await parseResponse<T>(res) };
  } catch (e) {
    const title = isApiError(e) && e.title ? e.title : null;
    const status = isApiError(e) ? e.status : 0;
    return { ok: false, kind: status === 403 ? "forbidden" : "error", status, errorRef: `ERR-${String(status || "0000").padStart(4, "0")}`, title };
  }
}

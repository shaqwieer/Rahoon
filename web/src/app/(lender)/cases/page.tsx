import type { Metadata } from "next";
import { apiGet, getMe } from "@/lib/api/server";
import type { CaseListData } from "@/lib/api/lender";
import { CasesView } from "./CasesView";

export const metadata: Metadata = { title: "الحالات" };

const VIEWS = ["mine", "action", "overdue", "expiring", "all", "drafts", "closed"] as const;

/** L02 — Case list with saved views, filters, safe bulk actions (P0 anchor). */
export default async function CasesPage({ searchParams }: PageProps<"/cases">) {
  const sp = await searchParams;
  const str = (k: string) => (typeof sp[k] === "string" ? (sp[k] as string) : "");
  const view = (VIEWS as readonly string[]).includes(str("view")) ? str("view") : "mine";
  const qs = new URLSearchParams({ view, page: str("page") || "1", pageSize: "10" });
  for (const k of ["q", "status", "sla", "region", "sort", "minAmount", "maxAmount"]) if (str(k)) qs.set(k, str(k));
  const [data, me] = await Promise.all([apiGet<CaseListData>(`/cases?${qs}`), getMe()]);
  const perms = me.authenticated ? me.permissions : [];
  return (
    <CasesView
      data={data}
      view={view}
      filters={{ q: str("q"), status: str("status"), sla: str("sla"), region: str("region"), sort: str("sort") }}
      canCreate={perms.includes("case.create")}
      canImport={perms.includes("case.import")}
      canAssign={perms.includes("case.assign")}
      canExport={perms.includes("case.export")}
    />
  );
}

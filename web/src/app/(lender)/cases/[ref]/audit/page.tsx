import type { Metadata } from "next";
import { LoadFailure } from "@/components/case/LoadFailure";
import { apiLoad } from "@/lib/api/load";
import type { AuditVerifyData, CaseAuditData } from "@/lib/api/audit";
import { AuditView, type AuditFilters } from "./AuditView";

export const metadata: Metadata = { title: "السجل" };

const PAGE_SIZE = 50;

/** L24 — Case audit log: filtered append-only history (blocked attempts included), chain verification, signed CSV export. */
export default async function CaseAuditPage({ params, searchParams }: PageProps<"/cases/[ref]/audit">) {
  const { ref } = await params;
  const sp = await searchParams;
  const str = (k: string) => (typeof sp[k] === "string" ? (sp[k] as string) : "");
  const filters: AuditFilters = {
    type: str("type"),
    actor: str("actor"),
    blocked: str("blocked") === "true",
    from: /^\d{4}-\d{2}-\d{2}$/.test(str("from")) ? str("from") : "",
    to: /^\d{4}-\d{2}-\d{2}$/.test(str("to")) ? str("to") : "",
  };
  const page = Math.max(1, Number.parseInt(str("page"), 10) || 1);

  const qs = new URLSearchParams();
  if (filters.type) qs.set("type", filters.type);
  if (filters.actor) qs.set("actor", filters.actor);
  if (filters.blocked) qs.set("blocked", "true");
  if (filters.from) qs.set("from", filters.from);
  if (filters.to) qs.set("to", filters.to);
  const filterQuery = qs.toString();
  const listQs = new URLSearchParams(qs);
  listQs.set("page", String(page));
  listQs.set("pageSize", String(PAGE_SIZE));

  const base = `/cases/${encodeURIComponent(ref)}/audit`;
  const [list, verify] = await Promise.all([apiLoad<CaseAuditData>(`${base}?${listQs}`), apiLoad<AuditVerifyData>(`${base}/verify`)]);

  if (!list.ok) {
    return (
      <section aria-labelledby="audit-h" className="flex flex-col gap-4">
        <h2 id="audit-h" className="m-0 text-20 font-bold">سجل التدقيق</h2>
        <LoadFailure state={list} forbiddenTitle="لا تملك صلاحية عرض سجل التدقيق" forbiddenBody="سجل التدقيق متاح لفريق الحالة والمدقق والامتثال. لم نعرض أي أحداث." />
      </section>
    );
  }

  return <AuditView key={listQs.toString()} reference={ref} data={list.data} verify={verify.ok ? verify.data : null} filters={filters} filterQuery={filterQuery} />;
}

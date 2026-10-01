import type { Metadata } from "next";
import Link from "next/link";
import { QueueTabs, TeamHeader } from "@/components/market/team/TeamBits";
import { Badge } from "@/components/market/ui";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { apiGet } from "@/lib/api/server";
import { dayTime } from "@/lib/market/format";

export const metadata: Metadata = { title: "السجل" };

interface AuditItem {
  seq: number;
  type: string;
  title: string;
  actorLabel: string | null;
  actorRole: string | null;
  subjectType: string | null;
  subjectReference: string | null;
  reason: string | null;
  detail: string | null;
  fromState: string | null;
  toState: string | null;
  occurredAt: string;
  blocked: boolean;
  data: string | null;
}
interface AuditResult { items: AuditItem[]; total: number; page: number; pages: number }

const str = (v: string | string[] | undefined) => (typeof v === "string" ? v : "");
const GROUPS = [
  { key: "", label: "الكل" },
  { key: "team", label: "الفريق" },
  { key: "role", label: "الأدوار" },
  { key: "auth", label: "الدخول" },
  { key: "directory", label: "الدليل" },
  { key: "market", label: "إظهار بيانات العملاء" },
];

/** Grants before → after, from the event data of role and membership changes (no secrets are ever recorded). */
function GrantChange({ data }: { data: string }) {
  let parsed: Record<string, unknown> | null = null;
  try { parsed = JSON.parse(data) as Record<string, unknown>; } catch { return null; }
  const before = (parsed.before ?? null) as { NameAr?: string }[] | Record<string, string> | null;
  const after = (parsed.after ?? null) as { NameAr?: string }[] | Record<string, string> | null;
  if (!before && !after) return null;
  const show = (x: typeof before) => Array.isArray(x) ? x.map((r) => r.NameAr).join("، ") : x ? Object.entries(x).map(([k, s]) => `${k} (${s})`).join("، ") : "—";
  return (
    <span className="flex flex-col text-12">
      <span><span className="text-muted">قبل:</span> {show(before) || "—"}</span>
      <span><span className="text-muted">بعد:</span> {show(after) || "—"}</span>
    </span>
  );
}

/** The team's append-only, hash-chained log (audit.read): who did what, when, on which subject, and why. */
export default async function TeamAudit({ searchParams }: PageProps<"/team/audit">) {
  const sp = await searchParams;
  const group = str(sp.group), q = str(sp.q);
  const page = Number(str(sp.page)) || 1;
  const qs = (over: Record<string, string | number>) => {
    const p = new URLSearchParams();
    for (const [k, v] of Object.entries({ group, q, page: 1, ...over })) if (v !== "" && !(k === "page" && v === 1)) p.set(k, String(v));
    const s = p.toString();
    return s ? `?${s}` : "";
  };
  const data = await apiGet<AuditResult>(`/team/admin/audit${qs({ page })}`);
  return (
    <div>
      <TeamHeader title="السجل" sub="سجل لا يُعدّل ولا يُحذف: الدخول، تغييرات الفريق والأدوار، الدليل، وكل إظهار لجوال عميل. لا يحفظ كلمات مرور ولا محتوى مستندات." />
      <QueueTabs current={group || ""} tabs={GROUPS.map((g) => ({ key: g.key, label: g.label, href: `/team/audit${qs({ group: g.key })}` }))} />
      <form method="get" action="/team/audit" className="mb-4 flex flex-wrap items-end gap-3" role="search">
        {group ? <input type="hidden" name="group" value={group} /> : null}
        <label className="flex min-w-[220px] flex-1 flex-col gap-1 text-14">
          بحث في العنوان أو المنفّذ أو المرجع
          <input name="q" defaultValue={q} className="min-h-11 rounded-sm border border-line-strong bg-white px-3 text-15" />
        </label>
        <button type="submit" className={buttonClasses({ variant: "secondary" })}>بحث</button>
      </form>
      {data.items.length === 0 ? (
        <p className="m-0 rounded-lg border border-dashed border-line-strong bg-white p-6 text-center text-15 text-muted">لا توجد أحداث تطابق البحث.</p>
      ) : (
        <ol className="m-0 flex list-none flex-col gap-2 p-0">
          {data.items.map((e) => (
            <li key={e.seq} className="flex flex-col gap-1 rounded-md border border-line bg-white p-3">
              <span className="flex flex-wrap items-center gap-2">
                <strong className="text-15">{e.title}</strong>
                {e.blocked ? <Badge tone="err">محجوب</Badge> : null}
                {e.fromState && e.toState ? <Badge>{e.fromState} ← {e.toState}</Badge> : null}
              </span>
              <span className="text-13 text-muted">
                {e.actorLabel ?? "النظام"}{e.actorRole ? ` (${e.actorRole})` : ""} · {dayTime(e.occurredAt)} · <bdi dir="ltr" className="font-mono text-12">{e.type}</bdi>
              </span>
              {e.reason ? <span className="text-13">السبب: {e.reason}</span> : null}
              {e.detail ? <span className="text-13"><bdi dir="ltr">{e.detail}</bdi></span> : null}
              {e.data ? <GrantChange data={e.data} /> : null}
            </li>
          ))}
        </ol>
      )}
      {data.pages > 1 ? (
        <nav aria-label="الصفحات" className="mt-4 flex items-center gap-3 text-14">
          {data.page > 1 ? <Link href={`/team/audit${qs({ page: data.page - 1 })}`}>الأحدث</Link> : null}
          <span>صفحة {data.page} من {data.pages}</span>
          {data.page < data.pages ? <Link href={`/team/audit${qs({ page: data.page + 1 })}`}>الأقدم</Link> : null}
        </nav>
      ) : null}
    </div>
  );
}

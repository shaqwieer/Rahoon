import type { Metadata } from "next";
import Link from "next/link";
import { NextActionPanel, RevealParty, SensitivePanel } from "@/components/case/OverviewActions";
import { Icon, Tag, buttonClasses } from "@/components/ui";
import { getWorkspace } from "@/lib/api/case";
import { SOLUTION_KIND_LABEL } from "@/lib/api/lender";
import { formatDate, formatMoney, formatNumber } from "@/lib/format";
import type { Tone } from "@/components/ui/tones";

export async function generateMetadata({ params }: PageProps<"/cases/[ref]">): Promise<Metadata> {
  const { ref } = await params;
  return { title: ref };
}

/** L05 — Case workspace overview (P0 anchor): next action first, figures with sources, solution versions, documents, tasks. */
export default async function CaseOverviewPage({ params }: PageProps<"/cases/[ref]">) {
  const { ref } = await params;
  const ws = await getWorkspace(ref);
  const h = ws.header;

  return (
    <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1fr)_360px]">
      <div className="flex min-w-0 flex-col gap-5">
        <NextActionPanel reference={h.reference} status={h.status} na={ws.nextAction} actions={ws.actions} />

        <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
          {ws.figures.map((f) => (
            <div key={f.key} className="flex flex-col gap-1.5 rounded-md border border-line bg-white p-4">
              <span className="text-13 text-muted">{f.label}</span>
              <span className="text-20 leading-8 font-bold">
                <bdi dir="ltr" className="tabular-nums">{f.value === null ? "—" : f.unit === "%" ? formatNumber(f.value, 1) : formatMoney(f.value)}</bdi>{" "}
                <span className="text-14 font-medium">{f.unit}</span>
              </span>
              <span className="flex items-center gap-1 text-12 text-muted">
                <Icon name={f.icon} size={14} />
                {f.source}
              </span>
            </div>
          ))}
        </div>

        <section aria-labelledby="sol-h" className="overflow-hidden rounded-lg border border-line bg-white">
          <div className="flex items-center justify-between border-b border-divider px-5 py-3">
            <h2 id="sol-h" className="m-0 text-18 font-semibold">الحلول وإصداراتها</h2>
            <Link href={`/cases/${h.reference}/solutions`} className="text-14 font-semibold">مقارنة الإصدارات</Link>
          </div>
          {ws.solutions.length === 0 ? (
            <p className="m-0 px-5 py-4 text-14 text-muted">لا توجد حلول بعد. يُتاح إعداد الحل في مرحلة «حل مقترح».</p>
          ) : (
            <ul className="m-0 list-none p-0">
              {ws.solutions.map((s) => (
                <li key={s.version} className="grid grid-cols-[56px_minmax(0,1fr)_auto] items-center gap-3 border-t border-divider px-5 py-3 first:border-t-0">
                  <span dir="ltr" className="font-mono text-16 font-bold">v{s.version}</span>
                  <Link href={`/cases/${h.reference}/solutions/${s.version}`} className="flex min-w-0 flex-col text-ink no-underline">
                    <strong className="text-15">
                      {SOLUTION_KIND_LABEL[s.kind]} {s.termMonths} شهراً · قسط <bdi dir="ltr">{formatMoney(s.installment)}</bdi> ر.س
                    </strong>
                    <span className="text-13 text-muted">
                      {s.returnReason
                        ? <>أعاده {s.returnedBy ?? "المراجِع"} للتعديل · «{s.returnReason}»</>
                        : <>{s.waiver > 0 ? <>تنازل عن غرامات <bdi dir="ltr">{formatMoney(s.waiver)}</bdi> ر.س · </> : null}أعدّه {s.preparedBy} · <bdi dir="ltr">{formatDate(s.preparedAt)}</bdi></>}
                    </span>
                  </Link>
                  <span className="flex flex-col items-end gap-0.5 text-12">
                    <span className="text-muted">{s.stage}</span>
                    <Tag tone={(s.tone === "muted" ? "neutral" : s.tone) as Tone}>{s.stateText}</Tag>
                  </span>
                </li>
              ))}
            </ul>
          )}
        </section>

        <div className="grid gap-4 md:grid-cols-2">
          <section aria-labelledby="docs-h" className="flex flex-col gap-2 rounded-lg border border-line bg-white p-4">
            <div className="flex items-center justify-between">
              <h2 id="docs-h" className="m-0 text-16 font-semibold">المستندات</h2>
              <Link href={`/cases/${h.reference}/documents`} className="text-14 font-semibold">كل المستندات ({formatNumber(ws.documents.total)})</Link>
            </div>
            {ws.documents.expiring.map((d) => (
              <div key={d.id} className="flex items-center gap-2 rounded-sm bg-warn-bg px-3 py-2 text-14 text-warn">
                <Icon name="event_upcoming" size={18} />
                <span className="flex-1">{d.text}</span>
                <Link href={`/cases/${h.reference}/documents?request=${d.id}`} className="font-semibold">طلب تحديث</Link>
              </div>
            ))}
            {ws.documents.needsAttention.map((d) => (
              <div key={d.id} className="flex items-center gap-2 text-14">
                <Icon name={d.status === "Rejected" ? "undo" : "upload_file"} size={18} className={d.status === "Rejected" ? "text-err" : "text-info"} />
                <span>{d.name} · {d.status === "Rejected" ? "أُعيد الطلب" : "مطلوب"}</span>
              </div>
            ))}
            <div className="flex items-center gap-2 text-14 text-ok">
              <Icon name="check_circle" size={18} />
              {formatNumber(ws.documents.verified)} مستندات متحقق منها
            </div>
          </section>

          <section aria-labelledby="tasks-h" className="flex flex-col gap-2 rounded-lg border border-line bg-white p-4">
            <div className="flex items-center justify-between">
              <h2 id="tasks-h" className="m-0 text-16 font-semibold">المهام المفتوحة</h2>
              <Link href={`/cases/${h.reference}/comms`} className="text-14 font-semibold">كل المهام ({formatNumber(ws.tasks.length)})</Link>
            </div>
            {ws.tasks.length === 0 ? <p className="m-0 text-14 text-muted">لا مهام مفتوحة.</p> : null}
            {ws.tasks.map((t) => (
              <div key={t.id} className="flex items-center gap-2 text-14">
                <Icon name="radio_button_unchecked" size={18} className="text-muted" />
                <span className="flex-1">{t.title}{t.assignee ? ` · ${t.assignee}` : ""}</span>
                <span className="text-13 text-muted"><bdi dir="ltr">{t.dueText}</bdi></span>
              </div>
            ))}
          </section>
        </div>
      </div>

      <aside className="flex flex-col gap-4">
        <section aria-labelledby="parties-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-4">
          <div className="flex items-center justify-between">
            <h2 id="parties-h" className="m-0 text-16 font-semibold">الأطراف</h2>
            <span className="text-12 text-muted">{h.ownerAccessLabel}</span>
          </div>
          {ws.parties.primary ? (
            <div className="flex items-center gap-3">
              <span className="grid size-9 shrink-0 place-items-center rounded-full bg-subtle text-13 font-semibold">{ws.parties.primary.initials}</span>
              <div className="flex min-w-0 flex-1 flex-col text-14">
                <span className="font-semibold">{ws.parties.primary.name} · {ws.parties.primary.role}</span>
                <span className="flex flex-wrap items-center gap-1 text-13 text-muted">
                  {ws.parties.primary.nationalIdMasked && ws.parties.primary.canReveal ? (
                    <RevealParty reference={h.reference} partyId={ws.parties.primary.id} masked={ws.parties.primary.nationalIdMasked} label="الهوية" />
                  ) : (
                    <>الهوية <bdi dir="ltr" className="font-mono">{ws.parties.primary.nationalIdMasked ?? "—"}</bdi></>
                  )}
                  · {ws.parties.primary.language}
                </span>
              </div>
            </div>
          ) : null}
          {ws.parties.providers.map((p) => (
            <div key={p.reference} className="flex items-center gap-3">
              <span className="grid size-9 shrink-0 place-items-center rounded-full bg-info-bg text-info"><Icon name="assignment" size={18} /></span>
              <div className="flex min-w-0 flex-col text-14">
                <span className="font-semibold">{p.provider}</span>
                <span className="text-13 text-muted">{p.text}</span>
              </div>
            </div>
          ))}
          <Link href={`/cases/${h.reference}/parties`} className={buttonClasses({ variant: "text", size: "sm" })}>كل الأطراف</Link>
        </section>

        <section aria-labelledby="act-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-4">
          <div className="flex items-center justify-between">
            <h2 id="act-h" className="m-0 text-16 font-semibold">آخر النشاط</h2>
            <Link href={`/cases/${h.reference}/audit`} className="text-14 font-semibold">السجل الكامل</Link>
          </div>
          <ol className="m-0 flex list-none flex-col gap-3 p-0">
            {ws.activity.map((a, i) => (
              <li key={i} className="flex gap-2">
                <Icon name={a.blocked ? "block" : a.type.startsWith("case.transition") ? "swap_horiz" : a.type.startsWith("document") ? "upload_file" : a.type.startsWith("solution") ? "edit" : "history"}
                  size={18} className={a.blocked ? "text-err" : "text-muted"} />
                <div className="flex flex-col">
                  <span className="text-14">{a.title}</span>
                  <span className="text-12 text-muted">{a.meta}</span>
                </div>
              </li>
            ))}
          </ol>
        </section>

        <SensitivePanel ws={ws} />
      </aside>
    </div>
  );
}

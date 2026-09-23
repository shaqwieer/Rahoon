import type { Metadata } from "next";
import Link from "next/link";
import { BarList, Icon, KpiTile, Ref, SlaBadge, buttonClasses } from "@/components/ui";
import { apiGet } from "@/lib/api/server";
import { toSlaTone, type PortfolioData } from "@/lib/api/lender";
import { formatDateTime, formatHijri, formatNumber } from "@/lib/format";
import { PortfolioScope } from "./PortfolioScope";

export const metadata: Metadata = { title: "المحفظة" };

/** Charts/KPIs round to millions/billions and say so in the unit (Foundations amount rule). */
const compactValue = (v: number) => formatNumber(v >= 1e9 ? v / 1e9 : v >= 1e6 ? v / 1e6 : v, 2);
const compactUnit = (v: number) => (v >= 1e9 ? "مليار ر.س" : v >= 1e6 ? "مليون ر.س" : "ر.س");

/** L01 — Portfolio dashboard (P0 anchor). What needs action today, where cases pile up, SLA by stage. */
export default async function PortfolioPage({ searchParams }: PageProps<"/portfolio">) {
  const sp = await searchParams;
  const region = typeof sp.region === "string" ? sp.region : "";
  const scope = sp.scope === "mine" ? "mine" : "all";
  const qs = new URLSearchParams();
  if (region) qs.set("region", region);
  if (scope === "mine") qs.set("scope", "mine");
  const d = await apiGet<PortfolioData>(`/portfolio${qs.size ? `?${qs}` : ""}`);
  const k = d.kpis;
  const hour = Number(new Intl.DateTimeFormat("en-GB", { hour: "numeric", hour12: false, timeZone: "Asia/Riyadh" }).format(new Date()));
  const greeting = hour < 12 ? "صباح الخير" : "مساء الخير";
  const exportQs = new URLSearchParams({ view: scope === "mine" ? "mine" : "all" });

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-end gap-4">
        <div className="flex min-w-0 flex-1 flex-col gap-1">
          <h1 className="m-0 text-24 leading-9 font-bold md:text-32 md:leading-[44px]">
            {greeting}، {d.greetingName}
          </h1>
          <p className="m-0 flex flex-wrap items-center gap-1.5 text-14 text-muted">
            <Icon name="database" size={16} />
            بيانات المحفظة من {d.sync.source} · آخر مزامنة <bdi dir="ltr">{d.sync.at ? formatDateTime(d.sync.at) : "—"}</bdi>
            {d.sync.at ? <> · {formatHijri(d.sync.at)}</> : null}
          </p>
        </div>
        <PortfolioScope regions={d.regions} region={region} scope={scope} />
        <a className={buttonClasses({ variant: "secondary" })} href={`/api/cases/export?${exportQs}`} download>
          <Icon name="download" size={18} />
          تصدير
        </a>
      </div>

      <div className="grid grid-cols-2 gap-3 md:gap-4 xl:grid-cols-4">
        <KpiTile icon="folder_open" label="الحالات النشطة" value={formatNumber(k.active)} href="/cases?view=all"
          sub={`+${formatNumber(k.newThisMonth)} هذا الشهر · ${formatNumber(k.closed90)} أُغلقت خلال 90 يوماً`} />
        <KpiTile icon="task_alt" iconTone="rust" label="بحاجة لإجرائك" value={formatNumber(k.myTasks)} unit="مهام" href="/tasks"
          sub={`${formatNumber(k.myTasksDueSoon)} منها خلال يومين`} />
        <KpiTile icon="alarm_off" iconTone="err" label="تجاوزت المهلة" value={formatNumber(k.overdue)} unit="حالة" href="/cases?view=overdue"
          sub={`${formatNumber(k.overduePercent, 1)}% من النشطة${k.overdueTop ? ` · ${formatNumber(k.overdueTop.n)} في ${k.overdueTop.label}` : ""}`} />
        <KpiTile icon="account_balance_wallet" label="المديونية القائمة" value={compactValue(k.outstanding)} unit={compactUnit(k.outstanding)}
          sub={`المصدر: ${d.sync.source}${d.sync.at ? ` · ${formatDateTime(d.sync.at).slice(-5)}` : ""}`} />
      </div>

      <div className="grid items-start gap-4 lg:grid-cols-[minmax(0,1.15fr)_minmax(0,1fr)]">
        <section aria-labelledby="attention-h" className="overflow-hidden rounded-lg border border-line bg-white">
          <div className="flex items-center justify-between border-b border-divider px-5 py-4">
            <h2 id="attention-h" className="m-0 text-18 leading-7 font-semibold">بحاجة لإجرائك</h2>
            <Link href="/tasks" className="text-14 font-semibold">كل مهامي ({formatNumber(k.myTasks)})</Link>
          </div>
          {d.attention.length === 0 ? (
            <p className="m-0 px-5 py-6 text-14 text-muted">لا مهام مفتوحة لك الآن.</p>
          ) : (
            <ul className="m-0 list-none p-0">
              {d.attention.map((a) => (
                <li key={a.id} className="border-t border-divider first:border-t-0">
                  <Link href={`/cases/${a.caseRef}`} className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-3 px-5 py-3.5 text-ink no-underline hover:bg-warm">
                    <span className="flex min-w-0 flex-col gap-0.5">
                      <span className="text-15 font-semibold">{a.task}</span>
                      <span className="text-13 text-muted">
                        <Ref strong={false}>{a.caseRef}</Ref> · {a.owner ?? "—"} · {a.state}
                      </span>
                    </span>
                    <SlaBadge tone={toSlaTone(a.slaTone)} size="sm">{a.slaText}</SlaBadge>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </section>

        <BarList
          title="توزيع الحالات النشطة حسب الحالة"
          items={d.distribution.map((x) => ({ key: x.status, label: x.label, value: x.n, highlight: x.status === "proposed_solution" }))}
          columns={{ label: "الحالة", value: "عدد الحالات" }}
          unit="حالة"
        />
      </div>

      <section aria-labelledby="sla-h" className="overflow-hidden rounded-lg border border-line bg-white">
        <h2 id="sla-h" className="m-0 px-5 py-3 text-16 font-semibold">مستوى الخدمة حسب المرحلة</h2>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[640px] border-collapse text-14">
            <thead>
              <tr className="bg-warm text-start text-muted">
                <th scope="col" className="px-5 py-2 text-start font-semibold">المرحلة</th>
                <th scope="col" className="px-3 py-2 text-start font-semibold">الحالات</th>
                <th scope="col" className="px-3 py-2 text-start font-semibold">الوسيط (أيام)</th>
                <th scope="col" className="px-3 py-2 text-start font-semibold">المهلة المعيارية</th>
                <th scope="col" className="px-3 py-2 text-start font-semibold">تجاوزت المهلة</th>
              </tr>
            </thead>
            <tbody>
              {d.slaByStage.map((s) => (
                <tr key={s.status} className="border-t border-divider">
                  <th scope="row" className="px-5 py-2 text-start font-semibold">{s.label}</th>
                  <td className="px-3 py-2 tabular-nums"><bdi dir="ltr">{formatNumber(s.cases)}</bdi></td>
                  <td className="px-3 py-2 tabular-nums"><bdi dir="ltr">{formatNumber(s.medianDays)}</bdi></td>
                  <td className="px-3 py-2 text-muted">{s.target} (افتراض)</td>
                  <td className="px-3 py-2">
                    <Link href={`/cases?view=overdue&status=${s.status}`} className="inline-flex items-center gap-1 font-semibold text-err">
                      <Icon name="alarm_off" size={16} />
                      <bdi dir="ltr">{formatNumber(s.overdue)}</bdi> حالة
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}

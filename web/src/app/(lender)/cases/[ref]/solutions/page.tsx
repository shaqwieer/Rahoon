import type { Metadata } from "next";
import Link from "next/link";
import { Icon, Tag, buttonClasses } from "@/components/ui";
import { apiGet, getMe } from "@/lib/api/server";
import { SOLUTION_KIND_LABEL, type SolutionContext, type SolutionDto } from "@/lib/api/lender";
import { formatDate, formatMoney, formatNumber, formatPercent } from "@/lib/format";
import type { Tone } from "@/components/ui/tones";

export const metadata: Metadata = { title: "الحلول" };

const STATUS: Record<string, { label: string; tone: Tone }> = {
  Draft: { label: "مسودة", tone: "neutral" },
  InReview: { label: "بانتظار المراجعة", tone: "info" },
  PendingApproval: { label: "بانتظار الاعتماد", tone: "warn" },
  Approved: { label: "معتمد", tone: "ok" },
  Returned: { label: "أُعيد", tone: "err" },
  Rejected: { label: "مرفوض", tone: "err" },
  Superseded: { label: "حل محله إصدار أحدث", tone: "neutral" },
  Offered: { label: "أُرسل للمالك", tone: "info" },
  Accepted: { label: "قبله المالك", tone: "ok" },
  Declined: { label: "رفضه المالك", tone: "err" },
  Countered: { label: "عرض مقابل", tone: "warn" },
  Expired: { label: "انتهت صلاحيته", tone: "neutral" },
};

/** L14 — Solution comparison: every version side by side with DSR against the policy limit. */
export default async function SolutionsPage({ params }: PageProps<"/cases/[ref]/solutions">) {
  const { ref } = await params;
  const [{ context: ctx, versions }, me] = await Promise.all([
    apiGet<{ context: SolutionContext; versions: SolutionDto[] }>(`/cases/${ref}/solutions`),
    getMe(),
  ]);
  const canPrepare = me.authenticated && me.permissions.includes("solution.prepare") && ["proposed_solution", "negotiation"].includes(ctx.caseStatus);
  const hasDraft = versions.some((v) => v.status === "Draft");
  const rows: Array<[string, (v: SolutionDto) => React.ReactNode]> = [
    ["النوع", (v) => SOLUTION_KIND_LABEL[v.kind]],
    ["المدة", (v) => `${formatNumber(v.termMonths)} شهراً`],
    ["القسط الشهري", (v) => <bdi dir="ltr">{formatMoney(v.installmentAmount)}</bdi>],
    ["أول قسط", (v) => <bdi dir="ltr">{formatDate(v.firstDueDate)}</bdi>],
    ["آخر قسط", (v) => <bdi dir="ltr">{formatDate(v.lastDueDate)}</bdi>],
    ["التنازل", (v) => <><bdi dir="ltr">{formatMoney(v.waiverAmount)}</bdi> ({formatPercent(v.waiverPercent * 100, { fractionDigits: 2 })})</>],
    ["المبلغ المعاد جدولته", (v) => <bdi dir="ltr">{formatMoney(v.rescheduledAmount)}</bdi>],
    ["نسبة الاستقطاع", (v) => (v.dsr === null ? "—" : <span className={v.dsr <= v.dsrLimit ? "text-ok" : "text-err"}>{formatPercent(v.dsr * 100, { fractionDigits: 1 })} {v.dsr <= v.dsrLimit ? "✓" : "✕"}</span>)],
    ["أعدّه", (v) => v.preparedBy ?? "—"],
  ];

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex flex-1 flex-col gap-1">
          <h2 className="m-0 text-20 font-bold">الحلول ومقارنتها</h2>
          <p className="m-0 text-14 text-muted">
            القائم <bdi dir="ltr">{formatMoney(ctx.outstanding)}</bdi> ر.س · الدخل المتحقق {ctx.netIncome ? <><bdi dir="ltr">{formatMoney(ctx.netIncome)}</bdi> ر.س ({ctx.incomeSource})</> : "غير متوفر"} · حد الاستقطاع {formatPercent(ctx.dsrLimit * 100, { fractionDigits: 0 })} (افتراض)
          </p>
        </div>
        <Link href={`/cases/${ref}/solutions/negotiation`} className={buttonClasses({ variant: "secondary" })}>
          <Icon name="forum" size={18} />
          سلسلة العروض
        </Link>
        {canPrepare ? (
          <Link href={`/cases/${ref}/solutions/new`} className={buttonClasses({ variant: "primary" })}>
            <Icon name="add" size={18} />
            {hasDraft ? "متابعة المسودة" : "إصدار جديد"}
          </Link>
        ) : null}
      </div>

      {versions.length === 0 ? (
        <p className="m-0 rounded-lg border border-line bg-white p-5 text-14 text-muted">لا توجد إصدارات بعد.</p>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-line bg-white">
          <table className="w-full min-w-[640px] border-collapse text-14">
            <caption className="sr-only">مقارنة إصدارات الحل</caption>
            <thead>
              <tr className="bg-warm">
                <th scope="col" className="px-4 py-3 text-start font-semibold text-muted">البند</th>
                {versions.map((v) => (
                  <th key={v.version} scope="col" className="px-4 py-3 text-start">
                    <Link href={`/cases/${ref}/solutions/${v.version}`} className="flex flex-col gap-1 no-underline">
                      <span dir="ltr" className="font-mono text-16 font-bold text-ink">v{v.version}</span>
                      <Tag tone={STATUS[v.status]?.tone ?? "neutral"}>{STATUS[v.status]?.label ?? v.status}</Tag>
                    </Link>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map(([label, cell]) => (
                <tr key={label} className="border-t border-divider">
                  <th scope="row" className="px-4 py-2.5 text-start font-medium text-muted">{label}</th>
                  {versions.map((v) => (
                    <td key={v.version} className="px-4 py-2.5 tabular-nums">{cell(v)}</td>
                  ))}
                </tr>
              ))}
              <tr className="border-t border-divider">
                <th scope="row" className="px-4 py-2.5 text-start font-medium text-muted">ملاحظة</th>
                {versions.map((v) => (
                  <td key={v.version} className="px-4 py-2.5 text-13 text-muted">{v.returnReason ? `أُعيد: ${v.returnReason}` : "—"}</td>
                ))}
              </tr>
            </tbody>
          </table>
        </div>
      )}
      <p className="m-0 text-13 text-muted">
        <Icon name="info" size={16} /> كل إصدار سابق محفوظ ولا يُحذف. أي تعديل بعد الإرسال ينشئ إصداراً جديداً يمر بالسلسلة نفسها.
      </p>
    </div>
  );
}

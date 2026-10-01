import { Amount, Badge, QualityBadge } from "@/components/market/ui";
import { Icon } from "@/components/ui/Icon";
import { cn } from "@/lib/cn";
import type { CalcLine, TermsResult } from "@/lib/market/types";

const STATE: Record<string, { label: string; tone: "ok" | "info" | "warn" }> = {
  verified: { label: "موثّق", tone: "ok" },
  declared: { label: "حسب المالك", tone: "info" },
  estimated: { label: "تقدير", tone: "warn" },
};

const BORN: Record<string, string> = { buyer: "يتحمله المشتري", seller: "يتحمله صاحب العقار" };
const PAYEE: Record<string, string> = { seller: "لصاحب العقار", developer: "للمطور", financier: "لجهة التمويل", other: "تكاليف" };

function LineRow({ l, emphasize }: { l: CalcLine; emphasize?: boolean }) {
  const st = l.state ? STATE[l.state] : null;
  return (
    <div className={cn("flex flex-col gap-1 border-b border-divider py-3 last:border-b-0 sm:flex-row sm:items-start sm:justify-between sm:gap-6", emphasize && "rounded-md bg-rust-50/60 px-3")}>
      <div className="flex min-w-0 flex-col gap-0.5">
        <span className={cn("text-15", emphasize ? "font-bold" : "font-medium")}>{l.label}</span>
        <span className="flex flex-wrap gap-x-3 gap-y-0.5 text-12 text-muted">
          {l.payee && PAYEE[l.payee] ? <span>{PAYEE[l.payee]}</span> : null}
          {l.bornBy && BORN[l.bornBy] && l.timing !== "info" ? <span>{BORN[l.bornBy]}</span> : null}
        </span>
        {l.note ? <span className="text-13 leading-5 text-charcoal">{l.note}</span> : null}
      </div>
      <div className="flex flex-none flex-col items-start gap-1 sm:items-end">
        <Amount value={l.value} size={emphasize ? "lg" : "md"} strong={emphasize} unknown="غير معروف بعد" />
        {st ? <Badge tone={st.tone}>{st.label}</Badge> : null}
      </div>
    </div>
  );
}

/**
 * The figures of an opportunity or estimate, grouped by when they are paid. The amount due now, the future obligations and
 * the totals are separate; unknown figures read «غير معروف بعد», never 0. The commission line follows the approved policy.
 */
export function TermsBreakdown({ result, audience = "buyer", className }: { result: TermsResult; audience?: "buyer" | "owner" | "team"; className?: string }) {
  const now = result.lines.filter((l) => l.timing === "now" && l.key !== "due_now" && !(l.key === "seller_costs" && audience === "buyer"));
  const later = result.lines.filter((l) => l.timing === "later");
  const dueNow = result.lines.find((l) => l.key === "due_now");
  const total = result.lines.find((l) => l.key === "buyer_total");
  const sellerNet = result.lines.find((l) => l.key === "seller_net");
  return (
    <div className={cn("flex flex-col gap-4", className)}>
      <div className="flex flex-wrap items-center gap-2">
        <QualityBadge quality={result.quality} />
        {result.gap ? <Badge tone="err" icon="warning">فجوة تحتاج مراجعة</Badge> : null}
      </div>
      {!result.complete ? (
        <p className="m-0 flex gap-2 rounded-md border border-warn-line bg-warn-bg p-3 text-14 leading-6">
          <Icon name="pending" size={20} className="flex-none text-warn" />
          {result.qualityText}
        </p>
      ) : null}
      {audience !== "owner" && dueNow ? (
        <section aria-label="المطلوب الآن">
          <h3 className="m-0 mb-1 text-14 font-semibold text-muted">عند الإتمام</h3>
          <div>
            {now.map((l) => <LineRow key={l.key} l={l} />)}
            <LineRow l={dueNow} emphasize />
          </div>
        </section>
      ) : null}
      {audience !== "owner" && later.length ? (
        <section aria-label="لاحقًا">
          <h3 className="m-0 mb-1 text-14 font-semibold text-muted">بعد النقل</h3>
          <div>
            {later.map((l) => <LineRow key={l.key} l={l} />)}
            {total ? <LineRow l={total} emphasize /> : null}
          </div>
        </section>
      ) : audience !== "owner" && total ? (
        <LineRow l={total} emphasize />
      ) : null}
      {audience !== "buyer" ? (
        <section aria-label="ما يخص صاحب العقار">
          <h3 className="m-0 mb-1 text-14 font-semibold text-muted">ما يخص صاحب العقار</h3>
          <div>
            {result.lines.filter((l) => l.key === "owner_amount" || l.key === "seller_costs" || (l.key === "developer_arrears" && l.bornBy === "seller")).map((l) => <LineRow key={l.key} l={l} />)}
            {sellerNet ? <LineRow l={sellerNet} emphasize /> : null}
          </div>
        </section>
      ) : null}
      <div className="flex gap-2 rounded-md bg-subtle p-3 text-13 leading-6 text-charcoal">
        <Icon name="percent" size={18} className="flex-none text-muted" />
        <span>
          {result.commission.policyApproved && result.commission.amount !== null ? (
            <>
              عمولة رهون: <Amount value={result.commission.amount} size="sm" strong /> — {result.commission.text}
            </>
          ) : (
            result.commission.text
          )}
        </span>
      </div>
      {result.feeLines?.length ? (
        <section aria-label="توزيع الرسوم">
          <h3 className="m-0 mb-1 text-14 font-semibold text-muted">توزيع الرسوم (داخل الأرقام أعلاه)</h3>
          <div>{result.feeLines.map((l) => <LineRow key={l.key} l={l} />)}</div>
        </section>
      ) : null}
      {result.assumptions?.length ? (
        <section aria-label="الافتراضات" className="rounded-md border border-line p-3">
          <h3 className="m-0 mb-1 text-14 font-semibold">ما تفترضه النتيجة</h3>
          <ul className="m-0 flex list-none flex-col gap-1 p-0 text-13 leading-6 text-charcoal">
            {result.assumptions.map((a) => <li key={a}>• {a}</li>)}
          </ul>
        </section>
      ) : null}
      {result.notes.length ? (
        <ul className="m-0 flex list-none flex-col gap-1 p-0 text-13 leading-6 text-muted">
          {result.notes.map((n) => (
            <li key={n} className="flex gap-1.5">
              <Icon name="info" size={16} className="mt-0.5 flex-none" />
              {n}
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}

import { Amount } from "@/components/market/ui";
import { Icon } from "@/components/ui/Icon";
import { cn } from "@/lib/cn";
import type { Fit, MatchExplanation, PaymentItem } from "@/lib/market/types";

const TONE = {
  fits: { box: "border-ok-line bg-ok-bg", text: "text-ok", icon: "check_circle" },
  does_not_fit: { box: "border-warn-line bg-warn-bg", text: "text-warn", icon: "do_not_disturb_on" },
  incomplete: { box: "border-line bg-subtle", text: "text-charcoal", icon: "help" },
} as const;

/**
 * The server's affordability answer for a declared capacity, with its reasons. «incomplete» says what is unknown instead of
 * guessing; nothing here is a financing approval. `compact` keeps the card short (two reasons, two limits).
 */
export function FitSummary({ fit, match, compact, title }: { fit: Fit; match?: MatchExplanation | null; compact?: boolean; title?: string }) {
  const t = TONE[fit.outcome] ?? TONE.incomplete;
  const take = (xs: string[], k: number) => (compact ? xs.slice(0, k) : xs);
  const lines = [
    ...take(fit.limits, 2).map((x) => ({ x, icon: "remove", cls: "text-warn" })),
    ...take(fit.unknowns, 1).map((x) => ({ x, icon: "help", cls: "text-muted" })),
    ...take(fit.reasons, compact ? 2 : 99).map((x) => ({ x, icon: "check", cls: "text-ok" })),
  ];
  return (
    <div className={cn("flex flex-col gap-1.5 rounded-md border p-2.5 text-12 leading-5", t.box)} role={compact ? undefined : "status"}>
      <strong className={cn("flex items-center gap-1 text-13", t.text)}>
        <Icon name={t.icon} size={16} />
        {title ?? fit.headline}
      </strong>
      <ul className="m-0 flex list-none flex-col gap-0.5 p-0">
        {lines.map(({ x, icon, cls }) => (
          <li key={x} className="flex gap-1">
            <Icon name={icon} size={14} className={cn("mt-0.5 flex-none", cls)} />
            <span>{x}</span>
          </li>
        ))}
      </ul>
      {match && (match.preferences.length || match.notMet.length || match.eligibility.length) ? (
        <ul className="m-0 flex list-none flex-col gap-0.5 border-t border-divider p-0 pt-1.5">
          {take(match.eligibility, 1).map((x) => <li key={x} className="flex gap-1"><Icon name="filter_alt" size={14} className="mt-0.5 flex-none text-muted" />{x}</li>)}
          {take(match.preferences, 2).map((x) => <li key={x} className="flex gap-1"><Icon name="thumb_up" size={14} className="mt-0.5 flex-none text-ok" />{x}</li>)}
          {take(match.notMet, 1).map((x) => <li key={x} className="flex gap-1 text-muted"><Icon name="info" size={14} className="mt-0.5 flex-none" />{x}</li>)}
        </ul>
      ) : null}
      {!compact && fit.caveats.length ? (
        <ul className="m-0 flex list-none flex-col gap-0.5 border-t border-divider p-0 pt-1.5 text-muted">
          {fit.caveats.map((c) => <li key={c} className="flex gap-1"><Icon name="info" size={14} className="mt-0.5 flex-none" />{c}</li>)}
        </ul>
      ) : null}
    </div>
  );
}

/** The next material payments as recorded (no invented dates: the model has no next-installment date). */
export function NextPayments({ items, className }: { items: PaymentItem[]; className?: string }) {
  return (
    <ol className={cn("m-0 flex list-none flex-col p-0", className)}>
      {items.map((p) => (
        <li key={p.key} className="flex flex-col gap-0.5 border-b border-divider py-2 last:border-b-0 sm:flex-row sm:items-baseline sm:justify-between sm:gap-4">
          <span className="flex min-w-0 flex-col">
            <span className="text-14 font-semibold">{p.label}</span>
            {p.note ? <span className="text-12 text-muted">{p.note}</span> : null}
          </span>
          {p.when === "financing" ? (
            <span className="text-13 text-info">تحدده جهة تمويلك</span>
          ) : (
            <Amount value={p.amount} size="sm" strong unknown="غير معروف بعد" />
          )}
        </li>
      ))}
    </ol>
  );
}

import { Fragment } from "react";

/**
 * Server-composed Arabic strings carry LTR tokens (dates, times, amounts, masked IDs, references, phones):
 * «2026-10-02 14:21 · رمز تحقق إلى +966 5• ••• ••81». Inside an RTL paragraph those runs reorder
 * (`1•••••••42` → `42•••••••1`, the time jumps before the date). This splits the text into LTR runs and
 * isolates each in <bdi dir="ltr">, keeping the surrounding Arabic untouched.
 */
const LTR_RUN = /[A-Za-z0-9+•][A-Za-z0-9•+\-:.,/_ ]*[A-Za-z0-9•]|[A-Za-z0-9•]/g;

export function BidiText({ text, className }: { text: string | null | undefined; className?: string }) {
  if (!text) return null;
  const parts: Array<{ ltr: boolean; s: string }> = [];
  let last = 0;
  for (const m of text.matchAll(LTR_RUN)) {
    const i = m.index ?? 0;
    if (i > last) parts.push({ ltr: false, s: text.slice(last, i) });
    parts.push({ ltr: true, s: m[0] });
    last = i + m[0].length;
  }
  if (last < text.length) parts.push({ ltr: false, s: text.slice(last) });
  return (
    <span className={className}>
      {parts.map((p, i) =>
        p.ltr ? (
          <bdi key={i} dir="ltr">
            {p.s}
          </bdi>
        ) : (
          <Fragment key={i}>{p.s}</Fragment>
        ),
      )}
    </span>
  );
}

/** «YYYY-MM-DD» for today in Riyadh (business dates are Riyadh dates; the API rejects future receipt dates). */
export function todayRiyadh(now: Date = new Date()): string {
  return new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Riyadh", year: "numeric", month: "2-digit", day: "2-digit" }).format(now);
}

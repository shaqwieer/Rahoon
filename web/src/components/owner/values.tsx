"use client";

import { Fragment } from "react";
import { cn } from "@/lib/cn";
import { formatMoney, formatNumber } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";
import { ownerCopy } from "./copy";

/** Owner copy for the current locale (client components). */
export function useOwnerCopy() {
  const { locale } = useI18n();
  return ownerCopy(locale);
}

/**
 * Amount with the owner unit: «15,074.52 ريال» (ar) / «SAR 15,074.52» (en). The number is always an LTR
 * isolate; the unit stays in the reading direction.
 */
export function Amount({ value, fractionDigits = 2, className, numberClassName, unitClassName }: {
  value: number | null | undefined;
  fractionDigits?: 0 | 2;
  className?: string;
  numberClassName?: string;
  unitClassName?: string;
}) {
  const { locale, numerals } = useI18n();
  const c = ownerCopy(locale);
  const n = fractionDigits === 0 ? formatNumber(value, 0, { numerals }) : formatMoney(value, { numerals });
  const num = (
    <bdi dir="ltr" className={cn("tabular-nums", numberClassName)}>
      {n}
    </bdi>
  );
  const unit = <span className={unitClassName}>{c.unit}</span>;
  return (
    <span className={cn("whitespace-nowrap", className)}>
      {locale === "en" ? (
        <>
          {unit} {num}
        </>
      ) : (
        <>
          {num} {unit}
        </>
      )}
    </span>
  );
}

// LTR runs inside server-authored Arabic text: references (AGR-2026-004172-01), dates with optional time,
// amounts (15,074.52). Trailing punctuation stays outside the isolate.
const LTR_TOKEN = /((?:[A-Z]{2,}-)?\d[\d,.:/-]*(?:\s\d{1,2}:\d{2})?)/g;

/**
 * Server-authored Arabic text (titles, bodies, statuses). Each number/date/reference becomes a
 * `<bdi dir="ltr">`; in the English UI the whole run is marked `lang="ar" dir="rtl"` so it doesn't reorder.
 */
export function ServerText({ text, className, as: Tag = "span" }: { text: string | null | undefined; className?: string; as?: "span" | "p" | "strong" | "div" }) {
  const { locale } = useI18n();
  if (!text) return null;
  const parts = text.split(LTR_TOKEN);
  const content = parts.map((p, i) => {
    if (i % 2 === 0) return <Fragment key={i}>{p}</Fragment>;
    const m = /^(.*?)([.,:-]*)$/.exec(p);
    const core = m?.[1] ?? p;
    const tail = m?.[2] ?? "";
    return (
      <Fragment key={i}>
        <bdi dir="ltr">{core}</bdi>
        {tail}
      </Fragment>
    );
  });
  return (
    <Tag className={className} {...(locale === "ar" ? {} : { lang: "ar", dir: "rtl" })}>
      {content}
    </Tag>
  );
}

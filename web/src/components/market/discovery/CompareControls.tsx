"use client";

import Link from "next/link";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Icon } from "@/components/ui/Icon";
import { cn } from "@/lib/cn";
import { COMPARE_MAX, useCompare } from "@/lib/market/compare";

/** «قارن» on a card or a detail page: adds the opportunity to the comparison (four at most). */
export function CompareToggle({ reference, className }: { reference: string; className?: string }) {
  const c = useCompare();
  const on = c.has(reference);
  const disabled = !on && c.full;
  return (
    <button type="button" onClick={() => c.toggle(reference)} aria-pressed={on} disabled={disabled}
      title={disabled ? `يمكن مقارنة ${COMPARE_MAX} فرص على الأكثر` : undefined}
      className={cn("relative z-10 inline-flex min-h-9 items-center gap-1 rounded-pill border px-3 text-13 font-semibold transition-colors",
        on ? "border-ink bg-ink text-white" : "border-line-strong bg-white text-charcoal hover:bg-subtle", disabled && "cursor-not-allowed opacity-50", className)}>
      <Icon name={on ? "check" : "compare_arrows"} size={16} />
      {on ? "في المقارنة" : "قارن"}
    </button>
  );
}

/** Bottom bar while something is picked: how many, open the comparison, clear. */
export function CompareTray() {
  const c = useCompare();
  if (c.list.length === 0) return null;
  return (
    <div role="region" aria-label="المقارنة" className="fixed inset-x-0 bottom-0 z-40 border-t border-line bg-white/95 shadow-3 backdrop-blur pb-[env(safe-area-inset-bottom)]">
      <div className="mx-auto flex max-w-[1440px] flex-wrap items-center justify-between gap-2 px-4 py-2.5 md:px-10">
        <span className="flex items-center gap-2 text-14">
          <Icon name="compare_arrows" size={20} className="text-rust" />
          <strong>{c.list.length}</strong> من {COMPARE_MAX} للمقارنة
        </span>
        <span className="flex items-center gap-2">
          <button type="button" onClick={c.clear} className="min-h-10 px-2 text-13 font-semibold text-muted underline">مسح</button>
          <Link href={`/compare?refs=${c.list.join(",")}`}
            className={buttonClasses({ variant: "primary", size: "md", className: c.list.length < 2 ? "pointer-events-none opacity-60" : "" })}
            aria-disabled={c.list.length < 2}>
            قارن الآن
          </Link>
        </span>
      </div>
    </div>
  );
}

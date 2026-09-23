import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { Icon } from "./Icon";

export interface KpiTileProps {
  label: ReactNode;
  /** Pre-formatted number (Western digits); rendered LTR with tabular numerals. */
  value: string;
  unit?: ReactNode;
  icon?: string;
  /** Icon colour (charcoal by default; rust for «بحاجة لإجرائك», error for overdue). */
  iconTone?: "charcoal" | "rust" | "err" | "ok" | "warn" | "info";
  /** Secondary line: trend, source, «المصدر: نظام التمويل · 06:00». */
  sub?: ReactNode;
  /** Optional delta with its good/bad direction spelled out in text (never colour only). */
  delta?: { text: ReactNode; good: boolean };
  href?: string;
  className?: string;
}

const ICON_TONE = { charcoal: "text-charcoal", rust: "text-rust", err: "text-err", ok: "text-ok", warn: "text-warn", info: "text-info" };

/** Portfolio / report KPI tile (anchor screens): label, 32/44 value + unit, sub line. */
export function KpiTile({ label, value, unit, icon, iconTone = "charcoal", sub, delta, href, className }: KpiTileProps) {
  const body = (
    <>
      <span className="flex items-center gap-1.5 text-14 text-muted">
        {icon ? <Icon name={icon} size={18} className={ICON_TONE[iconTone]} /> : null}
        {label}
      </span>
      <span className="text-32 leading-[44px] font-bold text-ink">
        <bdi dir="ltr" className="tabular-nums">
          {value}
        </bdi>
        {unit ? <span className="ms-1.5 text-16 font-medium">{unit}</span> : null}
      </span>
      {delta ? (
        <span className={cn("flex items-center gap-1 text-13 font-semibold", delta.good ? "text-ok" : "text-err")}>
          <Icon name={delta.good ? "trending_up" : "trending_down"} size={16} />
          {delta.text}
        </span>
      ) : null}
      {sub ? <span className="text-13 text-muted">{sub}</span> : null}
    </>
  );
  const classes = cn("flex flex-col gap-1.5 rounded-md border border-line bg-white px-5 py-[18px] no-underline", href && "hover:border-line-strong", className);
  return href ? (
    <a href={href} className={cn(classes, "text-ink hover:text-ink")}>
      {body}
    </a>
  ) : (
    <div className={classes}>{body}</div>
  );
}

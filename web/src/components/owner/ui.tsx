import Link from "next/link";
import type { InputHTMLAttributes, ReactNode } from "react";
import { Icon } from "@/components/ui/Icon";
import { cn } from "@/lib/cn";
import type { OwnerTone } from "./types";

/*
 * Owner (B6) presentational pieces — hook-free so both Server and Client Components can use them.
 * Sizes follow the 390px artboards: 12px card radius, 16–18px reading text, 48–54px touch targets.
 */

export function Card({ children, className, as: Tag = "div", accent, ...rest }: {
  children: ReactNode;
  className?: string;
  as?: "div" | "article" | "section" | "li";
  /** 4px orange top edge (next step / next installment). */
  accent?: boolean;
} & { "aria-labelledby"?: string; id?: string }) {
  return (
    <Tag className={cn("flex flex-col rounded-[12px] border border-line bg-white", accent && "border-t-4 border-t-orange", className)} {...rest}>
      {children}
    </Tag>
  );
}

/** Info callout (icon + text) — blue tint as drawn in D03/D10. */
export function InfoNote({ children, icon = "info", tone = "info", className }: { children: ReactNode; icon?: string; tone?: "info" | "neutral"; className?: string }) {
  return (
    <div
      className={cn(
        "flex gap-2.5 rounded-[12px] border px-3.5 py-3 text-15 leading-6",
        tone === "info" ? "border-info-line bg-info-bg" : "border-line bg-subtle",
        className,
      )}
    >
      <Icon name={icon} size={22} className={tone === "info" ? "text-info" : "text-muted"} />
      <div className="min-w-0 flex-1">{children}</div>
    </div>
  );
}

/** 5-segment journey bar (done = charcoal, current = orange, todo = track). Order follows reading direction. */
export function SegmentProgress({ current, total, label }: { current: number; total: number; label: string }) {
  return (
    <div role="progressbar" aria-valuemin={1} aria-valuemax={total} aria-valuenow={current} aria-valuetext={label} className="grid gap-1" style={{ gridTemplateColumns: `repeat(${total}, minmax(0, 1fr))` }}>
      {Array.from({ length: total }, (_, i) => (
        <span key={i} className={cn("h-1.5 rounded-[3px]", i + 1 < current ? "bg-charcoal" : i + 1 === current ? "bg-orange" : "bg-track")} />
      ))}
    </div>
  );
}

export const TONE_TEXT: Record<OwnerTone, string> = { ok: "text-ok", warn: "text-warn", err: "text-err", info: "text-info", neutral: "text-muted" };
export const TONE_ICON: Record<OwnerTone, string> = { ok: "check_circle", warn: "hourglass_top", err: "undo", info: "upload_file", neutral: "schedule" };
export function toTone(t: string | null | undefined): OwnerTone {
  return t === "ok" || t === "warn" || t === "err" || t === "info" ? t : "neutral";
}

/**
 * 52px option card with a real checkbox/radio (D08, D11, D13). The whole card is the label; the native input
 * stays keyboard-focusable and draws the focus ring on the card.
 */
export function ChoiceCard({ type, label, icon, checked, className, ...input }: {
  type: "checkbox" | "radio";
  label: ReactNode;
  icon?: string;
  checked: boolean;
  className?: string;
} & Omit<InputHTMLAttributes<HTMLInputElement>, "type" | "checked" | "className">) {
  return (
    <label
      className={cn(
        "relative flex min-h-[52px] cursor-pointer items-center gap-3 rounded-[8px] px-3.5 py-2 text-16 leading-6 has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-ink",
        checked ? "border-2 border-ink bg-rust-50 px-[13px] font-semibold" : "border border-line-strong bg-white",
        className,
      )}
    >
      <input type={type} checked={checked} className="absolute inset-0 m-0 cursor-pointer opacity-0" {...input} />
      {type === "checkbox" ? (
        <span aria-hidden="true" className={cn("flex size-[22px] flex-none items-center justify-center rounded-xs", checked ? "bg-ink text-white" : "border border-line-strong bg-white text-transparent")}>
          <Icon name="check" size={18} />
        </span>
      ) : icon ? null : (
        <span aria-hidden="true" className={cn("box-border size-[18px] flex-none rounded-full", checked ? "border-[6px] border-ink" : "border border-line-strong bg-white")} />
      )}
      {icon ? <Icon name={icon} size={22} className={checked ? "text-ink" : "text-muted"} /> : null}
      <span className="flex-1">{label}</span>
      {type === "radio" && icon && checked ? <Icon name="check_circle" size={20} className="text-ink" /> : null}
    </label>
  );
}

/** Text link with a 48px touch target (owner links sit alone on a line). */
export function TouchLink({ href, children, className, icon }: { href: string; children: ReactNode; className?: string; icon?: string }) {
  return (
    <Link href={href} className={cn("inline-flex min-h-12 items-center gap-1.5 self-start text-15 font-semibold", className)}>
      {icon ? <Icon name={icon} size={20} /> : null}
      {children}
    </Link>
  );
}

/** Reading column for single-purpose screens on tablet/desktop (the shell caps mobile at 560 already). */
export function Column({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cn("mx-auto flex w-full max-w-[560px] flex-col gap-3", className)}>{children}</div>;
}

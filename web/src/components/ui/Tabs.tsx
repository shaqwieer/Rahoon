"use client";

import Link from "next/link";
import { useRef, type KeyboardEvent, type ReactNode } from "react";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";

export interface TabDef {
  key: string;
  label: ReactNode;
  /** Optional count badge (e.g. «المستندات 1»). */
  count?: number;
  /** Badge tone: neutral grey, or error for items needing attention. */
  countTone?: "neutral" | "err";
  /** Route tab (CaseHeader, settings); otherwise `onChange` switches in place. */
  href?: string;
  /** id of the controlled tabpanel (in-place tabs). */
  panelId?: string;
  disabled?: boolean;
}

export interface TabsProps {
  label: string;
  tabs: TabDef[];
  active: string;
  onChange?: (key: string) => void;
  /** `page` (44px, bordered strip) or `compact` (40px, popovers). */
  size?: "page" | "compact";
  /** Removes the bottom border (when the parent draws it, e.g. CaseHeader). */
  bare?: boolean;
  className?: string;
}

/**
 * role=tablist with roving focus. Arrow keys follow the reading direction (ArrowLeft = next in RTL);
 * Home/End jump. In-place tabs activate on arrow; route tabs move focus and activate on Enter.
 */
export function Tabs({ label, tabs, active, onChange, size = "page", bare, className }: TabsProps) {
  const { dir } = useI18n();
  const refs = useRef<Array<HTMLElement | null>>([]);

  const onKeyDown = (e: KeyboardEvent<HTMLElement>, index: number) => {
    const enabled = tabs.map((t, i) => (t.disabled ? -1 : i)).filter((i) => i >= 0);
    const pos = enabled.indexOf(index);
    const nextKey = dir === "rtl" ? "ArrowLeft" : "ArrowRight";
    const prevKey = dir === "rtl" ? "ArrowRight" : "ArrowLeft";
    let target: number | undefined;
    if (e.key === nextKey) target = enabled[(pos + 1) % enabled.length];
    else if (e.key === prevKey) target = enabled[(pos - 1 + enabled.length) % enabled.length];
    else if (e.key === "Home") target = enabled[0];
    else if (e.key === "End") target = enabled[enabled.length - 1];
    if (target === undefined) return;
    e.preventDefault();
    refs.current[target]?.focus();
    const tab = tabs[target];
    if (!tab.href) onChange?.(tab.key);
  };

  return (
    <div
      role="tablist"
      aria-label={label}
      className={cn("flex overflow-x-auto [scrollbar-width:thin]", !bare && "border-b border-line", className)}
    >
      {tabs.map((tab, i) => {
        const selected = tab.key === active;
        const classes = cn(
          "inline-flex flex-none items-center gap-1.5 border-0 bg-transparent px-3.5 text-14 whitespace-nowrap no-underline outline-offset-[-2px]",
          size === "page" ? "min-h-11" : "min-h-10",
          selected ? "bar-bottom font-bold text-ink hover:text-ink" : "font-medium text-muted hover:text-ink",
          tab.disabled && "cursor-not-allowed opacity-60",
        );
        const badge =
          tab.count !== undefined ? (
            <bdi
              className={cn(
                "rounded-pill px-1.5 text-11 leading-[18px] font-semibold",
                tab.countTone === "err" ? "bg-err-bg text-err" : "bg-subtle text-muted",
              )}
            >
              {tab.count}
            </bdi>
          ) : null;
        const common = {
          role: "tab" as const,
          "aria-selected": selected,
          "aria-controls": tab.panelId,
          "aria-disabled": tab.disabled || undefined,
          tabIndex: selected ? 0 : -1,
          className: classes,
          onKeyDown: (e: KeyboardEvent<HTMLElement>) => onKeyDown(e, i),
        };
        if (tab.href && !tab.disabled) {
          return (
            <Link
              key={tab.key}
              href={tab.href}
              ref={(el) => {
                refs.current[i] = el;
              }}
              aria-current={selected ? "page" : undefined}
              {...common}
            >
              {tab.label}
              {badge}
            </Link>
          );
        }
        return (
          <button
            key={tab.key}
            type="button"
            ref={(el) => {
              refs.current[i] = el;
            }}
            disabled={tab.disabled}
            onClick={() => onChange?.(tab.key)}
            {...common}
          >
            {tab.label}
            {badge}
          </button>
        );
      })}
    </div>
  );
}

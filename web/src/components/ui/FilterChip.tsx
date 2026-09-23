"use client";

import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";
import { Icon } from "./Icon";

export interface FilterChipProps {
  label: ReactNode;
  /** `active` = applied filter (ink, removable); `menu` = opens a filter picker; `action` = dashed (e.g. «حفظ العرض»). */
  variant?: "active" | "menu" | "action";
  icon?: string;
  onClick?: () => void;
  /** Active chips: removes the filter. The remove button has its own accessible name. */
  onRemove?: () => void;
  expanded?: boolean;
  className?: string;
}

/** C12 filter chips (min-h 36, pill). */
export function FilterChip({ label, variant = "menu", icon, onClick, onRemove, expanded, className }: FilterChipProps) {
  const { t } = useI18n();
  if (variant === "active") {
    return (
      <span className={cn("inline-flex min-h-9 items-center gap-1 rounded-pill bg-ink ps-3 pe-1 text-13 font-semibold text-white", className)}>
        <button type="button" onClick={onClick} className="bg-transparent text-white">
          {label}
        </button>
        {onRemove ? (
          <button
            type="button"
            aria-label={`${t.common.remove}: ${typeof label === "string" ? label : ""}`.trim()}
            onClick={onRemove}
            className="surface-dark inline-flex size-7 items-center justify-center rounded-full bg-transparent text-white hover:bg-inv-raised"
          >
            <Icon name="close" size={16} />
          </button>
        ) : null}
      </span>
    );
  }
  return (
    <button
      type="button"
      onClick={onClick}
      aria-expanded={variant === "menu" ? Boolean(expanded) : undefined}
      aria-haspopup={variant === "menu" ? "true" : undefined}
      className={cn(
        "inline-flex min-h-9 items-center gap-1.5 rounded-pill bg-white px-3 text-13 font-semibold",
        variant === "action" ? "border border-dashed border-line-strong text-rust" : "border border-line-strong text-ink hover:bg-subtle",
        className,
      )}
    >
      {icon ? <Icon name={icon} size={16} /> : null}
      {label}
      {variant === "menu" ? <Icon name="expand_more" size={16} /> : null}
    </button>
  );
}

"use client";

import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { formatNumber } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";
import { IconButton } from "./IconButton";

export interface PaginationProps {
  /** 1-based page. */
  page: number;
  pageSize: number;
  total: number;
  onPageChange?: (page: number) => void;
  /** Route-based paging: returns the URL for a page (preferred for server lists). */
  hrefFor?: (page: number) => string;
  /** Appended after the range, e.g. «مرتبة حسب أقرب مهلة». */
  note?: ReactNode;
  className?: string;
}

/** «عرض 1–10 من 38» + previous/next (chevrons mirror in LTR). */
export function Pagination({ page, pageSize, total, onPageChange, hrefFor, note, className }: PaginationProps) {
  const { t, numerals } = useI18n();
  const pages = Math.max(1, Math.ceil(total / pageSize));
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(total, page * pageSize);
  const hasPrev = page > 1;
  const hasNext = page < pages;
  const nav = (target: number, enabled: boolean, label: string, icon: string) =>
    hrefFor && enabled ? (
      <IconButton label={label} icon={icon} mirror variant="outline" href={hrefFor(target)} iconSize={18} />
    ) : (
      <IconButton label={label} icon={icon} mirror variant="outline" disabled={!enabled} onClick={() => onPageChange?.(target)} iconSize={18} className={enabled ? "border-line-strong" : undefined} />
    );
  return (
    <nav aria-label={t.table.pagination} className={cn("flex flex-wrap items-center justify-between gap-3 text-14 text-muted", className)}>
      <span aria-live="polite">
        {t.table.showingPrefix}{" "}
        <bdi dir="ltr">
          {formatNumber(from, 0, { numerals })}–{formatNumber(to, 0, { numerals })}
        </bdi>{" "}
        {t.common.of} <bdi dir="ltr">{formatNumber(total, 0, { numerals })}</bdi>
        {note ? <> · {note}</> : null}
      </span>
      <div className="flex gap-1.5">
        {nav(page - 1, hasPrev, t.table.prevPage, "chevron_right")}
        {nav(page + 1, hasNext, t.table.nextPage, "chevron_left")}
      </div>
    </nav>
  );
}

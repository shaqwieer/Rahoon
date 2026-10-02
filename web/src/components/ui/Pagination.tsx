import Link from "next/link";
import { cn } from "@/lib/cn";
import { Icon } from "./Icon";

/** 1 … 4 5 6 … 12: first, last, the current page and its neighbours. */
export function pageWindow(page: number, pages: number): (number | "gap")[] {
  const keep = new Set([1, pages, page - 1, page, page + 1].filter((p) => p >= 1 && p <= pages));
  if (page <= 3) [2, 3, 4].forEach((p) => p <= pages && keep.add(p));
  if (page >= pages - 2) [pages - 3, pages - 2, pages - 1].forEach((p) => p >= 1 && keep.add(p));
  const list = [...keep].sort((a, b) => a - b);
  const out: (number | "gap")[] = [];
  list.forEach((p, i) => {
    if (i > 0 && p - list[i - 1] > 1) out.push("gap");
    out.push(p);
  });
  return out;
}

const cell = "inline-flex min-h-10 min-w-10 items-center justify-center gap-1 rounded-sm border px-2.5 text-14 font-semibold tabular-nums transition-colors";

/**
 * Numbered pages with «السابق / التالي» and «عرض 13–24 من 40». Works with links (`hrefFor`, server pages) or with a callback
 * (`onPage`, client lists). Renders nothing for a single page.
 */
export function Pagination({ page, pages, total, pageSize, hrefFor, onPage, disabled, label = "الصفحات", className }: {
  page: number;
  pages: number;
  total?: number;
  pageSize?: number;
  hrefFor?: (page: number) => string;
  onPage?: (page: number) => void;
  disabled?: boolean;
  label?: string;
  className?: string;
}) {
  if (pages <= 1) return null;
  const item = (p: number, content: React.ReactNode, opts: { current?: boolean; aria?: string; off?: boolean } = {}) => {
    const cls = cn(cell, opts.current ? "border-ink bg-ink text-white" : "border-line-strong bg-white text-ink hover:bg-subtle", opts.off && "pointer-events-none opacity-40");
    if (opts.current) return <span aria-current="page" className={cls}>{content}</span>;
    if (opts.off) return <span aria-disabled="true" aria-label={opts.aria} className={cls}>{content}</span>;
    if (hrefFor) return <Link href={hrefFor(p)} aria-label={opts.aria} className={cn(cls, "no-underline")}>{content}</Link>;
    return <button type="button" onClick={() => onPage?.(p)} disabled={disabled} aria-label={opts.aria} className={cls}>{content}</button>;
  };
  const from = total !== undefined && pageSize ? (page - 1) * pageSize + 1 : null;
  const to = total !== undefined && pageSize ? Math.min(total, page * pageSize) : null;
  return (
    <nav aria-label={label} className={cn("flex flex-col items-center justify-between gap-3 sm:flex-row", className)}>
      {from !== null && to !== null ? (
        <span className="text-13 text-muted">عرض <bdi dir="ltr" className="tabular-nums">{from}–{to}</bdi> من {total}</span>
      ) : <span />}
      <ul className="m-0 flex list-none flex-wrap items-center justify-center gap-1 p-0">
        <li>{item(page - 1, <><Icon name="chevron_right" size={18} /><span className="max-sm:sr-only">السابق</span></>, { aria: "الصفحة السابقة", off: page <= 1 })}</li>
        {pageWindow(page, pages).map((p, i) => (
          <li key={p === "gap" ? `g${i}` : p}>
            {p === "gap" ? <span className="inline-flex min-h-10 min-w-6 items-center justify-center text-muted">…</span> : item(p, p, { current: p === page, aria: `الصفحة ${p}` })}
          </li>
        ))}
        <li>{item(page + 1, <><span className="max-sm:sr-only">التالي</span><Icon name="chevron_left" size={18} /></>, { aria: "الصفحة التالية", off: page >= pages })}</li>
      </ul>
    </nav>
  );
}

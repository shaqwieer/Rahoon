import Link from "next/link";
import type { ReactNode } from "react";
import { cn } from "@/lib/cn";

export function TeamHeader({ title, sub, actions, back }: { title: ReactNode; sub?: ReactNode; actions?: ReactNode; back?: { href: string; label: string } }) {
  return (
    <header className="mb-6 flex flex-col gap-2">
      {back ? <Link href={back.href} className="text-14 font-semibold">← {back.label}</Link> : null}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="m-0 text-24 leading-9 font-bold md:text-28">{title}</h1>
        {actions}
      </div>
      {sub ? <p className="m-0 text-15 text-muted">{sub}</p> : null}
    </header>
  );
}

/** Queue tabs as links (server-rendered, shareable URLs). */
export function QueueTabs({ tabs, current }: { tabs: { key: string; label: string; href: string; count?: number }[]; current: string }) {
  return (
    <nav aria-label="القوائم" className="-mx-4 mb-4 overflow-x-auto px-4 md:mx-0 md:px-0">
      <ul className="m-0 flex min-w-max list-none gap-1 border-b border-line p-0">
        {tabs.map((t) => (
          <li key={t.key}>
            <Link href={t.href} aria-current={t.key === current ? "page" : undefined}
              className={cn("inline-flex min-h-11 items-center gap-1.5 px-3 text-14 no-underline", t.key === current ? "bar-bottom font-bold text-rust-700" : "text-charcoal hover:text-ink")}>
              {t.label}
              {t.count !== undefined ? <span className="rounded-pill bg-subtle px-2 text-12">{t.count}</span> : null}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}

/** Responsive list: a table on desktop, stacked cards on phones. */
export function TeamTable({ head, rows, empty }: { head: string[]; rows: { key: string; href: string; cells: ReactNode[] }[]; empty: string }) {
  if (rows.length === 0) return <p className="m-0 rounded-lg border border-dashed border-line-strong bg-white p-6 text-center text-15 text-muted">{empty}</p>;
  return (
    <>
      <div className="hidden overflow-hidden rounded-lg border border-line bg-white md:block">
        <table className="w-full border-collapse text-14">
          <thead className="bg-subtle text-13 text-muted">
            <tr>{head.map((h) => <th key={h} scope="col" className="px-4 py-3 text-start font-semibold">{h}</th>)}</tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.key} className="border-t border-divider hover:bg-warm">
                {r.cells.map((c, i) => (
                  <td key={i} className="px-4 py-3 align-middle">{i === 0 ? <Link href={r.href} className="font-semibold">{c}</Link> : c}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <ul className="m-0 flex list-none flex-col gap-2 p-0 md:hidden">
        {rows.map((r) => (
          <li key={r.key}>
            <Link href={r.href} className="flex flex-col gap-1.5 rounded-md border border-line bg-white p-3 text-ink no-underline">
              {r.cells.map((c, i) => (
                <span key={i} className={i === 0 ? "text-15 font-bold" : "flex gap-2 text-13"}>
                  {i > 0 ? <span className="text-muted">{head[i]}:</span> : null}
                  {c}
                </span>
              ))}
            </Link>
          </li>
        ))}
      </ul>
    </>
  );
}

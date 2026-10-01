"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { FitSummary } from "@/components/market/discovery/FitSummary";
import { Amount, Badge, DemoBadge } from "@/components/market/ui";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Icon } from "@/components/ui/Icon";
import { useCompare } from "@/lib/market/compare";
import type { CompareCell, CompareItem, CompareResult } from "@/lib/market/types";

const ROWS: { key: string; label: string; money?: boolean }[] = [
  { key: "dueNow", label: "المطلوب منك الآن", money: true },
  { key: "buyerTotal", label: "إجمالي الالتزام / سعر الشراء", money: true },
  { key: "futureBalance", label: "الرصيد المستقبلي للمطور", money: true },
  { key: "installment", label: "القسط ودوريته", money: true },
  { key: "monthlyEquivalent", label: "مكافئ القسط الشهري", money: true },
  { key: "extraPayments", label: "دفعات إضافية", money: true },
  { key: "remainingMonths", label: "المدة المتبقية" },
  { key: "verification", label: "ما راجعه الفريق" },
  { key: "location", label: "الموقع" },
  { key: "delivery", label: "الحالة والتسليم" },
  { key: "financing", label: "طريقة الشراء" },
];

function CellView({ cell, money }: { cell: CompareCell | undefined; money?: boolean }) {
  if (!cell) return <span className="text-muted">—</span>;
  if (cell.state === "not_applicable")
    return (
      <span className="flex flex-col gap-0.5">
        <Badge tone="neutral">لا ينطبق</Badge>
        {cell.text && cell.text !== "لا ينطبق" ? <span className="text-12 text-muted">{cell.text}</span> : null}
      </span>
    );
  if (cell.state === "unknown")
    return (
      <span className="flex flex-col gap-0.5">
        <Badge tone="warn" icon="help">غير معروف بعد</Badge>
        {cell.text && cell.text !== "غير معروف بعد" ? <span className="text-12 text-muted">{cell.text}</span> : null}
      </span>
    );
  return (
    <span className="flex flex-col gap-0.5">
      {cell.value !== null ? (money ? <Amount value={cell.value} size="sm" strong /> : <strong className="text-14 tabular-nums">{cell.value}</strong>) : null}
      {cell.text ? <span className={cell.value === null ? "text-14" : "text-12 text-muted"}>{cell.text}</span> : null}
    </span>
  );
}

/**
 * Up to four opportunities side by side. A path that doesn't have a figure says «لا ينطبق», an unknown figure says so — never a
 * comparable zero. An opportunity no longer published shows only that it is unavailable.
 */
export function CompareView({ result, refs }: { result: CompareResult; refs: string[] }) {
  const router = useRouter();
  const c = useCompare();
  // The URL is what was shared; keep this browser's selection in step with it.
  useEffect(() => {
    if (refs.length && refs.join(",") !== c.list.join(",")) c.set(refs);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- sync once per URL
  }, [refs.join(",")]);

  const remove = (ref: string) => {
    const next = refs.filter((r) => r !== ref);
    c.set(next);
    router.replace(next.length ? `/compare?refs=${next.join(",")}` : "/compare");
  };

  if (result.items.length === 0)
    return (
      <div className="flex flex-col items-start gap-3 rounded-lg border border-dashed border-line-strong bg-white p-6">
        <strong className="text-17">لم تختر فرصًا للمقارنة بعد</strong>
        <span className="text-14 text-muted">اضغط «قارن» على بطاقات الفرص (حتى أربع)، ثم «قارن الآن».</span>
        <Link href="/opportunities" className={buttonClasses({ variant: "primary", size: "md" })}>تصفح الفرص</Link>
      </div>
    );

  const items = result.items;
  return (
    <div className="flex flex-col gap-4">
      <div className="overflow-x-auto rounded-lg border border-line bg-white shadow-1">
        <table className="w-full min-w-[640px] border-collapse text-14">
          <caption className="sr-only">مقارنة الفرص</caption>
          <thead>
            <tr>
              <th scope="col" className="sticky start-0 z-10 w-40 bg-warm p-3 text-start align-bottom text-13 font-semibold text-muted">البند</th>
              {items.map((it) => (
                <th key={it.reference} scope="col" className="min-w-48 border-s border-divider p-3 text-start align-top">
                  <Header item={it} onRemove={() => remove(it.reference)} />
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {ROWS.map((row) => (
              <tr key={row.key} className="border-t border-divider">
                <th scope="row" className="sticky start-0 z-10 bg-warm p-3 text-start align-top text-13 font-semibold">{row.label}</th>
                {items.map((it) => (
                  <td key={it.reference} className="border-s border-divider p-3 align-top">
                    {it.available ? <CellView cell={it.cells[row.key]} money={row.money} /> : <span className="text-muted">—</span>}
                  </td>
                ))}
              </tr>
            ))}
            <tr className="border-t border-divider">
              <th scope="row" className="sticky start-0 z-10 bg-warm p-3 text-start align-top text-13 font-semibold">موافقات النقل</th>
              {items.map((it) => (
                <td key={it.reference} className="border-s border-divider p-3 align-top">
                  {it.available ? (
                    <ul className="m-0 flex list-none flex-col gap-1 p-0">
                      {it.approvals.map((a) => (
                        <li key={a.party} className="flex flex-col gap-0.5">
                          <span className="text-12 text-muted">{a.party}</span>
                          <Badge tone={a.status === "approved" ? "ok" : a.status === "conditional" ? "info" : a.status === "rejected" || a.status === "expired" ? "err" : "neutral"}>{a.statusLabel}</Badge>
                        </li>
                      ))}
                      {it.approvals.length === 0 ? <span className="text-muted">لم تُسجل</span> : null}
                    </ul>
                  ) : <span className="text-muted">—</span>}
                </td>
              ))}
            </tr>
            {result.hasProfile ? (
              <tr className="border-t border-divider">
                <th scope="row" className="sticky start-0 z-10 bg-warm p-3 text-start align-top text-13 font-semibold">مع قدرتك المسجلة</th>
                {items.map((it) => (
                  <td key={it.reference} className="border-s border-divider p-3 align-top">
                    {it.available && it.fit ? <FitSummary fit={it.fit} compact /> : <span className="text-muted">—</span>}
                  </td>
                ))}
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>
      <p className="m-0 text-13 leading-6 text-muted">
        «لا ينطبق» يعني أن هذا البند غير موجود في طريق هذه الفرصة (مثل الأقساط في عقار مموّل يُسدَّد تمويله عند الإتمام)، وليس صفرًا. الأرقام تقديرية ما لم يذكر أن الفريق راجعها، والمقارنة لا تعني توفيرًا أو موافقة تمويل.
      </p>
    </div>
  );
}

function Header({ item, onRemove }: { item: CompareItem; onRemove: () => void }) {
  return (
    <div className="flex flex-col gap-2">
      {item.available ? (
        <>
          {item.coverUrl ? (
            // eslint-disable-next-line @next/next/no-img-element -- API-served listing photo
            <img src={item.coverUrl} alt="" className="aspect-[4/3] w-full rounded-sm object-cover" loading="lazy" />
          ) : null}
          <Link href={`/opportunities/${item.reference}`} className="text-14 leading-6 font-bold text-ink">{item.title}</Link>
          <span className="flex flex-wrap gap-1"><Badge>{item.trackLabel}</Badge>{item.isDemo ? <DemoBadge /> : null}</span>
        </>
      ) : (
        <span className="flex flex-col gap-1">
          <Badge tone="warn">لم تعد متاحة</Badge>
          <bdi dir="ltr" className="font-mono text-12 text-muted">{item.reference}</bdi>
        </span>
      )}
      <button type="button" onClick={onRemove} className="inline-flex w-fit items-center gap-1 text-12 font-semibold text-muted underline">
        <Icon name="close" size={14} />
        إزالة
      </button>
    </div>
  );
}

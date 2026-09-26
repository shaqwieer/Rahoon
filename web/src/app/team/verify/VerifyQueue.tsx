"use client";

import Link from "next/link";
import { useTeamCopy } from "@/components/team/TeamShell";
import { Tag } from "@/components/ui/Status";
import { EmptyState } from "@/components/ui/SystemState";
import type { VerifyItem } from "@/lib/api/team";
import { formatDateTime } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";

export function VerifyQueue({ items }: { items: VerifyItem[] }) {
  const c = useTeamCopy();
  const V = c.verify;
  const { numerals } = useI18n();
  return (
    <div className="flex flex-col gap-5">
      <header className="flex flex-col gap-1">
        <h1 className="m-0 text-26 leading-9 font-bold">{V.title}</h1>
        <p className="m-0 text-15 text-muted">{V.sub}</p>
      </header>
      {items.length === 0 ? (
        <EmptyState icon="fact_check" title={V.empty} />
      ) : (
        <ul className="m-0 flex list-none flex-col gap-2.5 p-0">
          {items.map((i) => (
            <li key={i.offerId} className="flex flex-wrap items-center gap-3 rounded-lg border border-line bg-white p-4">
              <div className="flex min-w-0 flex-1 flex-col gap-1">
                <span className="flex flex-wrap items-center gap-2">
                  <bdi dir="ltr" className="font-mono font-semibold">
                    {i.reference}
                  </bdi>
                  <Tag tone="info">{c.offers.paths[i.path] ?? i.path}</Tag>
                  <span className="text-13 text-muted">{c.offers.version(i.versionNo)}</span>
                </span>
                <span className="text-14">{i.institutionName}</span>
                <span className="text-13 text-muted">
                  {c.offers.recordedBy(i.recordedByLabel)} · <bdi dir="ltr">{formatDateTime(i.recordedAt, { numerals })}</bdi>
                </span>
              </div>
              {i.recordedByMe ? (
                <Tag tone="neutral" icon="block">
                  {V.recordedByYou}
                </Tag>
              ) : (
                <Link href={`/team/requests/${i.reference}`} className="inline-flex min-h-10 items-center rounded-sm bg-rust-700 px-4 text-14 font-semibold text-white no-underline hover:text-white">
                  {V.open}
                </Link>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

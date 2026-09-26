"use client";

import Link from "next/link";
import { useState } from "react";
import { useTeamCopy } from "@/components/team/TeamShell";
import { Tag } from "@/components/ui/Status";
import { EmptyState } from "@/components/ui/SystemState";
import { Button } from "@/components/ui/Button";
import { Tabs } from "@/components/ui/Tabs";
import type { ConcernQueue } from "@/lib/api/team";
import { formatDateTime } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";
import { AnswerDialog } from "../requests/[ref]/ConcernPanels";

export function ObjectionsView({ queue }: { queue: ConcernQueue }) {
  const c = useTeamCopy();
  const K = c.concerns;
  const { numerals } = useI18n();
  const [answering, setAnswering] = useState<string | null>(null);
  return (
    <div className="flex flex-col gap-5">
      <header className="flex flex-col gap-1">
        <h1 className="m-0 text-26 leading-9 font-bold">{K.title}</h1>
        <p className="m-0 text-15 text-muted">{K.sub}</p>
      </header>
      <Tabs
        label={K.tabsLabel}
        tabs={[
          { key: "open", label: K.tabs.open, href: "/team/objections?tab=open" },
          { key: "answered", label: K.tabs.answered, href: "/team/objections?tab=answered" },
        ]}
        active={queue.tab}
      />
      {queue.items.length === 0 ? (
        <EmptyState icon="support_agent" title={K.empty} />
      ) : (
        <ul className="m-0 flex list-none flex-col gap-3 p-0">
          {queue.items.map((i) => (
            <li key={i.id} className="flex flex-col gap-2 rounded-lg border border-line bg-white p-4">
              <div className="flex flex-wrap items-center gap-2 text-13">
                <Tag tone={i.kind === "complaint" ? "warn" : "info"}>{K.kinds[i.kind] ?? i.kind}</Tag>
                <Tag tone="neutral">{K.subjects[i.subject] ?? i.subject}</Tag>
                <bdi dir="ltr" className="font-mono font-semibold">
                  {i.reference}
                </bdi>
                <span className="text-muted">
                  {K.request}:{" "}
                  <Link href={`/team/requests/${i.requestReference}`} className="font-mono">
                    <bdi dir="ltr">{i.requestReference}</bdi>
                  </Link>{" "}
                  · {i.institutionName} · {i.applicantName}
                </span>
              </div>
              <p className="m-0 text-15 leading-6 whitespace-pre-line">{i.text}</p>
              <span className="text-12 text-muted">
                <bdi dir="ltr">{formatDateTime(i.createdAt, { numerals })}</bdi>
                {queue.tab === "open" ? ` · ${K.daysOpen(c.queue.days(i.daysOpen))} (${c.internal})` : ""}
              </span>
              {i.responseText ? (
                <div className="rounded-sm bg-info-bg p-2 text-14">
                  <strong>{i.outcome ? `${K.outcomes[i.outcome] ?? i.outcome} · ` : ""}</strong>
                  {i.responseText}
                  {i.respondedByLabel ? <span className="block text-12 text-muted">{K.answeredBy(i.respondedByLabel)}</span> : null}
                </div>
              ) : null}
              {queue.tab === "open" ? (
                i.mayAnswer ? (
                  <Button size="sm" className="self-start" onClick={() => setAnswering(i.id)}>
                    {K.answer}
                  </Button>
                ) : (
                  <span className="text-13 text-muted">{K.notYou}</span>
                )
              ) : null}
            </li>
          ))}
        </ul>
      )}
      <AnswerDialog concernId={answering} onClose={() => setAnswering(null)} />
    </div>
  );
}

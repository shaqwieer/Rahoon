"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { useTeamCopy } from "@/components/team/TeamShell";
import { Icon } from "@/components/ui/Icon";
import { Tag } from "@/components/ui/Status";
import { EmptyState } from "@/components/ui/SystemState";
import { Tabs } from "@/components/ui/Tabs";
import type { Tone } from "@/components/ui/tones";
import type { TeamQueue, TeamTimer } from "@/lib/api/team";
import { formatDate } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";

export const TEAM_STATUS_TONE: Record<string, { tone: Tone; icon: string }> = {
  submitted: { tone: "info", icon: "send" },
  team_review: { tone: "info", icon: "manage_search" },
  info_requested: { tone: "warn", icon: "help" },
  lender_coordination: { tone: "info", icon: "forum" },
  offer_available: { tone: "sel", icon: "local_offer" },
  response_recorded: { tone: "warn", icon: "task_alt" },
  closed: { tone: "neutral", icon: "lock" },
  not_eligible: { tone: "err", icon: "block" },
  withdrawn: { tone: "neutral", icon: "undo" },
};

export function TeamStatusTag({ status }: { status: string }) {
  const c = useTeamCopy();
  const s = TEAM_STATUS_TONE[status] ?? { tone: "neutral" as Tone, icon: "radio_button_unchecked" };
  return (
    <Tag tone={s.tone} icon={s.icon}>
      {c.status[status as keyof typeof c.status] ?? status}
    </Tag>
  );
}

/** Internal duration indicator (V5) — icon + text, labelled internal wherever it appears. */
export function TimerTag({ timer }: { timer: TeamTimer }) {
  const c = useTeamCopy();
  if (timer.level === "none") return <span className="text-muted">—</span>;
  const tone: Tone = timer.level === "alert" ? "err" : timer.level === "warn" ? "warn" : "neutral";
  return (
    <span className="inline-flex flex-col gap-0.5">
      <Tag tone={tone} icon={timer.level === "ok" ? "schedule" : "alarm"}>
        {c.queue.days(timer.daysInStatus)}
      </Tag>
      <span className="text-11 text-muted">{c.queue.timerLevel[timer.level]}</span>
    </span>
  );
}

export function TeamQueueView({ queue }: { queue: TeamQueue }) {
  const c = useTeamCopy();
  const Q = c.queue;
  const { numerals } = useI18n();
  const tabs = [
    { key: "mine", label: Q.tabs.mine, count: queue.counts.mine, href: "/team?tab=mine" },
    { key: "unassigned", label: Q.tabs.unassigned, count: queue.counts.unassigned, href: "/team?tab=unassigned" },
    ...(queue.canViewAll ? [{ key: "all", label: Q.tabs.all, count: queue.counts.all, href: "/team?tab=all" }] : []),
    { key: "closed", label: Q.tabs.closed, href: "/team?tab=closed" },
  ];
  const head = (children: ReactNode) => <th className="px-3 py-2.5 text-start text-13 font-semibold text-muted">{children}</th>;

  return (
    <div className="flex flex-col gap-5">
      <header className="flex flex-col gap-1">
        <h1 className="m-0 text-26 leading-9 font-bold">{Q.title}</h1>
        <p className="m-0 text-15 text-muted">{Q.sub}</p>
      </header>
      <Tabs label={Q.tabsLabel} tabs={tabs} active={queue.tab} />
      {queue.items.length === 0 ? (
        <EmptyState icon="inbox" title={Q.emptyTitle} body={Q.emptyBody} />
      ) : (
        <>
          <div className="hidden overflow-x-auto rounded-lg border border-line bg-white md:block">
            <table className="w-full border-collapse text-14">
              <thead className="border-b border-line bg-subtle">
                <tr>
                  {head(Q.cols.ref)}
                  {head(Q.cols.applicant)}
                  {head(Q.cols.lender)}
                  {head(Q.cols.status)}
                  {head(Q.cols.waiting)}
                  {head(Q.cols.assigned)}
                  {head(
                    <span className="inline-flex items-center gap-1" title={c.internal}>
                      {Q.cols.timer} <Icon name="lock" size={14} />
                    </span>,
                  )}
                </tr>
              </thead>
              <tbody>
                {queue.items.map((r) => (
                  <tr key={r.reference} className="border-b border-divider last:border-0 hover:bg-subtle">
                    <td className="px-3 py-3">
                      <Link href={`/team/requests/${r.reference}`} className="font-mono font-semibold">
                        <bdi dir="ltr">{r.reference}</bdi>
                      </Link>
                      {r.submittedAt ? (
                        <div className="text-12 text-muted">
                          <bdi dir="ltr">{formatDate(r.submittedAt, { numerals })}</bdi>
                        </div>
                      ) : null}
                    </td>
                    <td className="px-3 py-3">{r.applicantName}</td>
                    <td className="px-3 py-3">{r.institutionName}</td>
                    <td className="px-3 py-3">
                      <TeamStatusTag status={r.status} />
                    </td>
                    <td className="px-3 py-3">{c.waiting[r.waitingOn]}</td>
                    <td className="px-3 py-3">{r.assignedTo ?? <span className="text-muted">{Q.unassigned}</span>}</td>
                    <td className="px-3 py-3">
                      <TimerTag timer={r.timer} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <p className="m-0 flex items-center gap-1.5 border-t border-divider px-3 py-2 text-12 text-muted">
              <Icon name="lock" size={14} />
              {Q.cols.timer}: {c.internal}
            </p>
          </div>
          <ul className="m-0 flex list-none flex-col gap-2.5 p-0 md:hidden">
            {queue.items.map((r) => (
              <li key={r.reference}>
                <Link href={`/team/requests/${r.reference}`} className="flex flex-col gap-1.5 rounded-lg border border-line bg-white p-4 text-ink no-underline hover:bg-subtle">
                  <span className="flex items-center justify-between gap-2">
                    <bdi dir="ltr" className="font-mono text-14 font-semibold">
                      {r.reference}
                    </bdi>
                    <TeamStatusTag status={r.status} />
                  </span>
                  <strong className="text-15">{r.institutionName}</strong>
                  <span className="text-14 text-muted">
                    {r.applicantName} · {Q.cols.waiting}: {c.waiting[r.waitingOn]}
                  </span>
                  <span className="text-13 text-muted">{r.assignedTo ?? Q.unassigned}</span>
                </Link>
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
}

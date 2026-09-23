"use client";

import { useId, type ReactNode } from "react";
import { cn } from "@/lib/cn";
import type { SlaTone } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";
import { Avatar } from "./Avatar";
import { Button } from "./Button";
import { Icon } from "./Icon";
import { SlaBadge } from "./Status";

export type NextActionState = "available" | "blocked" | "not-yours";

export interface NextActionCardProps {
  state: NextActionState;
  /** What to do, e.g. «إرسال الحل v2 للموافقة الداخلية». Rendered as h3. */
  title: ReactNode;
  headingLevel?: 2 | 3;
  /** Deadline chip at the top end (blocked defaults to «غير مؤهل بعد»). */
  sla?: { tone: SlaTone; text: ReactNode };
  /** Satisfied conditions / evidence (available state). */
  checklist?: Array<{ label: ReactNode; meta?: ReactNode }>;
  /** «بعدها: …» — what happens after the action. */
  after?: ReactNode;
  /** The single dominant action (available). Label conventionally ends with «…» when it opens a review screen. */
  action?: { label: string; href?: string; onClick?: () => void; review?: boolean; loading?: boolean };
  /** Why the action is not possible yet (blocked). Linked to the disabled button via aria-describedby. */
  blocked?: { title: ReactNode; reasons: ReactNode[]; fix?: { label: string; href: string } };
  /** Who owns the action (not-yours). No primary button is shown. */
  owner?: { name: string; initials: string; detail?: ReactNode };
  /** Context under the owner block (sent at, deadline, separation-of-duties note). */
  note?: ReactNode;
  /** Optional low-emphasis action for not-yours (e.g. «إرسال تذكير»). */
  secondaryAction?: { label: string; onClick?: () => void; href?: string };
  className?: string;
}

const TOP_BORDER: Record<NextActionState, string> = {
  available: "border-t-orange",
  blocked: "border-t-line-strong",
  "not-yours": "border-t-info",
};

/** C04: one authorized dominant action; blocked shows the reason; not-yours shows the owner instead of a button. */
export function NextActionCard({
  state,
  title,
  headingLevel = 3,
  sla,
  checklist,
  after,
  action,
  blocked,
  owner,
  note,
  secondaryAction,
  className,
}: NextActionCardProps) {
  const { t } = useI18n();
  const reasonId = useId();
  const H = headingLevel === 2 ? "h2" : "h3";
  const eyebrow = state === "not-yours" ? t.nextAction.eyebrowNotYours : t.nextAction.eyebrowYours;
  const badge =
    state === "blocked" ? (
      <span className="inline-flex items-center gap-1 rounded-xs bg-info-bg px-2 py-0.5 text-12 font-semibold text-info">
        <Icon name="block" size={16} />
        {t.nextAction.blocked}
      </span>
    ) : sla ? (
      <SlaBadge tone={sla.tone} size="sm">
        {sla.text}
      </SlaBadge>
    ) : null;

  return (
    <article className={cn("flex flex-col gap-3 rounded-md border border-t-[3px] border-line bg-white p-5", TOP_BORDER[state], className)}>
      <div className="flex items-center justify-between gap-2">
        <span className="text-12 font-semibold text-muted">{eyebrow}</span>
        {badge}
      </div>
      <H className="m-0 text-18 leading-7 font-bold">{title}</H>

      {state === "available" && checklist?.length ? (
        <ul className="m-0 flex list-none flex-col gap-1.5 p-0 text-14 leading-[22px]">
          {checklist.map((c, i) => (
            <li key={i} className="flex gap-2">
              <Icon name="check_circle" size={18} className="text-ok" />
              <span>
                {c.label} {c.meta ? <span className="text-muted">{c.meta}</span> : null}
              </span>
            </li>
          ))}
        </ul>
      ) : null}

      {state === "blocked" && blocked ? (
        <div id={reasonId} role="note" className="flex flex-col gap-1.5 rounded-sm bg-warm p-3 text-14 leading-[22px]">
          <strong>{blocked.title}</strong>
          {blocked.reasons.map((r, i) => (
            <span key={i} className="flex gap-2">
              <Icon name="cancel" size={18} className="text-err" />
              <span>{r}</span>
            </span>
          ))}
          {blocked.fix ? (
            <a href={blocked.fix.href} className="font-semibold">
              {blocked.fix.label}
            </a>
          ) : null}
        </div>
      ) : null}

      {state === "not-yours" && owner ? (
        <div className="flex items-center gap-2.5">
          <Avatar initials={owner.initials} size={36} />
          <div className="flex flex-col">
            <span className="text-14 font-semibold">{owner.name}</span>
            {owner.detail ? <span className="text-13 text-muted">{owner.detail}</span> : null}
          </div>
        </div>
      ) : null}

      {note ? <div className="text-13 leading-5 text-muted">{note}</div> : null}
      {after ? (
        <div className="text-13 leading-5 text-muted">
          {t.nextAction.after} {after}
        </div>
      ) : null}

      {state === "available" && action ? (
        <Button size="lg" fullWidth href={action.href} onClick={action.onClick} review={action.review} loading={action.loading} className="min-h-11 text-15">
          {action.label}
        </Button>
      ) : null}
      {state === "blocked" && action ? (
        <Button size="lg" fullWidth softDisabled review={action.review} aria-describedby={blocked ? reasonId : undefined} className="min-h-11 text-15">
          {action.label}
        </Button>
      ) : null}
      {state === "not-yours" && secondaryAction ? (
        <Button variant="secondary" size="lg" fullWidth href={secondaryAction.href} onClick={secondaryAction.onClick} className="min-h-11 text-15">
          {secondaryAction.label}
        </Button>
      ) : null}
    </article>
  );
}

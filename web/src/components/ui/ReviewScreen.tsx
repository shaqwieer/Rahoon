"use client";

import { useId, type ReactNode } from "react";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";
import { Alert } from "./Alert";
import { Button } from "./Button";
import { Icon } from "./Icon";

export interface ReviewScreenProps {
  /** «إجراء عالي الأثر · يُسجَّل في السجل» by default. */
  eyebrow?: ReactNode;
  /** Page title (h1) announcing the action. */
  title: ReactNode;
  /** 1 · «ما سيحدث»: transition, lock, assignment, deadline, what the owner sees. */
  whatHappens: Array<{ icon: string; content: ReactNode }>;
  /** 2 · «الأدلة»: attached evidence rows. */
  evidence?: Array<{ name: ReactNode; meta?: ReactNode; href?: string; linkLabel?: string }>;
  /** 3 · reason textarea + attestation checkbox (caller-owned form controls). */
  reason: ReactNode;
  /** Separation of duties: when set, replaces the attestation area with the reason the user cannot act. */
  blockedReason?: ReactNode;
  sectionTitles?: { whatHappens?: ReactNode; evidence?: ReactNode; reason?: ReactNode };
  /** Right column (summary card, notes). */
  aside?: ReactNode;
  back: { label: string; href?: string; onClick?: () => void };
  /** «سيُسجل: الفاعل، الوقت، الملاحظة، نسخة v2». */
  recordCaption?: ReactNode;
  primary: { label: string; onClick: () => void; loading?: boolean; loadingLabel?: string };
  /** The primary button stays disabled until the reason and attestation are complete. */
  complete: boolean;
  /** Error from the last submit (focus should move to the first missing field — caller's ErrorSummary). */
  error?: ReactNode;
  className?: string;
}

/** Numbered review section card (h2 with number circle). */
export function ReviewSection({ n, title, children, id }: { n: number; title: ReactNode; children: ReactNode; id?: string }) {
  const autoId = useId();
  const hid = id ?? autoId;
  return (
    <section aria-labelledby={hid} className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
      <h2 id={hid} className="m-0 flex items-center gap-2 text-17 font-semibold">
        <span aria-hidden="true" className="inline-flex size-6 items-center justify-center rounded-full bg-ink text-13 text-white">
          {n}
        </span>
        <span className="sr-only">{n}. </span>
        {title}
      </h2>
      {children}
    </section>
  );
}

/**
 * High-impact action pattern (L15 / Handoff ReviewScreen): numbered sections «ما سيحدث / الأدلة / السبب والإقرار»,
 * optional aside, and a sticky footer with back + primary (disabled until complete). No yes/no dialogs.
 */
export function ReviewScreen({
  eyebrow,
  title,
  whatHappens,
  evidence,
  reason,
  blockedReason,
  sectionTitles,
  aside,
  back,
  recordCaption,
  primary,
  complete,
  error,
  className,
}: ReviewScreenProps) {
  const { t } = useI18n();
  const hintId = useId();
  let n = 0;
  return (
    <div className={cn("flex min-h-full flex-col", className)}>
      <div className="grid flex-1 grid-cols-1 gap-8 pb-8 lg:grid-cols-[minmax(0,1fr)_380px]">
        <div className="flex max-w-[760px] flex-col gap-5">
          <div className="flex flex-col gap-1">
            <span className="text-13 text-muted">{eyebrow ?? t.review.highImpact}</span>
            <h1 className="m-0 text-28 leading-10 font-bold">{title}</h1>
          </div>
          {error ? <Alert tone="err">{error}</Alert> : null}
          <ReviewSection n={++n} title={sectionTitles?.whatHappens ?? t.review.whatHappens}>
            <ul className="m-0 flex list-none flex-col gap-2.5 p-0 text-15 leading-6">
              {whatHappens.map((w, i) => (
                <li key={i} className="flex gap-2.5">
                  <Icon name={w.icon} size={20} className="text-muted" />
                  <span>{w.content}</span>
                </li>
              ))}
            </ul>
          </ReviewSection>
          {evidence?.length ? (
            <ReviewSection n={++n} title={sectionTitles?.evidence ?? t.review.evidence}>
              <ul className="m-0 flex list-none flex-col p-0">
                {evidence.map((e, i) => (
                  <li key={i} className="flex min-h-9 flex-wrap items-center gap-2.5 text-14">
                    <Icon name="check_circle" size={20} className="text-ok" />
                    <span className="flex-1">{e.name}</span>
                    {e.meta ? <span className="text-13 text-muted">{e.meta}</span> : null}
                    {e.href ? (
                      <a href={e.href} className="text-13 font-semibold">
                        {e.linkLabel ?? t.common.open}
                      </a>
                    ) : null}
                  </li>
                ))}
              </ul>
            </ReviewSection>
          ) : null}
          <ReviewSection n={++n} title={sectionTitles?.reason ?? t.review.reasonAndAttestation}>
            {blockedReason ? (
              <Alert tone="info" icon="block" role="none">
                {blockedReason}
              </Alert>
            ) : (
              reason
            )}
          </ReviewSection>
        </div>
        {aside ? <aside className="flex flex-col gap-4">{aside}</aside> : null}
      </div>
      <div className="sticky bottom-0 z-10 -mx-4 flex min-h-[76px] flex-wrap items-center gap-3 border-t border-line bg-white px-4 py-3 md:-mx-10 md:px-10">
        <Button variant="secondary" size="lg" href={back.href} onClick={back.onClick} className="min-h-11 text-15">
          {back.label}
        </Button>
        <span id={hintId} className="ms-auto text-13 text-muted">
          {!complete && !blockedReason ? t.review.incomplete : (recordCaption ?? t.review.willRecord)}
        </span>
        <Button
          size="lg"
          className="min-h-11 px-[22px] text-15"
          softDisabled={!complete || Boolean(blockedReason)}
          aria-describedby={hintId}
          loading={primary.loading}
          loadingLabel={primary.loadingLabel}
          onClick={primary.onClick}
        >
          {primary.label}
        </Button>
      </div>
    </div>
  );
}

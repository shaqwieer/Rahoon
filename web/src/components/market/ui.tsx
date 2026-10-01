import type { ReactNode } from "react";
import { Icon } from "@/components/ui/Icon";
import { cn } from "@/lib/cn";
import { dayTime, sar } from "@/lib/market/format";

/**
 * An amount in riyals. Null is never shown as 0: it reads «غير معروف» (or the given label) in a muted style.
 * Server- and client-safe (no hooks). The number is isolated LTR; the unit stays in the reading direction.
 */
export function Amount({ value, unknown = "غير معروف", className, size = "md", strong }: { value: number | null | undefined; unknown?: string; className?: string; size?: "sm" | "md" | "lg" | "xl"; strong?: boolean }) {
  const text = sar(value);
  const sizes = { sm: "text-13", md: "text-15", lg: "text-20", xl: "text-28 leading-9" };
  if (text === null)
    return (
      <span className={cn("inline-flex items-center gap-1 text-muted", sizes[size], className)}>
        <Icon name="help" size={size === "xl" ? 22 : 16} />
        {unknown}
      </span>
    );
  return (
    <span className={cn("whitespace-nowrap", sizes[size], strong && "font-bold", className)}>
      <bdi dir="ltr" className="tabular-nums">
        {text}
      </bdi>{" "}
      <span className="text-[0.8em] font-medium text-muted">ر.س</span>
    </span>
  );
}

export type BadgeTone = "neutral" | "info" | "ok" | "warn" | "err" | "rust";

const TONES: Record<BadgeTone, string> = {
  neutral: "border-line bg-subtle text-charcoal",
  info: "border-info-line bg-info-bg text-info",
  ok: "border-ok-line bg-ok-bg text-ok",
  warn: "border-warn-line bg-warn-bg text-warn",
  err: "border-err-line bg-err-bg text-err",
  rust: "border-rust-200 bg-rust-50 text-rust",
};

export function Badge({ tone = "neutral", icon, children, className }: { tone?: BadgeTone; icon?: string; children: ReactNode; className?: string }) {
  return (
    <span className={cn("inline-flex w-fit items-center gap-1 rounded-pill border px-2.5 py-0.5 text-12 leading-5 font-semibold whitespace-nowrap", TONES[tone], className)}>
      {icon ? <Icon name={icon} size={14} /> : null}
      {children}
    </span>
  );
}

/** Status tone per status key (sale/buyer/opportunity/interest). */
export function statusTone(status: string): BadgeTone {
  switch (status) {
    case "submitted":
    case "received":
    case "preparing":
      return "info";
    case "underReview":
    case "inFollowUp":
    case "awaitingOwnerConfirmation":
      return "rust";
    case "needsCompletion":
    case "paused":
      return "warn";
    case "approvedForListing":
    case "approvedForMatching":
    case "readyToPublish":
    case "published":
      return "ok";
    case "rejected":
    case "withdrawn":
    case "closed":
      return "err";
    default:
      return "neutral";
  }
}

export function StatusBadge({ status, label }: { status: string; label: string }) {
  return <Badge tone={statusTone(status)}>{label}</Badge>;
}

/** What has been verified, in one honest tag: never a full «موثّق» unless every figure was checked against a document. */
export function QualityBadge({ quality }: { quality: "complete_verified" | "complete_estimate" | "incomplete" }) {
  if (quality === "complete_verified") return <Badge tone="ok" icon="verified">أرقام راجعها الفريق</Badge>;
  if (quality === "complete_estimate") return <Badge tone="info" icon="calculate">تقدير مبدئي</Badge>;
  return <Badge tone="warn" icon="pending">تقدير غير مكتمل</Badge>;
}

export function DemoBadge({ className }: { className?: string }) {
  return (
    <Badge tone="neutral" icon="science" className={className}>
      تجريبي
    </Badge>
  );
}

export function Card({ title, actions, children, className, id }: { title?: ReactNode; actions?: ReactNode; children: ReactNode; className?: string; id?: string }) {
  return (
    <section id={id} className={cn("rounded-lg border border-line bg-white p-4 shadow-1 md:p-6", className)}>
      {title || actions ? (
        <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
          {title ? <h2 className="m-0 text-18 leading-7 font-bold">{title}</h2> : <span />}
          {actions}
        </div>
      ) : null}
      {children}
    </section>
  );
}

export function Row({ label, children, hint }: { label: ReactNode; children: ReactNode; hint?: ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 border-b border-divider py-2.5 last:border-b-0 sm:flex-row sm:items-baseline sm:justify-between sm:gap-6">
      <dt className="text-14 text-muted">{label}</dt>
      <dd className="m-0 flex flex-col items-start gap-0.5 text-15 font-semibold sm:items-end sm:text-end">
        {children}
        {hint ? <span className="text-12 font-normal text-muted">{hint}</span> : null}
      </dd>
    </div>
  );
}

export function Timeline({ events, empty = "لا توجد أحداث بعد." }: { events: { id: string; title: string; body?: string | null; reason?: string | null; at: string; actorLabel?: string | null; visible?: boolean }[]; empty?: string }) {
  if (events.length === 0) return <p className="m-0 text-14 text-muted">{empty}</p>;
  return (
    <ol className="m-0 flex list-none flex-col p-0">
      {events.map((e, i) => (
        <li key={e.id} className="relative flex gap-3 pb-4 last:pb-0">
          <span className={cn("relative z-10 mt-1.5 size-2.5 flex-none rounded-full", i === 0 ? "bg-orange" : "bg-line-strong")} />
          {i < events.length - 1 ? <span className="absolute start-[4px] top-4 bottom-0 w-0.5 bg-divider" aria-hidden="true" /> : null}
          <span className="flex min-w-0 flex-col gap-0.5">
            <span className="text-15 font-semibold">
              {e.title}
              {e.visible === false ? <span className="ms-2 text-12 font-normal text-muted">(داخلي)</span> : null}
            </span>
            {e.body ? <span className="text-14 leading-6 text-charcoal">{e.body}</span> : null}
            {e.reason ? <span className="text-14 leading-6 text-charcoal">السبب: {e.reason}</span> : null}
            <span className="text-12 text-muted">
              <bdi dir="ltr">{dayTime(e.at)}</bdi>
              {e.actorLabel ? ` · ${e.actorLabel}` : null}
            </span>
          </span>
        </li>
      ))}
    </ol>
  );
}

/** Honest save indicator for autosaving forms. */
export function SaveState({ state }: { state: "idle" | "device" | "saving" | "saved" | "failed" }) {
  if (state === "idle") return null;
  const map = {
    device: { icon: "smartphone", text: "محفوظ على هذا الجهاز فقط", cls: "text-muted" },
    saving: { icon: "sync", text: "جارٍ الحفظ…", cls: "text-muted" },
    saved: { icon: "cloud_done", text: "تم الحفظ في حسابك", cls: "text-ok" },
    failed: { icon: "cloud_off", text: "لم يُحفظ — تعديلاتك محفوظة على هذا الجهاز وسنعيد المحاولة", cls: "text-err" },
  }[state];
  return (
    <span role="status" aria-live="polite" className={cn("inline-flex items-center gap-1 text-13", map.cls)}>
      <Icon name={map.icon} size={16} className={state === "saving" ? "animate-rh-spin" : undefined} />
      {map.text}
    </span>
  );
}

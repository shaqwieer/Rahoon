import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { Icon } from "./Icon";

export type AlertTone = "info" | "ok" | "warn" | "err" | "neutral";

const TONE: Record<AlertTone, { box: string; fg: string; icon: string }> = {
  info: { box: "bg-info-bg border-info-line", fg: "text-info", icon: "info" },
  ok: { box: "bg-ok-bg border-ok-line", fg: "text-ok", icon: "check_circle" },
  warn: { box: "bg-warn-bg border-warn-line", fg: "text-warn", icon: "schedule" },
  err: { box: "bg-err-bg border-err-line", fg: "text-err", icon: "error" },
  neutral: { box: "bg-warm border-line", fg: "text-charcoal", icon: "info" },
};

export interface AlertProps {
  tone?: AlertTone;
  title?: ReactNode;
  children?: ReactNode;
  icon?: string;
  /** Link or button at the inline end (e.g. «فتح التكليف»). */
  action?: ReactNode;
  /**
   * `status` (polite) for info/ok, `alert` (assertive) for warn/err — the C05 default.
   * Pass `none` for static callouts that are part of the page, not a change.
   */
  role?: "status" | "alert" | "none";
  /** Compact callout style (13px, used for notes inside cards). */
  compact?: boolean;
  className?: string;
  id?: string;
}

/** In-page alert / callout (C05). Stays until resolved; colour is supported by an icon and text. */
export function Alert({ tone = "info", title, children, icon, action, role, compact, className, id }: AlertProps) {
  const t = TONE[tone];
  const resolvedRole = role ?? (tone === "warn" || tone === "err" ? "alert" : "status");
  return (
    <div
      id={id}
      role={resolvedRole === "none" ? undefined : resolvedRole}
      className={cn(
        "flex items-start gap-3 rounded-md border",
        compact ? "px-3.5 py-3 text-13 leading-5" : "px-4 py-3.5 text-14 leading-[22px]",
        t.box,
        className,
      )}
    >
      <Icon name={icon ?? t.icon} size={compact ? 20 : 22} className={t.fg} />
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
        {title ? <strong className={cn(compact ? "text-14" : "text-15", t.fg)}>{title}</strong> : null}
        {children ? <div>{children}</div> : null}
      </div>
      {action ? <div className="flex-none self-center text-14 font-semibold whitespace-nowrap [&_a]:text-ink">{action}</div> : null}
    </div>
  );
}

/**
 * Development-only notice for simulated SMS: shows the `sandboxCode` the API returns in dev.
 * Never pretend a real SMS was delivered.
 */
export function SandboxCodeBox({ code, title, note }: { code: string; title: string; note?: string }) {
  return (
    <div role="note" className="flex items-start gap-3 rounded-md border border-dashed border-info-line bg-info-bg px-4 py-3 text-14 leading-[22px]">
      <Icon name="science" size={20} className="text-info" />
      <div className="flex flex-col gap-0.5">
        <span>
          {title}{" "}
          <bdi dir="ltr" className="font-mono text-16 font-semibold tracking-[2px] text-ink">
            {code}
          </bdi>
        </span>
        {note ? <span className="text-12 text-muted">{note}</span> : null}
      </div>
    </div>
  );
}

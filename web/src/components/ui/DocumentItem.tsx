"use client";

import { useId, useRef, useState, type ReactNode } from "react";
import { cn } from "@/lib/cn";
import { useI18n } from "@/lib/i18n/client";
import { Button } from "./Button";
import { Icon } from "./Icon";
import { TONES, type Tone } from "./tones";

export type DocumentStatus =
  | "required"
  | "uploaded"
  | "under_review"
  | "verified"
  | "valid"
  | "rejected"
  | "returned"
  | "expired"
  | "expiring"
  | "not_requested";

const STATUS: Record<DocumentStatus, { icon: string; tone: Tone }> = {
  required: { icon: "upload_file", tone: "info" },
  uploaded: { icon: "pending", tone: "warn" },
  under_review: { icon: "pending", tone: "warn" },
  verified: { icon: "check_circle", tone: "ok" },
  valid: { icon: "event_available", tone: "ok" },
  rejected: { icon: "cancel", tone: "err" },
  returned: { icon: "undo", tone: "err" },
  expired: { icon: "event_busy", tone: "err" },
  expiring: { icon: "event_upcoming", tone: "warn" },
  not_requested: { icon: "radio_button_unchecked", tone: "neutral" },
};

export interface DocumentItemProps {
  /** Document-type icon (description, badge, analytics, receipt_long…). */
  icon?: string;
  name: ReactNode;
  /** Version label (v1, v2) — previous versions are kept, never deleted. */
  version?: string;
  /** Requester · uploader · date · validity · who can see it. */
  meta?: ReactNode;
  status: DocumentStatus;
  /** Overrides the status text (e.g. «صالح · 77 يوماً»). */
  statusLabel?: ReactNode;
  action?: { label: string; onClick?: () => void; href?: string };
  className?: string;
}

/** C07 document row: type icon, name + version, meta, status tag (icon + text), one action. */
export function DocumentItem({ icon = "description", name, version, meta, status, statusLabel, action, className }: DocumentItemProps) {
  const { t } = useI18n();
  const s = STATUS[status];
  const c = TONES[s.tone];
  return (
    <div className={cn("grid grid-cols-[40px_minmax(0,1fr)] items-center gap-3.5 px-[18px] py-3.5 sm:grid-cols-[40px_minmax(0,1fr)_auto]", className)}>
      <span className="flex size-10 items-center justify-center rounded-[8px] bg-subtle">
        <Icon name={icon} size={22} className="text-charcoal" />
      </span>
      <div className="flex min-w-0 flex-col gap-0.5">
        <div className="flex flex-wrap items-center gap-2">
          <strong className="text-15">{name}</strong>
          {version ? (
            <span dir="ltr" className="rounded-xs bg-subtle px-1.5 py-px font-mono text-11 font-medium text-muted">
              {version}
            </span>
          ) : null}
        </div>
        {meta ? <span className="text-13 leading-5 text-muted">{meta}</span> : null}
      </div>
      <div className="col-span-2 flex flex-wrap items-center justify-start gap-2.5 sm:col-span-1 sm:justify-end">
        <span
          className="inline-flex items-center gap-1 rounded-xs border px-2 py-[3px] text-12 font-semibold"
          style={{ color: c.fg, background: c.bg, borderColor: c.bd }}
        >
          <Icon name={s.icon} size={14} />
          {statusLabel ?? t.docStatus[status]}
        </span>
        {action ? (
          <Button variant="secondary" size="sm" onClick={action.onClick} href={action.href}>
            {action.label}
          </Button>
        ) : null}
      </div>
    </div>
  );
}

/** Container list for DocumentItem rows (white card, dividers between rows). */
export function DocumentList({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cn("overflow-hidden rounded-lg border border-line bg-white [&>*+*]:border-t [&>*+*]:border-divider", className)}>{children}</div>;
}

export interface UploadDropzoneProps {
  onFiles: (files: File[]) => void;
  accept?: string;
  multiple?: boolean;
  disabled?: boolean;
  hint?: ReactNode;
  className?: string;
}

/** Dashed drop area; the picker is a real button (keyboard + screen reader) and dropping is a shortcut. */
export function UploadDropzone({ onFiles, accept = ".pdf,.jpg,.jpeg,.png", multiple, disabled, hint, className }: UploadDropzoneProps) {
  const { t } = useI18n();
  const inputRef = useRef<HTMLInputElement>(null);
  const hintId = useId();
  const [over, setOver] = useState(false);
  return (
    <div
      onDragOver={(e) => {
        if (disabled) return;
        e.preventDefault();
        setOver(true);
      }}
      onDragLeave={() => setOver(false)}
      onDrop={(e) => {
        e.preventDefault();
        setOver(false);
        if (!disabled && e.dataTransfer.files.length) onFiles(Array.from(e.dataTransfer.files));
      }}
      className={cn(
        "flex flex-col items-center gap-2 rounded-md border-2 border-dashed px-6 py-6 text-center",
        over ? "border-rust bg-rust-50" : "border-line-strong bg-warm",
        className,
      )}
    >
      <Icon name="upload_file" size={28} className="text-rust" />
      <strong className="text-15">
        {t.fields.uploadDrop}{" "}
        <button
          type="button"
          disabled={disabled}
          aria-describedby={hintId}
          onClick={() => inputRef.current?.click()}
          className="bg-transparent p-0 font-[inherit] text-rust underline underline-offset-[3px] disabled:text-soft"
        >
          {t.fields.uploadPick}
        </button>
      </strong>
      <span id={hintId} className="text-13 text-muted">
        {hint ?? t.fields.uploadHint}
      </span>
      <input
        ref={inputRef}
        type="file"
        accept={accept}
        multiple={multiple}
        hidden
        onChange={(e) => {
          if (e.target.files?.length) onFiles(Array.from(e.target.files));
          e.target.value = "";
        }}
      />
    </div>
  );
}

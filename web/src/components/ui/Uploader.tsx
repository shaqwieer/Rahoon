"use client";

import { useEffect, useId, useRef, useState, type DragEvent, type ReactNode } from "react";
import { isApiError, newIdempotencyKey } from "@/lib/api/client";
import { cn } from "@/lib/cn";
import { formatBytes, prepareUpload, UploadRejected, type UploadPurpose } from "@/lib/upload/compress";
import { Icon } from "./Icon";

export type UploadState = "waiting" | "preparing" | "uploading" | "done" | "failed";

export interface UploadItem {
  id: string;
  name: string;
  original: File;
  previewUrl: string | null;
  originalSize: number;
  size: number | null;
  compressed: boolean;
  state: UploadState;
  progress: number;
  error?: string;
  /** Kept across retries so the server never stores the same file twice. */
  key: string;
}

/**
 * The upload pipeline shared by photos and documents: each file is prepared (smart compression, type and size checks), then
 * sent with progress, at most `concurrency` at a time. A failed file keeps its place with «إعادة المحاولة»; a finished one
 * leaves the list after a moment (the page shows it in its own place).
 */
export function useUploadQueue<T>({ purpose, send, onUploaded, concurrency = 2 }: {
  purpose: UploadPurpose;
  send: (file: File, opts: { onProgress: (f: number) => void; idempotencyKey: string }) => Promise<T>;
  onUploaded: (result: T, item: UploadItem) => void;
  concurrency?: number;
}) {
  const [items, setItems] = useState<UploadItem[]>([]);
  const running = useRef(0);
  const queue = useRef<string[]>([]);
  const itemsRef = useRef<UploadItem[]>([]);
  const sendRef = useRef(send);
  const doneRef = useRef(onUploaded);
  useEffect(() => {
    sendRef.current = send;
    doneRef.current = onUploaded;
  }, [send, onUploaded]);
  useEffect(() => {
    itemsRef.current = items;
  }, [items]);
  useEffect(() => () => itemsRef.current.forEach((i) => i.previewUrl && URL.revokeObjectURL(i.previewUrl)), []);

  const patch = (id: string, p: Partial<UploadItem>) => {
    setItems((xs) => xs.map((x) => (x.id === id ? { ...x, ...p } : x)));
  };

  const pump = () => {
    while (running.current < concurrency && queue.current.length) {
      const id = queue.current.shift()!;
      const item = itemsRef.current.find((x) => x.id === id);
      if (!item) continue;
      running.current++;
      void (async () => {
        try {
          patch(id, { state: "preparing", progress: 0, error: undefined });
          const prepared = await prepareUpload(item.original, purpose);
          patch(id, { state: "uploading", size: prepared.file.size, compressed: prepared.compressed });
          const result = await sendRef.current(prepared.file, { onProgress: (f) => patch(id, { progress: f }), idempotencyKey: item.key });
          patch(id, { state: "done", progress: 1 });
          doneRef.current(result, item);
          window.setTimeout(() => {
            setItems((xs) => xs.filter((x) => x.id !== id));
            if (item.previewUrl) URL.revokeObjectURL(item.previewUrl);
          }, 1600);
        } catch (err) {
          const msg = err instanceof UploadRejected ? err.message
            : isApiError(err) ? (err.title || (err.code === "offline" ? "لا يوجد اتصال بالإنترنت." : "تعذّر الرفع. تحقق من الاتصال."))
            : "تعذّر الرفع. تحقق من الاتصال.";
          patch(id, { state: "failed", error: msg });
        } finally {
          running.current--;
          pump();
        }
      })();
    }
  };

  const add = (files: File[]) => {
    const next: UploadItem[] = files.map((f) => ({
      id: `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`, name: f.name, original: f,
      previewUrl: f.type.startsWith("image/") ? URL.createObjectURL(f) : null, originalSize: f.size, size: null, compressed: false,
      state: "waiting", progress: 0, key: newIdempotencyKey(),
    }));
    itemsRef.current = [...itemsRef.current, ...next];
    setItems((xs) => [...xs, ...next]);
    queue.current.push(...next.map((n) => n.id));
    pump();
  };

  const retry = (id: string) => {
    patch(id, { state: "waiting", error: undefined, progress: 0 });
    queue.current.push(id);
    pump();
  };

  const dismiss = (id: string) => {
    setItems((xs) => {
      const it = xs.find((x) => x.id === id);
      if (it?.previewUrl) URL.revokeObjectURL(it.previewUrl);
      return xs.filter((x) => x.id !== id);
    });
  };

  return { items, add, retry, dismiss, busy: items.some((i) => i.state !== "done" && i.state !== "failed") };
}

/**
 * Rahoon's drop zone: drag files onto it or choose them. `compact` is the one-line version used inside a list row.
 * Keyboard: the button opens the file picker.
 */
export function FileDropzone({ onFiles, accept, multiple = true, title, hint, icon = "upload_file", compact, disabled, buttonLabel = "اختيار ملفات", className }: {
  onFiles: (files: File[]) => void;
  accept: string;
  multiple?: boolean;
  title: ReactNode;
  hint?: ReactNode;
  icon?: string;
  compact?: boolean;
  disabled?: boolean;
  buttonLabel?: string;
  className?: string;
}) {
  const id = useId();
  const input = useRef<HTMLInputElement>(null);
  const [over, setOver] = useState(false);
  const take = (list: FileList | null) => {
    const files = Array.from(list ?? []);
    if (files.length) onFiles(multiple ? files : files.slice(0, 1));
  };
  const drag = {
    onDragOver: (e: DragEvent) => {
      if (disabled) return;
      e.preventDefault();
      setOver(true);
    },
    onDragLeave: () => setOver(false),
    onDrop: (e: DragEvent) => {
      e.preventDefault();
      setOver(false);
      if (!disabled) take(e.dataTransfer.files);
    },
  };
  return (
    <div {...drag}
      className={cn("flex rounded-lg border-2 border-dashed transition-colors",
        over ? "border-rust bg-rust-50" : "border-rust-200 bg-warm hover:border-rust",
        compact ? "flex-wrap items-center gap-3 px-3 py-2.5" : "flex-col items-center gap-2 px-4 py-7 text-center",
        disabled && "pointer-events-none opacity-60", className)}>
      <input ref={input} id={id} type="file" accept={accept} multiple={multiple} className="sr-only" tabIndex={-1} aria-hidden="true"
        onChange={(e) => {
          take(e.target.files);
          e.target.value = "";
        }} />
      <span className={cn("flex flex-none items-center justify-center rounded-full bg-white text-rust shadow-1", compact ? "size-9" : "size-12")}>
        <Icon name={icon} size={compact ? 20 : 26} />
      </span>
      <span className={cn("flex min-w-0 flex-col", compact ? "flex-1 text-start" : "items-center gap-0.5")}>
        <strong className={compact ? "text-14" : "text-16"}>{title}</strong>
        {hint ? <span className="text-12 leading-5 text-muted">{hint}</span> : null}
      </span>
      <button type="button" onClick={() => input.current?.click()} disabled={disabled}
        className={cn("inline-flex min-h-10 items-center gap-1.5 rounded-sm border border-line-strong bg-white px-4 text-14 font-semibold text-ink hover:border-ink", !compact && "mt-1")}>
        <Icon name="folder_open" size={18} />
        {buttonLabel}
      </button>
    </div>
  );
}

const STATE_TEXT: Record<UploadState, string> = { waiting: "في الانتظار", preparing: "تجهيز وضغط…", uploading: "جارٍ الرفع", done: "تم الرفع", failed: "لم يُرفع" };

/** The files on their way: thumbnail, name, size before → after compression, progress, and retry/remove on failure. */
export function UploadList({ items, onRetry, onDismiss, className }: { items: UploadItem[]; onRetry: (id: string) => void; onDismiss: (id: string) => void; className?: string }) {
  if (items.length === 0) return null;
  return (
    <ul aria-live="polite" className={cn("m-0 flex list-none flex-col gap-2 p-0", className)}>
      {items.map((i) => {
        const pct = Math.round(i.progress * 100);
        const failed = i.state === "failed";
        return (
          <li key={i.id} className={cn("flex items-center gap-3 rounded-md border bg-white p-2.5", failed ? "border-err-line" : "border-line")}>
            <span className="flex size-11 flex-none items-center justify-center overflow-hidden rounded-sm bg-subtle text-muted">
              {i.previewUrl ? (
                // eslint-disable-next-line @next/next/no-img-element -- local preview of the chosen file
                <img src={i.previewUrl} alt="" className="size-full object-cover" />
              ) : (
                <Icon name={/\.pdf$/i.test(i.name) ? "picture_as_pdf" : "description"} size={22} />
              )}
            </span>
            <span className="flex min-w-0 flex-1 flex-col gap-1">
              <span className="flex items-center justify-between gap-2">
                <span className="truncate text-14 font-semibold" dir="auto">{i.name}</span>
                <span className={cn("flex-none text-12", failed ? "text-err" : i.state === "done" ? "text-ok" : "text-muted")}>
                  {i.state === "uploading" ? `${STATE_TEXT.uploading} ${pct}%` : STATE_TEXT[i.state]}
                </span>
              </span>
              {failed ? (
                <span className="text-12 text-err">{i.error}</span>
              ) : (
                <>
                  <span className="h-1.5 overflow-hidden rounded-pill bg-track" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={pct} aria-label={`رفع ${i.name}`}>
                    <span className={cn("block h-full rounded-pill transition-[width] duration-200", i.state === "done" ? "bg-ok" : "bg-rust", i.state === "preparing" && "w-1/4 animate-rh-pulse")}
                      style={i.state === "preparing" ? undefined : { width: `${i.state === "done" ? 100 : pct}%` }} />
                  </span>
                  <span className="text-12 text-muted">
                    {i.size !== null && i.compressed ? (
                      <>من {formatBytes(i.originalSize)} إلى {formatBytes(i.size)} · وفّرنا <bdi dir="ltr">{Math.round((1 - i.size / i.originalSize) * 100)}%</bdi></>
                    ) : formatBytes(i.size ?? i.originalSize)}
                  </span>
                </>
              )}
            </span>
            {failed ? (
              <span className="flex flex-none gap-1">
                <button type="button" onClick={() => onRetry(i.id)} aria-label={`إعادة رفع ${i.name}`} className="inline-flex size-9 items-center justify-center rounded-sm text-rust hover:bg-rust-50">
                  <Icon name="refresh" size={20} />
                </button>
                <button type="button" onClick={() => onDismiss(i.id)} aria-label={`إزالة ${i.name}`} className="inline-flex size-9 items-center justify-center rounded-sm text-muted hover:bg-subtle">
                  <Icon name="close" size={20} />
                </button>
              </span>
            ) : null}
          </li>
        );
      })}
    </ul>
  );
}

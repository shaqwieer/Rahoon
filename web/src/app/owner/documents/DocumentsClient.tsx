"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";
import type { OwnerDocument } from "@/components/owner/types";
import { TONE_ICON, TONE_TEXT, toTone } from "@/components/owner/ui";
import { useSubmit } from "@/components/owner/useSubmit";
import { ServerText, useOwnerCopy } from "@/components/owner/values";
import { Alert, Button, DateText, Icon } from "@/components/ui";
import { apiSend, apiUpload } from "@/lib/api/client";
import { cn } from "@/lib/cn";

const MAX_BYTES = 20 * 1024 * 1024;

/** D04 list: requested/rejected documents as action cards, the rest as status rows. One live region for results. */
export function DocumentsClient({ items }: { items: OwnerDocument[] }) {
  const c = useOwnerCopy();
  const [announce, setAnnounce] = useState<string | null>(null);
  const actionable = items.filter((d) => d.canUpload);
  const rest = items.filter((d) => !d.canUpload);
  return (
    <>
      <div role="status" aria-live="polite">
        {announce ? (
          <Alert tone="ok" role="none">
            {announce}
          </Alert>
        ) : null}
      </div>
      {actionable.map((d) => (
        <DocRequestCard key={d.id} doc={d} onUploaded={() => setAnnounce(c.docs.uploaded(d.title))} />
      ))}
      {rest.length ? (
        <ul className="m-0 flex list-none flex-col gap-3 p-0">
          {rest.map((d) => {
            const tone = toTone(d.tone);
            return (
              <li key={d.id} className="flex min-h-12 items-center gap-3 rounded-[12px] border border-line bg-white px-4 py-3.5">
                <Icon name={TONE_ICON[tone === "err" || tone === "info" ? "neutral" : tone]} size={24} className={TONE_TEXT[tone]} />
                <div className="flex min-w-0 flex-1 flex-col">
                  <ServerText as="strong" text={d.title} className="text-16" />
                  <ServerText text={d.status} className={cn("text-14 font-semibold", TONE_TEXT[tone])} />
                </div>
              </li>
            );
          })}
        </ul>
      ) : null}
    </>
  );
}

function DocRequestCard({ doc, onUploaded }: { doc: OwnerDocument; onUploaded: () => void }) {
  const c = useOwnerCopy();
  const D = c.docs;
  const router = useRouter();
  const fileRef = useRef<HTMLInputElement>(null);
  const cameraRef = useRef<HTMLInputElement>(null);
  const upload = useSubmit();
  const help = useSubmit();
  const [helpSent, setHelpSent] = useState(false);
  const [sizeError, setSizeError] = useState<string | null>(null);
  const rejected = doc.rawStatus === "Rejected";
  const titleId = `doc-${doc.id}`;
  const errorId = `doc-${doc.id}-error`;

  const onFile = async (input: HTMLInputElement | null) => {
    const file = input?.files?.[0];
    if (!input || !file) return;
    input.value = "";
    if (file.size > MAX_BYTES) {
      setSizeError(D.tooLarge);
      return;
    }
    setSizeError(null);
    const fd = new FormData();
    fd.append("file", file);
    fd.append("documentId", doc.id);
    const res = await upload.run((k) => apiUpload<{ status: string; version: number }>("/owner/documents/upload", fd, { idempotencyKey: k }));
    if (res.ok) {
      onUploaded();
      router.refresh();
    }
  };

  const askHelp = async () => {
    const res = await help.run((k) => apiSend<{ sent: boolean }>("POST", `/owner/documents/${doc.id}/help`, undefined, { idempotencyKey: k }));
    if (res.ok) setHelpSent(true);
  };

  const error = sizeError ?? upload.error;
  return (
    <article aria-labelledby={titleId} className={cn("flex flex-col gap-2.5 rounded-[12px] border bg-white p-4", rejected ? "border-err-line" : "border-line")}>
      <div className="flex items-center gap-2.5">
        <Icon name={rejected ? "undo" : "upload_file"} size={24} className={rejected ? "text-err" : "text-info"} />
        <h2 id={titleId} className="m-0 flex-1 text-17 font-bold">
          <ServerText text={doc.title} />
        </h2>
      </div>
      {rejected && (doc.reason || doc.help) ? (
        <div className="rounded-[8px] bg-err-bg p-3 text-16 leading-[26px]">
          <strong>{D.whyAgain}</strong>
          <br />
          <ServerText text={[doc.reason, doc.help].filter(Boolean).join(" ")} />
        </div>
      ) : null}
      <span className="text-15 text-muted">
        {doc.dueOn ? (
          <>
            {D.until} <DateText value={doc.dueOn} /> ·{" "}
          </>
        ) : null}
        {D.requestedBy} <ServerText text={doc.requestedBy} />
      </span>
      <div className="grid grid-cols-2 gap-2.5">
        <Button size="lg" icon="upload_file" className="min-h-[52px] rounded-[8px]" loading={upload.busy} loadingLabel={D.uploading} onClick={() => fileRef.current?.click()} aria-describedby={error ? errorId : `${titleId}-hint`}>
          {D.upload}
        </Button>
        <Button size="lg" variant="secondary" icon="photo_camera" className="min-h-[52px] rounded-[8px]" disabled={upload.busy} onClick={() => cameraRef.current?.click()} aria-describedby={error ? errorId : `${titleId}-hint`}>
          {D.camera}
        </Button>
      </div>
      <input ref={fileRef} type="file" accept="application/pdf,image/*" className="sr-only" tabIndex={-1} aria-hidden="true" onChange={(e) => void onFile(e.currentTarget)} />
      <input ref={cameraRef} type="file" accept="image/*" capture="environment" className="sr-only" tabIndex={-1} aria-hidden="true" onChange={(e) => void onFile(e.currentTarget)} />
      <span id={`${titleId}-hint`} className="text-13 text-muted">
        {D.fileHint}
      </span>
      <div aria-live="assertive" id={errorId}>
        {error ? (
          <Alert tone="err" role="none" compact>
            {error}
          </Alert>
        ) : null}
      </div>
      <div aria-live="polite">
        {helpSent ? (
          <Alert tone="ok" role="none" compact action={<Link href="/owner/messages">{D.openMessages}</Link>}>
            {D.helpSent}
          </Alert>
        ) : (
          <>
            <Button variant="text" size="lg" className="self-start px-0 text-15" loading={help.busy} onClick={() => void askHelp()}>
              {D.help}
            </Button>
            {help.error ? (
              <Alert tone="err" role="none" compact>
                {help.error}
              </Alert>
            ) : null}
          </>
        )}
      </div>
    </article>
  );
}

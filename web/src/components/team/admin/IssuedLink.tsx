"use client";

import { useState } from "react";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { dayTime } from "@/lib/market/format";
import { absoluteLink, type InvitationIssued } from "@/lib/team/admin";

/**
 * The one-time invitation link, shown once. Nothing was e-mailed (no provider is configured): the page says so plainly
 * and the inviter hands the link over themselves.
 */
export function IssuedLink({ issued }: { issued: InvitationIssued }) {
  // Rendered only in the browser, after the inviter's action, so the site's origin is known.
  const link = absoluteLink(issued.link);
  const [copied, setCopied] = useState(false);
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(link);
      setCopied(true);
    } catch {
      setCopied(false);
    }
  };
  return (
    <Alert tone="warn" title="لم يُرسل الرابط إلى أحد">
      <p className="m-0 mb-2 text-14">{issued.delivery}</p>
      <label className="flex flex-col gap-1 text-13 font-semibold">
        رابط الدعوة (صالح حتى {dayTime(issued.expiresAt)})
        <input readOnly value={link} dir="ltr" onFocus={(e) => e.currentTarget.select()}
          className="min-h-11 w-full rounded-sm border border-line-strong bg-white px-3 font-mono text-13" />
      </label>
      <div className="mt-2 flex items-center gap-3">
        <Button size="sm" variant="secondary" onClick={() => void copy()}>نسخ الرابط</Button>
        <span role="status" className="text-13">{copied ? "نُسخ." : ""}</span>
      </div>
    </Alert>
  );
}

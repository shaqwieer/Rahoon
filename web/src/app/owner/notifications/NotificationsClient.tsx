"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import type { OwnerNotification } from "@/components/owner/types";
import { TONE_TEXT, toTone } from "@/components/owner/ui";
import { useSubmit } from "@/components/owner/useSubmit";
import { ServerText, useOwnerCopy } from "@/components/owner/values";
import { Alert, Button, DateText, Icon } from "@/components/ui";
import { apiSend } from "@/lib/api/client";
import { cn } from "@/lib/cn";

const CATEGORY_ICON: Record<string, string> = {
  document: "description",
  message: "chat",
  appointment: "event",
  offer: "local_offer",
  payment: "payments",
  agreement: "handshake",
  complaint: "support_agent",
};

/** Owner notifications (`/api/notifications`): open marks one as read and follows its owner-portal link. */
export function NotificationsClient({ items, unread }: { items: OwnerNotification[]; unread: number }) {
  const c = useOwnerCopy();
  const N = c.notifications;
  const router = useRouter();
  const all = useSubmit();
  const one = useSubmit();
  const [pending, setPending] = useState<string | null>(null);

  const open = async (n: OwnerNotification) => {
    const target = n.link && n.link.startsWith("/owner") ? n.link : null;
    if (!n.read) {
      setPending(n.id);
      await one.run((k) => apiSend("POST", `/notifications/${n.id}/read`, undefined, { idempotencyKey: k }));
      setPending(null);
    }
    if (target) router.push(target);
    else router.refresh();
  };

  const markAll = async () => {
    const res = await all.run((k) => apiSend("POST", "/notifications/read-all", undefined, { idempotencyKey: k }));
    if (res.ok) router.refresh();
  };

  return (
    <>
      {unread > 0 ? (
        <Button variant="text" size="lg" className="self-end px-1 text-15" loading={all.busy} onClick={() => void markAll()}>
          {N.markAll}
        </Button>
      ) : null}
      <div aria-live="assertive">{all.error ?? one.error ? <Alert tone="err" role="none">{all.error ?? one.error}</Alert> : null}</div>
      <ul className="m-0 flex list-none flex-col gap-2.5 p-0">
        {items.map((n) => {
          const tone = toTone(n.tone);
          return (
            <li key={n.id}>
              <button
                type="button"
                onClick={() => void open(n)}
                aria-busy={pending === n.id || undefined}
                className={cn(
                  "flex min-h-14 w-full items-start gap-3 rounded-[12px] border bg-white px-4 py-3.5 text-start text-ink hover:bg-subtle",
                  n.read ? "border-line" : "border-line-strong bar-start",
                )}
              >
                <Icon name={CATEGORY_ICON[n.category] ?? "notifications"} size={24} className={tone === "neutral" ? "text-charcoal" : TONE_TEXT[tone]} />
                <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                  <span className="flex items-center gap-2">
                    <ServerText as="strong" text={n.title} className={cn("text-16", n.read ? "font-semibold" : "font-bold")} />
                    {n.read ? null : <span className="rounded-pill bg-rust-50 px-2 text-12 font-bold text-rust-700">{N.unread}</span>}
                  </span>
                  {n.body ? <ServerText text={n.body} className="text-15 leading-6 text-charcoal" /> : null}
                  <span className="text-13 text-muted">
                    <DateText value={n.createdAt} mode="datetime" />
                  </span>
                </span>
                {n.link && n.link.startsWith("/owner") ? <Icon name="chevron_left" size={22} mirror className="self-center text-muted" /> : null}
              </button>
            </li>
          );
        })}
      </ul>
    </>
  );
}

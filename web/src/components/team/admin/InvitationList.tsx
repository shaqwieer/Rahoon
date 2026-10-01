"use client";

import { useState } from "react";
import { ActionError, useTeamAction } from "@/components/market/team/useTeamAction";
import { Badge } from "@/components/market/ui";
import { Button } from "@/components/ui/Button";
import { apiSend, isApiError } from "@/lib/api/client";
import { dayTime } from "@/lib/market/format";
import { INVITATION_STATUS, type InvitationIssued, type InvitationRow } from "@/lib/team/admin";
import { IssuedLink } from "./IssuedLink";

/** Invitations: status, roles, expiry; renew (new one-time link, the old one stops working) or revoke. */
export function InvitationList({ items, canManage }: { items: InvitationRow[]; canManage: boolean }) {
  const { busy, error, run } = useTeamAction();
  const [issued, setIssued] = useState<{ id: string; data: InvitationIssued } | null>(null);
  const [renewError, setRenewError] = useState<string | null>(null);
  const [renewing, setRenewing] = useState<string | null>(null);

  const renew = async (id: string) => {
    setRenewing(id);
    setRenewError(null);
    try {
      // No idempotency replay for links: each renew deliberately issues a new token.
      const data = await apiSend<InvitationIssued>("POST", `/team/admin/invitations/${id}/renew`, {});
      setIssued({ id, data });
    } catch (err) {
      setRenewError(isApiError(err) ? err.title || "تعذّر التجديد." : "تعذّر الاتصال. أعد المحاولة.");
    } finally {
      setRenewing(null);
    }
  };

  if (items.length === 0) return <p className="m-0 text-14 text-muted">لا توجد دعوات.</p>;
  return (
    <div className="flex flex-col gap-3">
      <ActionError error={error} />
      {renewError ? <p role="alert" className="m-0 text-14 text-err">{renewError}</p> : null}
      <ul className="m-0 flex list-none flex-col gap-2 p-0">
        {items.map((i) => {
          const st = INVITATION_STATUS[i.status];
          const open = i.status === "pending" || i.status === "expired";
          return (
            <li key={i.id} className="flex flex-col gap-2 rounded-md border border-line p-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span className="flex flex-col">
                  <strong className="text-15">{i.fullName}</strong>
                  <span className="text-13 text-muted"><bdi dir="ltr">{i.email}</bdi> · <bdi dir="ltr">{i.phoneMasked}</bdi></span>
                </span>
                <Badge tone={st.tone}>{st.label}</Badge>
              </div>
              <span className="text-13">الأدوار: {i.roles.join("، ")}</span>
              <span className="text-12 text-muted">
                دعاه {i.invitedByLabel} · {dayTime(i.createdAt)}
                {open ? ` · تنتهي ${dayTime(i.expiresAt)}` : i.acceptedAt ? ` · قُبلت ${dayTime(i.acceptedAt)}` : i.revokedAt ? ` · أُلغيت ${dayTime(i.revokedAt)}` : ""}
              </span>
              {issued?.id === i.id ? <IssuedLink issued={issued.data} /> : null}
              {canManage && open ? (
                <div className="flex flex-wrap gap-2">
                  <Button size="sm" variant="secondary" loading={renewing === i.id} onClick={() => void renew(i.id)}>
                    {i.status === "expired" ? "إصدار رابط جديد" : "تجديد الرابط"}
                  </Button>
                  {i.status === "pending" ? (
                    <Button size="sm" variant="text" loading={busy === `rv-${i.id}`} onClick={() => void run(`rv-${i.id}`, "POST", `/team/admin/invitations/${i.id}/revoke`)}>إلغاء الدعوة</Button>
                  ) : null}
                </div>
              ) : null}
            </li>
          );
        })}
      </ul>
    </div>
  );
}

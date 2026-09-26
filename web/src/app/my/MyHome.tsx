"use client";

import { DebtorTop } from "@/components/shell/OwnerShell";
import { Button } from "@/components/ui/Button";
import { KeyValueList } from "@/components/ui/KeyValueList";
import { SkipLink } from "@/components/ui/SkipLink";
import { IntegrationStateTag, type IntegrationState } from "@/components/ui/Status";
import { EmptyState } from "@/components/ui/SystemState";
import { Alert } from "@/components/ui/Alert";
import { Icon } from "@/components/ui/Icon";
import { RequestStatusChip, useRequestCopy, useRequestSubmit, WaitingOnLine } from "@/components/individual/ui";
import { apiSend } from "@/lib/api/client";
import type { MyRequestListItem } from "@/lib/api/requests";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { formatDate } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";
import { hardNavigate } from "@/lib/navigation";
import { useState } from "react";

export interface IndividualAccount {
  idMasked: string;
  idType: "citizen" | "resident";
  phoneMasked: string;
  identityAssurance: string;
  termsVersion: string | null;
  termsAcceptedAt: string | null;
  awarenessOptIn: boolean;
  nationalIdProvider: IntegrationState;
}

/** Mobile-first individual home: «طلباتي» (each request with its status and «ننتظر») and the account card. No bottom nav yet. */
export function MyHome({ account, requests }: { account: IndividualAccount; requests: MyRequestListItem[] }) {
  const { t } = useI18n();
  const M = t.individual.my;
  const c = useRequestCopy();
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const start = useRequestSubmit();

  const startRequest = async () => {
    const res = await start.run((key) => apiSend<{ reference: string }>("POST", "/my/requests", undefined, { idempotencyKey: key }));
    if (res.ok) router.push(`/my/requests/${res.data.reference}/apply?step=1`);
  };

  const signOut = async () => {
    setBusy(true);
    try {
      const res = await apiSend<{ next?: string }>("POST", "/auth/logout");
      hardNavigate(res?.next, "/");
    } catch {
      hardNavigate("/");
    }
  };

  return (
    <div className="flex min-h-dvh flex-col bg-warm">
      <SkipLink />
      <DebtorTop
        title={M.title}
        sub={M.sub}
        showBell={false}
        trailing={
          <Button variant="text" size="lg" onClick={() => void signOut()} loading={busy} className="text-15">
            {M.signOut}
          </Button>
        }
      />
      <main id="main" tabIndex={-1} className="mx-auto flex w-full max-w-[560px] flex-1 flex-col gap-5 px-[22px] py-6 outline-none">
        <h1 className="m-0 text-26 leading-[38px] font-bold">{M.welcome}</h1>

        <section aria-labelledby="requests-h" className="flex flex-col gap-3">
          <h2 id="requests-h" className="m-0 text-19 font-bold">
            {M.requestsTitle}
          </h2>
          {requests.length === 0 ? (
            <EmptyState icon="description" title={c.home.emptyTitle} body={c.home.emptyBody} headingLevel={3} />
          ) : (
            <ul className="m-0 flex list-none flex-col gap-2.5 p-0">
              {requests.map((r) => {
                const href = r.status === "draft" ? `/my/requests/${r.reference}/apply?step=1` : `/my/requests/${r.reference}`;
                return (
                  <li key={r.reference}>
                    <Link
                      href={href}
                      className="flex flex-col gap-2 rounded-[12px] border border-line bg-white p-4 text-ink no-underline hover:bg-subtle hover:text-ink"
                    >
                      <span className="flex items-center justify-between gap-2">
                        <bdi dir="ltr" className="font-mono text-14 text-muted">
                          {r.reference}
                        </bdi>
                        <RequestStatusChip status={r.status} />
                      </span>
                      <strong className="text-17">{r.institutionName || c.home.draftLender}</strong>
                      {r.status === "draft" ? null : <WaitingOnLine waitingOn={r.waitingOn} className="text-14" />}
                      <span className="flex items-center gap-1 text-15 font-semibold text-rust-700">
                        {r.status === "draft" ? c.home.continueDraft : c.home.open}
                        <Icon name="chevron_left" size={20} mirror />
                      </span>
                    </Link>
                  </li>
                );
              })}
            </ul>
          )}
          {start.error ? <Alert tone="err">{start.error}</Alert> : null}
          <Button size="xl" fullWidth icon="add" loading={start.busy} loadingLabel={c.home.starting} onClick={() => void startRequest()}>
            {c.home.start}
          </Button>
          <p className="m-0 text-14 text-muted">{c.home.separateHint}</p>
        </section>

        <section aria-labelledby="account-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
          <h2 id="account-h" className="m-0 text-17 font-bold">
            {M.accountTitle}
          </h2>
          <KeyValueList
            rows={[
              { key: M.idLabel, value: <bdi dir="ltr" className="font-mono">{account.idMasked}</bdi> },
              { key: M.phoneLabel, value: <bdi dir="ltr" className="font-mono">{account.phoneMasked}</bdi> },
              { key: M.identityLabel, value: <span className="text-14 leading-[22px]">{M.identitySelf}</span> },
              { key: M.digitalId, value: <IntegrationStateTag state={account.nationalIdProvider} /> },
              {
                key: M.termsLabel,
                value: account.termsVersion && account.termsAcceptedAt ? (
                  <span className="text-14">{M.termsAccepted(account.termsVersion, formatDate(account.termsAcceptedAt))}</span>
                ) : (
                  "—"
                ),
              },
            ]}
          />
        </section>
      </main>
    </div>
  );
}

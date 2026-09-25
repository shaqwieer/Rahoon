"use client";

import { DebtorTop } from "@/components/shell/OwnerShell";
import { Button } from "@/components/ui/Button";
import { KeyValueList } from "@/components/ui/KeyValueList";
import { SkipLink } from "@/components/ui/SkipLink";
import { IntegrationStateTag, type IntegrationState } from "@/components/ui/Status";
import { EmptyState } from "@/components/ui/SystemState";
import { apiSend } from "@/lib/api/client";
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

/** Mobile-first individual home: «طلباتي» (empty until step 5) and the account card. No bottom nav yet. */
export function MyHome({ account }: { account: IndividualAccount }) {
  const { t } = useI18n();
  const M = t.individual.my;
  const [busy, setBusy] = useState(false);

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
          <EmptyState icon="description" title={M.requestsEmptyTitle} body={`${M.requestsEmptyBody} ${M.requestsSoon}`} headingLevel={3} />
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

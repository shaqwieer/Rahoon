import type { Metadata } from "next";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { getOwnerInvitation } from "@/lib/api/owner";
import { getServerDictionary } from "@/lib/i18n/server";
import { InviteNotice, OwnerFrame, RevealAction } from "./OwnerFrame";

export async function generateMetadata(): Promise<Metadata> {
  const { t } = await getServerDictionary();
  return { title: t.auth.invite.eyebrow, referrer: "no-referrer" };
}

/** D01a — owner invitation landing (trust card first, no data entry on this step). */
export default async function InvitePage({ params }: PageProps<"/invite/[token]">) {
  const { token } = await params;
  const { t } = await getServerDictionary();
  const I = t.auth.invite;
  const inv = await getOwnerInvitation(token);
  const verifyHref = `/invite/${encodeURIComponent(token)}/verify`;

  if (inv.status === "error") {
    return (
      <OwnerFrame>
        <InviteNotice icon="wifi_off" tone="warn" title={t.sysStates.errorTitle} body={I.loadError} primary={{ label: t.common.retry, href: `/invite/${encodeURIComponent(token)}` }} />
      </OwnerFrame>
    );
  }
  if (inv.status === "expired") {
    return (
      <OwnerFrame>
        <InviteNotice
          icon="link_off"
          tone="warn"
          title={I.expiredTitle}
          body={I.expiredBody}
          primary={{ label: I.requestNew, revealNote: I.requestNewNote }}
          secondary={{ label: I.contactLender, revealNote: I.requestNewNote }}
        />
      </OwnerFrame>
    );
  }
  if (inv.status === "invalid") {
    return (
      <OwnerFrame>
        <InviteNotice icon="link_off" tone="err" title={I.invalidTitle} body={I.invalidBody} secondary={{ label: I.contactLender, revealNote: I.requestNewNote }} />
      </OwnerFrame>
    );
  }
  if (inv.status === "used") {
    return (
      <OwnerFrame>
        <InviteNotice icon="verified" tone="info" title={I.usedTitle} body={I.usedBody} primary={{ label: I.usedAction, href: verifyHref }} />
      </OwnerFrame>
    );
  }

  const lender = inv.lenderName ?? "";
  return (
    <OwnerFrame>
      <span className="text-15 text-muted">{I.eyebrow}</span>
      <h1 className="m-0 text-28 leading-10 font-bold">{I.title(lender)}</h1>
      <p className="m-0 text-18 leading-[30px] text-charcoal">{I.body}</p>
      <section aria-labelledby="trust-h" className="flex flex-col gap-2.5 rounded-[12px] border border-line bg-white p-4">
        <h2 id="trust-h" className="m-0 flex items-center gap-2 text-16 font-bold">
          <Icon name="verified" size={22} className="text-ok" />
          {I.trustTitle}
        </h2>
        <ul className="m-0 flex list-none flex-col gap-1 p-0 text-16 leading-[26px]">
          <li>
            • {I.trustDomain}{" "}
            <bdi dir="ltr" className="font-mono text-14">
              rahoon.sa
            </bdi>
          </li>
          <li>• {I.trustNoPayment}</li>
          {inv.invitationCode ? (
            <li>
              • {I.trustCode}{" "}
              <bdi dir="ltr" className="font-mono text-15 font-semibold">
                {inv.invitationCode}
              </bdi>{" "}
              {I.trustCodeTail}
            </li>
          ) : null}
        </ul>
      </section>
      <Button size="xl" fullWidth href={verifyHref} className="mt-auto">
        {I.continue}
      </Button>
      <div className="flex justify-center">
        <RevealAction label={I.unknown} note={I.unknownNote} variant="text" />
      </div>
    </OwnerFrame>
  );
}

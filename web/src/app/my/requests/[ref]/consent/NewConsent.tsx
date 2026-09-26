"use client";

import { useRouter } from "next/navigation";
import { ConsentBox } from "../apply/RequestWizard";
import { IndividualFrame, useRequestCopy } from "@/components/individual/ui";
import { Button } from "@/components/ui/Button";
import type { MyRequestDetail } from "@/lib/api/requests";

export function NewConsent({ detail }: { detail: MyRequestDetail }) {
  const c = useRequestCopy();
  const router = useRouter();
  const back = `/my/requests/${encodeURIComponent(detail.reference)}`;
  return (
    <IndividualFrame title={c.consent.title} sub={<bdi dir="ltr">{detail.reference}</bdi>} back={{ href: back }}>
      <h1 className="m-0 text-24 leading-9 font-bold">{c.wizard.docs.consentTitle}</h1>
      <ConsentBox detail={detail} onRecorded={() => router.push(back)} />
      {detail.consent ? (
        <Button href={back} size="xl" fullWidth>
          {c.submitted.track}
        </Button>
      ) : null}
    </IndividualFrame>
  );
}

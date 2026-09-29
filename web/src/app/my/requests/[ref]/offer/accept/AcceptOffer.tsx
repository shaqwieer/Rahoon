"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { IndividualFrame, Panel, useRequestCopy, useRequestSubmit } from "@/components/individual/ui";
import { Alert, SandboxCodeBox } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Checkbox } from "@/components/ui/Field";
import { OtpInput } from "@/components/ui/OtpInput";
import { Tag } from "@/components/ui/Status";
import { apiSend } from "@/lib/api/client";
import { autoOtpCode, type OtpIssued } from "@/lib/api/otp";
import type { MyRequestDetail } from "@/lib/api/requests";
import { useI18n } from "@/lib/i18n/client";
import { useSmsConfirmation } from "@/lib/sms-confirmation";
import { OfferTerms } from "../OfferView";

export function AcceptOffer({ detail }: { detail: MyRequestDetail }) {
  const c = useRequestCopy();
  const A = c.accept;
  const { t } = useI18n();
  const router = useRouter();
  const sms = useSmsConfirmation();
  const ref = encodeURIComponent(detail.reference);
  const [checked, setChecked] = useState(false);
  const [checkError, setCheckError] = useState<string | null>(null);
  const [sent, setSent] = useState<{ destination: string; sandbox: string | null } | null>(null);
  const [code, setCode] = useState("");
  const send = useRequestSubmit();
  const confirm = useRequestSubmit();

  const requestCode = async () => {
    if (!checked) {
      setCheckError(A.checkError);
      return;
    }
    const res = await send.run((key) => apiSend<OtpIssued>("POST", `/my/requests/${ref}/offer/accept/otp`, undefined, { idempotencyKey: key }));
    if (!res.ok) return;
    // SMS confirmation off: record the acceptance straight away; the code step appears only if that fails.
    const auto = autoOtpCode(res.data);
    if (auto && (await accept(auto))) return;
    setSent({ destination: res.data.destination, sandbox: res.data.sandboxCode ?? null });
  };

  const accept = async (value = code) => {
    const res = await confirm.run((key) => apiSend("POST", `/my/requests/${ref}/offer/respond`, { kind: "accept", code: value }, { idempotencyKey: key }));
    if (res.ok) {
      router.push(`/my/requests/${ref}`);
      router.refresh();
    } else setCode("");
    return res.ok;
  };

  return (
    <IndividualFrame title={A.title} sub={<bdi dir="ltr">{detail.reference}</bdi>} back={{ href: `/my/requests/${ref}/offer` }}>
      <h1 className="m-0 text-24 leading-9 font-bold">{A.heading}</h1>
      {detail.offer ? (
        <Panel>
          <OfferTerms offer={detail.offer} />
        </Panel>
      ) : null}
      <Panel className="border-line-strong">
        <Tag tone="warn" icon="rule" className="self-start">
          {A.draft}
        </Tag>
        <p className="m-0 rounded-md bg-subtle p-3 text-15 leading-7">{detail.offerAcceptText?.text}</p>
        <p className="m-0 text-14 text-muted">{A.notSignature}</p>
        <Checkbox
          boxSize={24}
          checked={checked}
          disabled={Boolean(sent)}
          onChange={(e) => {
            setChecked(e.target.checked);
            if (e.target.checked) setCheckError(null);
          }}
          error={checkError ?? undefined}
          label={A.check}
        />
      </Panel>
      {send.error ? <Alert tone="err">{send.error}</Alert> : null}
      {!sent ? (
        <Button size="xl" fullWidth icon={sms ? "sms" : "check"} loading={send.busy || confirm.busy} onClick={() => void requestCode()}>
          {sms ? A.sendCode : A.confirm}
        </Button>
      ) : (
        <div className="flex flex-col gap-3">
          <p className="m-0 text-15">
            {c.wizard.docs.codeSentTo}{" "}
            <bdi dir="ltr" className="font-mono">
              {sent.destination}
            </bdi>
          </p>
          {sent.sandbox ? <SandboxCodeBox title={t.sandbox.title} code={sent.sandbox} note={t.sandbox.note} /> : null}
          <OtpInput label={A.codeLabel} value={code} onChange={setCode} error={Boolean(confirm.error)} boxHeight={52} autoFocus />
          {confirm.error ? <Alert tone="err">{confirm.error}</Alert> : null}
          <Button size="xl" fullWidth loading={confirm.busy} disabled={code.length !== 6} onClick={() => void accept()}>
            {A.confirm}
          </Button>
        </div>
      )}
    </IndividualFrame>
  );
}

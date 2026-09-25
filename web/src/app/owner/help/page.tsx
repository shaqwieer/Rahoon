import { OwnerPage } from "@/components/owner/OwnerPage";
import { getOwnerContext, ownerMetadata } from "@/components/owner/server";
import { Card, TouchLink } from "@/components/owner/ui";
import { HardshipForm } from "./HardshipForm";

export const generateMetadata = () => ownerMetadata((c) => c.help.title);

/** D11 — hardship help: tell us early (reason optional), ask for a call; links to messages and complaints. */
export default async function OwnerHelpPage() {
  const { c, shell } = await getOwnerContext();
  const H = c.help;
  return (
    <OwnerPage shell={shell} title={H.title} active="help">
      <p className="m-0 text-18 leading-[30px]">{H.intro}</p>
      <HardshipForm />
      <Card as="section" aria-labelledby="help-more" className="mt-2 gap-0.5 px-4 py-3">
        <h2 id="help-more" className="m-0 mb-1 text-16 font-semibold">
          {H.more}
        </h2>
        <TouchLink href="/owner/messages" icon="chat">
          {H.messages}
        </TouchLink>
        <TouchLink href="/owner/complaints/new" icon="support_agent">
          {H.complaint}
        </TouchLink>
        <TouchLink href="/owner/complaints" icon="fact_check">
          {H.complaints}
        </TouchLink>
      </Card>
    </OwnerPage>
  );
}

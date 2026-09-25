import { OwnerPage } from "@/components/owner/OwnerPage";
import { getOwnerContext, ownerGet, ownerMetadata } from "@/components/owner/server";
import type { OwnerComplaint } from "@/components/owner/types";
import { ServerText } from "@/components/owner/values";
import { Button, DateText, EmptyState, Tag } from "@/components/ui";

export const generateMetadata = () => ownerMetadata((c) => c.complaint.listTitle);

const STATUS_TONE: Record<string, "ok" | "info" | "warn" | "neutral"> = {
  Received: "info",
  InReview: "info",
  AwaitingOwner: "warn",
  Resolved: "ok",
  Escalated: "info",
  Closed: "neutral",
};

/** D13 tracking — the owner's complaints/objections with status, reply due date and the written reply. */
export default async function OwnerComplaintsPage() {
  const { c, shell } = await getOwnerContext();
  const items = await ownerGet<OwnerComplaint[]>("/complaints");
  const K = c.complaint;
  return (
    <OwnerPage shell={shell} title={K.listTitle} backHref="/owner/help" backLabel={c.back} active="help">
      {items.length === 0 ? (
        <EmptyState icon="support_agent" title={K.emptyTitle} body={K.emptyBody} />
      ) : (
        <ul className="m-0 flex list-none flex-col gap-3 p-0">
          {items.map((x) => (
            <li key={x.reference}>
              <article aria-labelledby={`cmp-${x.reference}`} className="flex flex-col gap-1.5 rounded-[12px] border border-line bg-white px-4 py-3.5">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <h2 id={`cmp-${x.reference}`} className="m-0 text-16 font-bold">
                    {K.type[x.type] ?? x.type} ·{" "}
                    <bdi dir="ltr" className="font-mono text-15">
                      {x.reference}
                    </bdi>
                  </h2>
                  <Tag tone={STATUS_TONE[x.status] ?? "neutral"}>{K.status[x.status] ?? x.status}</Tag>
                </div>
                <ServerText text={x.subject} className="text-15 text-charcoal" />
                <span className="text-13 text-muted">
                  {K.submitted} <DateText value={x.submittedAt} />
                  {x.dueOn && !x.response ? (
                    <>
                      {" "}
                      · {K.dueOn} <DateText value={x.dueOn} />
                    </>
                  ) : null}
                </span>
                {x.response ? (
                  <div className="mt-1 flex flex-col gap-1 rounded-[8px] bg-subtle p-3">
                    <strong className="text-14">
                      {K.response}
                      {x.respondedAt ? (
                        <>
                          {" "}
                          · <DateText value={x.respondedAt} />
                        </>
                      ) : null}
                    </strong>
                    <ServerText as="p" text={x.response} className="m-0 text-15 leading-6 whitespace-pre-wrap" />
                  </div>
                ) : null}
              </article>
            </li>
          ))}
        </ul>
      )}
      <Button href="/owner/complaints/new" size="lg" variant="secondary" icon="add" className="min-h-[52px] rounded-[8px]">
        {K.new}
      </Button>
      <p className="m-0 text-13 leading-5 text-muted">{K.regulator}</p>
    </OwnerPage>
  );
}

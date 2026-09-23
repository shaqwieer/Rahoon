import { OwnerHeading, OwnerPage } from "@/components/owner/OwnerPage";
import { getOwnerContext, ownerGet, ownerMetadata } from "@/components/owner/server";
import type { OwnerClosure } from "@/components/owner/types";
import { ServerText } from "@/components/owner/values";
import { DateText, Icon } from "@/components/ui";
import { getServerDictionary } from "@/lib/i18n/server";

export const generateMetadata = () => ownerMetadata((c) => c.closure.closedTitle);

/** D14 — closed case: closure documents to download (owner file endpoint) and the read-only access window. */
export default async function OwnerClosurePage() {
  const { c, shell } = await getOwnerContext();
  const data = await ownerGet<OwnerClosure>("/closure");
  const { t } = await getServerDictionary();
  const C = c.closure;
  return (
    <OwnerPage shell={shell} title={t.owner.greeting(shell.firstName)} sub={shell.lenderName ? t.owner.sub(shell.lenderName) : undefined} active="docs" heading="none">
      {data.closed ? (
        <div className="flex flex-col gap-3.5 pt-1">
          <span className="flex size-14 items-center justify-center rounded-full bg-ok-bg">
            <Icon name="task_alt" size={32} className="text-ok" />
          </span>
          <OwnerHeading title={C.closedTitle} visible />
          {data.closedAt ? (
            <p className="m-0 text-18 leading-[30px]">
              {C.closedBody} <DateText value={data.closedAt} />
              {C.closedBodyTail}
            </p>
          ) : null}
          {data.documents.length === 0 ? <p className="m-0 text-16 text-muted">{C.noDocs}</p> : null}
          <ul className="m-0 flex list-none flex-col gap-3 p-0">
            {data.documents.map((d, i) => (
              <li key={d.fileVersionId ?? i} className="flex items-center gap-3 rounded-[12px] border border-line bg-white px-4 py-3.5">
                <Icon name="description" size={24} />
                <div className="flex min-w-0 flex-1 flex-col">
                  <ServerText as="strong" text={d.title} className="text-16" />
                  <span className="text-13 text-muted">{d.date ? <DateText value={d.date} /> : null}</span>
                </div>
                {d.fileVersionId ? (
                  <a
                    href={`/api/owner/files/${encodeURIComponent(d.fileVersionId)}`}
                    download
                    aria-label={C.download(d.title)}
                    className="inline-flex size-12 flex-none items-center justify-center rounded-[8px] border border-line-strong bg-white text-ink hover:bg-subtle hover:text-ink"
                  >
                    <Icon name="download" size={22} />
                  </a>
                ) : (
                  <span className="text-13 text-muted">{C.preparing}</span>
                )}
              </li>
            ))}
          </ul>
          {data.accessExpiresAt ? (
            <p className="m-0 text-14 leading-[22px] text-muted">
              {C.access} <DateText value={data.accessExpiresAt} />
              {C.accessTail}
            </p>
          ) : null}
        </div>
      ) : (
        <div className="flex flex-col gap-3 rounded-lg border border-line bg-white p-6">
          <span className="flex size-11 items-center justify-center rounded-full bg-subtle">
            <Icon name="folder_open" size={24} className="text-charcoal" />
          </span>
          <OwnerHeading title={C.openTitle} visible />
          <p className="m-0 text-16 leading-[26px] text-muted">{C.openBody}</p>
        </div>
      )}
    </OwnerPage>
  );
}

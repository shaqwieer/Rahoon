import { PublicFooter, PublicHeader } from "@/components/shell/Public";
import { Icon } from "@/components/ui/Icon";
import { getServerDictionary } from "@/lib/i18n/server";

const TERMS_VERSION = "terms-draft-2026-09";

/** Shared layout for the draft legal pages: clearly labelled as a draft pending legal approval. */
export async function LegalDraft({ title, body, facts }: { title: string; body: string; facts?: readonly string[] }) {
  const { t } = await getServerDictionary();
  const L = t.individual.legal;
  return (
    <>
      <PublicHeader />
      <main id="main" tabIndex={-1} className="mx-auto flex w-full max-w-[760px] flex-1 flex-col gap-5 px-5 py-10 outline-none md:py-16">
        <span className="flex w-fit items-center gap-1.5 rounded-full border border-warn-line bg-warn-bg px-3 py-1 text-13 font-semibold text-warn">
          <Icon name="edit_note" size={18} />
          {L.draft}
        </span>
        <h1 className="m-0 text-32 leading-[46px] font-bold">{title}</h1>
        <p className="m-0 text-17 leading-[29px] text-charcoal">{body}</p>
        {facts ? (
          <ul className="m-0 flex flex-col gap-2 ps-5 text-16 leading-[27px]">
            {facts.map((f) => (
              <li key={f}>{f}</li>
            ))}
          </ul>
        ) : null}
        <span className="text-13 text-muted">
          {L.version}: <bdi dir="ltr" className="font-mono">{TERMS_VERSION}</bdi>
        </span>
      </main>
      <PublicFooter />
    </>
  );
}

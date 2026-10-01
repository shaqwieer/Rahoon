import { M } from "@/components/market/copy";
import { Icon } from "@/components/ui/Icon";
import { PublicPage } from "./PublicPage";

/** Version of the draft terms and privacy text accepted at sign-in (kept in step with the API's TermsVersion). */
export const TERMS_VERSION = "terms-draft-2026-10";

/** Shared layout for the draft legal pages: clearly labelled as a draft pending legal approval. */
export function LegalDraft({ title, body, facts }: { title: string; body: string; facts: readonly string[] }) {
  return (
    <PublicPage title={title} lead={body}>
      <div className="flex flex-col gap-5">
        <span className="flex w-fit items-center gap-1.5 rounded-full border border-warn-line bg-warn-bg px-3 py-1 text-13 font-semibold text-warn">
          <Icon name="edit_note" size={18} />
          {M.legal.draft}
        </span>
        <ul className="m-0 flex flex-col gap-2 ps-5 text-16 leading-[28px]">
          {facts.map((f) => (
            <li key={f}>{f}</li>
          ))}
        </ul>
        <span className="text-13 text-muted">
          {M.legal.version}: <bdi dir="ltr" className="font-mono">{TERMS_VERSION}</bdi>
        </span>
      </div>
    </PublicPage>
  );
}

import type { Metadata } from "next";
import { LegalDraft } from "../LegalDraft";
import { getServerDictionary } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  const { t } = await getServerDictionary();
  return { title: t.individual.legal.termsTitle };
}

/** Terms of use — placeholder until the legal text is approved; the accepted version is recorded per account. */
export default async function TermsPage() {
  const { t } = await getServerDictionary();
  const L = t.individual.legal;
  return <LegalDraft title={L.termsTitle} body={L.termsBody} />;
}

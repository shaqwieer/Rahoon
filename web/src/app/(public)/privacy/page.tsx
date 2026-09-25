import type { Metadata } from "next";
import { LegalDraft } from "../LegalDraft";
import { getServerDictionary } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  const { t } = await getServerDictionary();
  return { title: t.individual.legal.privacyTitle };
}

/** Privacy policy — placeholder until approved; lists only what the platform actually enforces today. */
export default async function PrivacyPage() {
  const { t } = await getServerDictionary();
  const L = t.individual.legal;
  return <LegalDraft title={L.privacyTitle} body={L.privacyBody} facts={L.privacyFacts} />;
}

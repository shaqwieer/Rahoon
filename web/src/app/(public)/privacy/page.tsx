import type { Metadata } from "next";
import { M } from "@/components/market/copy";
import { LegalDraft } from "../LegalDraft";

export const metadata: Metadata = { title: M.legal.privacyTitle };

/** Privacy policy — draft until legal approval; lists only what the platform actually enforces today. */
export default function PrivacyPage() {
  return <LegalDraft title={M.legal.privacyTitle} body={M.legal.privacyBody} facts={M.legal.privacyFacts} />;
}

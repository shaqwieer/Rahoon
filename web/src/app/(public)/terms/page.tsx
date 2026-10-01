import type { Metadata } from "next";
import { M } from "@/components/market/copy";
import { LegalDraft } from "../LegalDraft";

export const metadata: Metadata = { title: M.legal.termsTitle };

/** Terms of use — draft until legal approval. */
export default function TermsPage() {
  return <LegalDraft title={M.legal.termsTitle} body={M.legal.termsBody} facts={M.legal.termsFacts} />;
}

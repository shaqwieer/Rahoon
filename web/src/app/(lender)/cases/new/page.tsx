import type { Metadata } from "next";
import { StartDraft } from "./StartDraft";

export const metadata: Metadata = { title: "حالة جديدة" };

/** L03 entry: creates a draft (reference allocated server-side) and opens the wizard. */
export default function NewCasePage() {
  return <StartDraft />;
}

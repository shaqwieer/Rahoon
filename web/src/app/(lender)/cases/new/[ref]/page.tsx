import type { Metadata } from "next";
import { apiGet } from "@/lib/api/server";
import { CreateCaseWizard, type DraftData } from "./CreateCaseWizard";

export const metadata: Metadata = { title: "حالة جديدة" };

/** L03 — six-step create-case wizard over a persisted draft (autosaved). */
export default async function DraftPage({ params }: PageProps<"/cases/new/[ref]">) {
  const { ref } = await params;
  const draft = await apiGet<DraftData>(`/cases/drafts/${encodeURIComponent(ref)}`);
  return <CreateCaseWizard initial={draft} />;
}

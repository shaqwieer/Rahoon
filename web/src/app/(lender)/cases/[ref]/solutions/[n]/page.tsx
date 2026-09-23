import type { Metadata } from "next";
import { apiGet } from "@/lib/api/server";
import type { SolutionDetail } from "@/lib/api/lender";
import { SolutionBuilder } from "./SolutionBuilder";

export async function generateMetadata({ params }: PageProps<"/cases/[ref]/solutions/[n]">): Promise<Metadata> {
  const { n } = await params;
  return { title: `الحل v${n}` };
}

/** L13 — Solution builder (editable draft) or locked version detail. */
export default async function SolutionPage({ params }: PageProps<"/cases/[ref]/solutions/[n]">) {
  const { ref, n } = await params;
  const detail = await apiGet<SolutionDetail>(`/cases/${ref}/solutions/${encodeURIComponent(n)}`);
  return <SolutionBuilder reference={ref} detail={detail} />;
}

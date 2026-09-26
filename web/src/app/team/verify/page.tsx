import type { Metadata } from "next";
import { teamCopy } from "@/components/team/copy";
import { apiGet } from "@/lib/api/server";
import type { VerifyItem } from "@/lib/api/team";
import { getLocale } from "@/lib/i18n/server";
import { VerifyQueue } from "./VerifyQueue";

export async function generateMetadata(): Promise<Metadata> {
  return { title: teamCopy(await getLocale()).verify.title };
}

/** «بحاجة إلى تحقق» (design request D-4): offers awaiting a second member's check (T06). */
export default async function VerifyPage() {
  const items = await apiGet<VerifyItem[]>("/team/verify");
  return <VerifyQueue items={items} />;
}

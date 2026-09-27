import type { Metadata } from "next";
import { teamCopy } from "@/components/team/copy";
import { apiGet } from "@/lib/api/server";
import type { ExecutionVerifyItem, VerifyItem } from "@/lib/api/team";
import { getLocale } from "@/lib/i18n/server";
import { VerifyQueue } from "./VerifyQueue";

export async function generateMetadata(): Promise<Metadata> {
  return { title: teamCopy(await getLocale()).verify.title };
}

/** «بحاجة إلى تحقق» (design requests D-4, D-7): offers and execution records awaiting a second member's check (T06). */
export default async function VerifyPage() {
  const [items, execution] = await Promise.all([apiGet<VerifyItem[]>("/team/verify"), apiGet<ExecutionVerifyItem[]>("/team/verify/execution")]);
  return <VerifyQueue items={items} execution={execution} />;
}

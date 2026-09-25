import type { Metadata } from "next";
import { apiGet } from "@/lib/api/server";
import { getServerDictionary } from "@/lib/i18n/server";
import { MyHome, type IndividualAccount } from "./MyHome";

export async function generateMetadata(): Promise<Metadata> {
  const { t } = await getServerDictionary();
  return { title: t.individual.my.title };
}

/** «حسابي» — the individual's home. Requests (list, start, tracking) arrive in Phase 1A step 5. */
export default async function MyPage() {
  const account = await apiGet<IndividualAccount>("/individual/account");
  return <MyHome account={account} />;
}

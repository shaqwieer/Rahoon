import type { Metadata } from "next";
import { getIndividualContext, myGet } from "@/components/individual/server";
import { apiGet } from "@/lib/api/server";
import type { MyRequestListItem } from "@/lib/api/requests";
import { getServerDictionary } from "@/lib/i18n/server";
import { MyHome, type IndividualAccount } from "./MyHome";

export async function generateMetadata(): Promise<Metadata> {
  const { t } = await getServerDictionary();
  return { title: t.individual.my.title };
}

/** «حسابي» — the individual's home: «طلباتي» (several requests, Q7/Q14) and the account card. */
export default async function MyPage() {
  await getIndividualContext();
  const [account, requests] = await Promise.all([apiGet<IndividualAccount>("/individual/account"), myGet<MyRequestListItem[]>("/requests")]);
  return <MyHome account={account} requests={requests} />;
}

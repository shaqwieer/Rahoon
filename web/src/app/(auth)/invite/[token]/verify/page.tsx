import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { getOwnerInvitation } from "@/lib/api/owner";
import { getServerDictionary } from "@/lib/i18n/server";
import { OwnerVerify } from "./OwnerVerify";

export async function generateMetadata(): Promise<Metadata> {
  const { t } = await getServerDictionary();
  return { title: t.auth.verify.title, referrer: "no-referrer" };
}

/** D01b + OTP step — invitation link + last 4 ID digits + SMS code (national digital ID is a reserved placeholder). */
export default async function VerifyPage({ params }: PageProps<"/invite/[token]/verify">) {
  const { token } = await params;
  const inv = await getOwnerInvitation(token);
  // Expired / invalid / unreachable invitations are explained on the invitation page itself.
  if (inv.status !== "active" && inv.status !== "used") redirect(`/invite/${encodeURIComponent(token)}`);
  return <OwnerVerify token={token} phoneMasked={inv.phoneMasked ?? ""} nationalIdState={inv.nationalIdProvider ?? "unavailable"} />;
}

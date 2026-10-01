"use client";

import { PhoneSignIn } from "@/components/market/PhoneSignIn";
import { hardNavigate } from "@/lib/navigation";

export function SignInForm({ next }: { next: string }) {
  return <PhoneSignIn onSignedIn={(r) => hardNavigate(r.needsName ? `/account?name=1&next=${encodeURIComponent(next)}` : next, "/account")} />;
}

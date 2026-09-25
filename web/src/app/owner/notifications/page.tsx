import { OwnerPage } from "@/components/owner/OwnerPage";
import { getOwnerContext, ownerMetadata } from "@/components/owner/server";
import type { OwnerNotifications } from "@/components/owner/types";
import { EmptyState } from "@/components/ui";
import { apiGet } from "@/lib/api/server";
import { NotificationsClient } from "./NotificationsClient";

export const generateMetadata = () => ownerMetadata((c) => c.notifications.title);

/** Owner notifications — the bell target on mobile and desktop. */
export default async function OwnerNotificationsPage() {
  const { c, shell } = await getOwnerContext();
  const data = await apiGet<OwnerNotifications>("/notifications");
  return (
    <OwnerPage shell={{ ...shell, unread: data.unread }} title={c.notifications.title} backHref="/owner" backLabel={c.backHome} active="home">
      {data.items.length === 0 ? (
        <EmptyState icon="notifications" title={c.notifications.emptyTitle} body={c.notifications.emptyBody} />
      ) : (
        <NotificationsClient items={data.items} unread={data.unread} />
      )}
    </OwnerPage>
  );
}

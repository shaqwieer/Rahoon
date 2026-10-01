/** Loading placeholder shown while a server page fetches (announced once to screen readers). */
export function PageSkeleton({ cards = 3 }: { cards?: number }) {
  return (
    <div role="status" aria-live="polite" className="flex flex-col gap-5">
      <span className="sr-only">جارٍ التحميل…</span>
      <div className="h-9 w-64 animate-rh-pulse rounded-sm bg-subtle" />
      <div className="h-24 animate-rh-pulse rounded-lg bg-subtle" />
      <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
        {Array.from({ length: cards }, (_, i) => (
          <div key={i} className="h-64 animate-rh-pulse rounded-lg bg-subtle" />
        ))}
      </div>
    </div>
  );
}

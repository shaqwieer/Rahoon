"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState, type RefObject } from "react";
import { Dialog } from "@/components/ui/Dialog";
import { Icon } from "@/components/ui/Icon";
import { useI18n } from "@/lib/i18n/client";

/** Opens the palette on Ctrl/⌘ K anywhere in the workspace. */
export function useCommandPaletteShortcut(open: () => void) {
  const openRef = useRef(open);
  useEffect(() => {
    openRef.current = open;
  }, [open]);
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && !e.altKey && e.key.toLowerCase() === "k") {
        e.preventDefault();
        openRef.current();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);
}

/**
 * S10 global search overlay (720px, 96px from the top). Results are scoped server-side to the active
 * organization and permissions; submitting goes to /search?q= which renders them.
 */
export function CommandPalette({ open, onClose, orgName }: { open: boolean; onClose: () => void; orgName: string }) {
  const { t } = useI18n();
  const inputRef = useRef<HTMLInputElement>(null);
  return (
    <Dialog open={open} onClose={onClose} title={t.shell.searchDialog} hideTitle size="lg" top={96} initialFocusRef={inputRef} className="overflow-hidden">
      <PaletteBody inputRef={inputRef} orgName={orgName} onDone={onClose} />
    </Dialog>
  );
}

function PaletteBody({ inputRef, orgName, onDone }: { inputRef: RefObject<HTMLInputElement | null>; orgName: string; onDone: () => void }) {
  const { t } = useI18n();
  const router = useRouter();
  const [q, setQ] = useState("");
  return (
    <form
      role="search"
      onSubmit={(e) => {
        e.preventDefault();
        const query = q.trim();
        if (!query) return;
        onDone();
        router.push(`/search?q=${encodeURIComponent(query)}`);
      }}
    >
      <div className="flex h-[60px] items-center gap-3 border-b border-line px-5">
        <Icon name="search" size={22} className="text-muted" />
        <label htmlFor="palette-q" className="sr-only">
          {t.shell.searchDialog}
        </label>
        <input
          ref={inputRef}
          id="palette-q"
          type="search"
          autoComplete="off"
          enterKeyHint="search"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          placeholder={t.shell.searchPlaceholder}
          className="h-full min-w-0 flex-1 border-0 bg-transparent text-18 font-medium outline-none placeholder:text-soft"
        />
        <kbd dir="ltr" className="rounded-xs border border-line bg-white px-1.5 py-px font-mono text-11 font-medium text-muted">
          Esc
        </kbd>
      </div>
      <div className="flex items-center gap-1.5 bg-warm px-5 py-2 text-12 text-muted">
        <Icon name="shield" size={16} />
        {t.shell.searchScope(orgName)}
      </div>
      <div className="flex flex-col gap-2 px-5 py-6 text-14 text-muted">
        <span>{t.shell.searchHint}</span>
      </div>
      <div className="flex flex-wrap items-center gap-4 border-t border-divider px-5 py-2.5 text-12 text-muted">
        <span>
          <kbd dir="ltr" className="font-mono">
            Enter
          </kbd>{" "}
          {t.shell.searchGo}
        </span>
        <span>
          <kbd dir="ltr" className="font-mono">
            Esc
          </kbd>{" "}
          {t.common.close}
        </span>
      </div>
    </form>
  );
}

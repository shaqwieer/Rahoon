/**
 * The mortgage-default help model was withdrawn on 2026-10-01 (docs/redefinition/legacy-inventory.md). Its portals are
 * archived behind RAHOON_LEGACY_MODES=1 (src/proxy.ts answers 404 for them otherwise). Read at runtime on the server.
 */
export const LEGACY_MODES = process.env.RAHOON_LEGACY_MODES === "1";

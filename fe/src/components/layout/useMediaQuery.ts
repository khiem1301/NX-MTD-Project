import { useSyncExternalStore } from 'react';

/** Theo dõi media query (ví dụ màn rộng) mà không bị nháy layout ở lần render đầu như useBreakpoint. */
export function useMediaQuery(query: string): boolean {
  return useSyncExternalStore(
    (notify) => {
      const mql = window.matchMedia(query);
      mql.addEventListener('change', notify);
      return () => mql.removeEventListener('change', notify);
    },
    () => window.matchMedia(query).matches,
    () => true,
  );
}

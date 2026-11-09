import { useCallback, useEffect, useRef, useState } from "react";

/** Re-renders every `ms` milliseconds and returns the current time (used for countdowns). */
export function useNow(ms = 1000) {
  const [now, setNow] = useState(Date.now());
  useEffect(() => {
    const t = setInterval(() => setNow(Date.now()), ms);
    return () => clearInterval(t);
  }, [ms]);
  return now;
}

/** Calls `fn` immediately and then every `ms` milliseconds. */
export function usePolling(fn, deps, ms = 3000, enabled = true) {
  const [state, setState] = useState({ data: null, loading: true, error: null });
  const fnRef = useRef(fn);
  fnRef.current = fn;
  const seq = useRef(0);

  const reload = useCallback(async () => {
    const id = ++seq.current;
    try {
      const data = await fnRef.current();
      if (id === seq.current) setState({ data, loading: false, error: null });
    } catch (e) {
      if (id === seq.current) setState((s) => ({ ...s, loading: false, error: e.message }));
    }
  }, []);

  useEffect(() => {
    if (!enabled) return undefined;
    setState((s) => ({ ...s, loading: true }));
    reload();
    const t = setInterval(reload, ms);
    return () => clearInterval(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, ms, reload, ...deps]);

  return { ...state, reload };
}

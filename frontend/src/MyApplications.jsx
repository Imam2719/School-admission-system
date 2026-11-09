import { useEffect, useRef, useState } from "react";
import { api } from "./api.js";
import { usePolling, useNow } from "./hooks.js";
import { effectiveStatus, formatRemaining, holdTotalMs } from "./utils.js";

const R = 26, C = 2 * Math.PI * R;

export function Ring({ remaining, total }) {
  const frac = total > 0 ? Math.min(1, Math.max(0, remaining / total)) : 0;
  const low = remaining < 60000;
  return (
    <div className={low ? "ring low" : "ring"} aria-label="time left">
      <svg viewBox="0 0 64 64" width="64" height="64">
        <circle cx="32" cy="32" r={R} className="ring-bg" />
        <circle cx="32" cy="32" r={R} className="ring-fg" strokeDasharray={C} strokeDashoffset={C * (1 - frac)} transform="rotate(-90 32 32)" />
      </svg>
      <span>{formatRemaining(remaining)}</span>
    </div>
  );
}

function ApplicationCard({ hold, now, studentId, notify, reload }) {
  const [busy, setBusy] = useState(null);
  const status = effectiveStatus(hold, now);
  const remaining = new Date(hold.expiresAt).getTime() - now;
  const active = status === "HELD";
  const refreshed = useRef(false);

  useEffect(() => {
    if (status === "EXPIRED" && hold.status === "HELD" && !refreshed.current) {
      refreshed.current = true;
      reload();
    }
  }, [status, hold.status, reload]);

  async function act(kind) {
    setBusy(kind);
    try {
      if (kind === "pay") {
        await api.confirmHold(hold.holdId, studentId);
        notify("success", "Admission fee confirmed. Your seat is yours.");
      } else {
        await api.cancelHold(hold.holdId, studentId);
        notify("success", "Application cancelled. The seat is open again.");
      }
    } catch (e) {
      notify("error", e.message);
    } finally {
      setBusy(null);
      reload();
    }
  }

  return (
    <article className={`app ${status.toLowerCase()}`}>
      <div className="app-main">
        <h3>{hold.className}</h3>
        <p className="muted">Application {hold.holdId}</p>
        <span className={`badge ${status.toLowerCase()}`}>{status}</span>
      </div>
      {active && <Ring remaining={remaining} total={holdTotalMs(hold.holdId, remaining)} />}
      {active && (
        <div className="actions">
          <button className="primary" onClick={() => act("pay")} disabled={busy !== null}>
            {busy === "pay" ? "Confirming…" : "Confirm admission fee"}
          </button>
          <button className="ghost" onClick={() => act("cancel")} disabled={busy !== null}>
            {busy === "cancel" ? "Cancelling…" : "Cancel application"}
          </button>
        </div>
      )}
    </article>
  );
}

export default function MyApplications({ studentId, notify }) {
  const now = useNow(1000);
  const { data, loading, error, reload } = usePolling(() => api.myHolds(studentId), [studentId], 3000, !!studentId);

  if (!studentId) return <div className="empty">Enter your 14-digit Student ID above to see your applications.</div>;

  return (
    <section>
      <div className="head"><h2>My applications</h2><span className="live"><b className="dot" />Live</span></div>
      {error && <div className="banner">{error} <button className="link" onClick={reload}>Try again</button></div>}
      {loading && !data && <div className="list"><div className="app skel" /></div>}
      {data && data.length === 0 && <div className="empty">No applications yet. Pick a class in the Classes tab.</div>}
      {data && data.length > 0 && (
        <div className="list">
          {data.map((h) => <ApplicationCard key={h.holdId} hold={h} now={now} studentId={studentId} notify={notify} reload={reload} />)}
        </div>
      )}
    </section>
  );
}

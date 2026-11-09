import { useState } from "react";
import { api } from "./api.js";
import { usePolling } from "./hooks.js";

export function Seats({ c }) {
  if (c.totalSeats > 200) {
    const pct = (n) => `${(n / c.totalSeats) * 100}%`;
    return (
      <div className="bar" role="img" aria-label={`${c.confirmedSeats} confirmed, ${c.heldSeats} held, ${c.availableSeats} open`}>
        <i className="s-c" style={{ width: pct(c.confirmedSeats) }} />
        <i className="s-h" style={{ width: pct(c.heldSeats) }} />
      </div>
    );
  }
  const seats = Array.from({ length: c.totalSeats }, (_, i) =>
    i < c.confirmedSeats ? "s-c" : i < c.confirmedSeats + c.heldSeats ? "s-h" : "s-a");
  return (
    <div className="seats" role="img" aria-label={`${c.confirmedSeats} confirmed, ${c.heldSeats} held, ${c.availableSeats} open`}>
      {seats.map((cls, i) => <i key={i} className={cls} />)}
    </div>
  );
}

export default function ClassList({ studentId, notify, onApplied }) {
  const { data, loading, error, reload } = usePolling(() => api.listClasses(), [], 3000);
  const [busyId, setBusyId] = useState(null);

  async function apply(c) {
    setBusyId(c.id);
    try {
      await api.createHold(c.id, studentId);
      notify("success", `Seat held in ${c.name}. Confirm the fee before the timer ends.`);
      onApplied();
    } catch (e) {
      notify("error", e.message);
      reload();
    } finally {
      setBusyId(null);
    }
  }

  return (
    <section>
      <div className="head">
        <h2>Open classes</h2>
        <span className="live"><b className="dot" />Live</span>
      </div>
      {error && <div className="banner">{error} <button className="link" onClick={reload}>Try again</button></div>}
      {loading && !data && <div className="grid">{[0, 1, 2].map((i) => <div key={i} className="card skel" />)}</div>}
      {data && data.length === 0 && <div className="empty">No classes yet. Add the first one in the Admin tab.</div>}
      {data && data.length > 0 && (
        <div className="grid">
          {data.map((c) => {
            const full = c.availableSeats <= 0;
            return (
              <article className="card" key={c.id}>
                <div className="card-top">
                  <h3>{c.name}</h3>
                  <span className={full ? "pill full" : "pill"}>{full ? "Full" : `${c.availableSeats} left`}</span>
                </div>
                <Seats c={c} />
                <dl className="legend">
                  <div><dt><i className="s-c" />Confirmed</dt><dd>{c.confirmedSeats}</dd></div>
                  <div><dt><i className="s-h" />Held</dt><dd>{c.heldSeats}</dd></div>
                  <div><dt><i className="s-a" />Open</dt><dd>{c.availableSeats}</dd></div>
                  <div><dt>Total</dt><dd>{c.totalSeats}</dd></div>
                </dl>
                <button className="primary" onClick={() => apply(c)} disabled={!studentId || full || busyId === c.id}>
                  {busyId === c.id ? "Holding seat…" : full ? "No seats left" : !studentId ? "Enter Student ID to apply" : "Apply for this class"}
                </button>
              </article>
            );
          })}
        </div>
      )}
    </section>
  );
}

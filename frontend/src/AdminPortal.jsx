import { useState } from "react";
import { api } from "./api.js";
import { usePolling } from "./hooks.js";
import { Seats } from "./ClassList.jsx";
import AdminApplications from "./AdminApplications.jsx";

export default function AdminPortal({ notify }) {
  const { data, loading, error, reload } = usePolling(() => api.listClasses(), [], 3000);
  const [name, setName] = useState("");
  const [seats, setSeats] = useState(120);
  const [busy, setBusy] = useState(false);
  const [tab, setTab] = useState("apps");

  async function create(e) {
    e.preventDefault();
    if (!name.trim()) return notify("error", "Enter a class name, for example Class 6 - Morning Shift.");
    if (!Number.isInteger(Number(seats)) || Number(seats) <= 0) return notify("error", "Total seats must be a whole number above 0.");
    setBusy(true);
    try {
      const c = await api.createClass(name.trim(), Number(seats));
      notify("success", `${c.name} created with ${c.totalSeats} seats.`);
      setName("");
      reload();
    } catch (err) {
      notify("error", err.message);
    } finally {
      setBusy(false);
    }
  }

  const sum = (k) => (data || []).reduce((n, c) => n + c[k], 0);

  return (
    <>
      <header className="hero admin"><div className="wrap">
        <p className="brand">Admin portal</p>
        <h1>Manage classes and seats</h1>
        <p className="sub">Create classes, then approve guardian applications. Approved guardians pay from their own portal.</p>
      </div></header>
      <main className="wrap">
        <nav className="tabs" role="tablist">
          {[["apps", "Applications"], ["classes", "Classes and seats"]].map(([k, label]) => (
            <button key={k} role="tab" aria-selected={tab === k} className={tab === k ? "tab on" : "tab"} onClick={() => setTab(k)}>{label}</button>
          ))}
        </nav>
        {tab === "apps" && <AdminApplications notify={notify} />}
        {tab === "classes" && (<>
        <div className="stats" style={{ marginTop: 20 }}>
          <div><b>{data ? data.length : "–"}</b><span>Classes</span></div>
          <div><b>{data ? sum("totalSeats") : "–"}</b><span>Total seats</span></div>
          <div><b>{data ? sum("confirmedSeats") : "–"}</b><span>Confirmed</span></div>
          <div><b>{data ? sum("heldSeats") : "–"}</b><span>On hold</span></div>
        </div>

        <div className="admin-grid">
          <form className="card form" onSubmit={create}>
            <h3>Add a class</h3>
            <label>Class name
              <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Class 6 - Morning Shift" />
            </label>
            <label>Total seats
              <input type="number" min="1" value={seats} onChange={(e) => setSeats(e.target.value)} />
            </label>
            <button className="primary" type="submit" disabled={busy}>{busy ? "Creating…" : "Create class"}</button>
          </form>

          <section>
            <div className="head" style={{ marginTop: 0 }}><h2>All classes</h2><span className="live"><b className="dot" />Live</span></div>
            {error && <div className="banner">{error} <button className="link" onClick={reload}>Try again</button></div>}
            {loading && !data && <div className="card skel" />}
            {data && data.length === 0 && <div className="empty" style={{ marginTop: 0 }}>No classes yet. Add the first one.</div>}
            <div className="list">
              {data && data.map((c) => (
                <article className="card" key={c.id}>
                  <div className="card-top"><h3>{c.name}</h3><span className="pill">{c.availableSeats} of {c.totalSeats} open</span></div>
                  <Seats c={c} />
                  <dl className="legend">
                    <div><dt><i className="s-c" />Confirmed</dt><dd>{c.confirmedSeats}</dd></div>
                    <div><dt><i className="s-h" />Held</dt><dd>{c.heldSeats}</dd></div>
                    <div><dt><i className="s-a" />Open</dt><dd>{c.availableSeats}</dd></div>
                    <div><dt>Total</dt><dd>{c.totalSeats}</dd></div>
                  </dl>
                </article>
              ))}
            </div>
          </section>
        </div></>)}
      </main>
    </>
  );
}

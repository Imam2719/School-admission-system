import { useEffect, useState } from "react";
import { api } from "./api.js";
import { usePolling, useNow } from "./hooks.js";
import { Ring } from "./MyApplications.jsx";
import { holdTotalMs, isValidNid, loadNid, saveNid } from "./utils.js";

function ApplyForm({ notify, onDone }) {
  const { data: classes } = usePolling(() => api.listClasses(), [], 3000);
  const [f, setF] = useState({ guardianName: "", studentName: "", nid: "", classId: "" });
  const [busy, setBusy] = useState(false);
  const set = (k) => (e) => setF({ ...f, [k]: k === "nid" ? e.target.value.replace(/\D/g, "") : e.target.value });
  const isFull = (c) => c.availableSeats <= 0;

  // If the selected class fills up while the guardian is on this page, clear the choice and tell them.
  useEffect(() => {
    const picked = (classes || []).find((c) => c.id === f.classId);
    if (picked && isFull(picked)) {
      setF((old) => ({ ...old, classId: "" }));
      notify("error", `${picked.name} is full. Please choose another class.`);
    }
  }, [classes]); // eslint-disable-line react-hooks/exhaustive-deps

  function choose(c) {
    if (isFull(c)) return notify("error", `${c.name} is full. No seats are available, please choose another class.`);
    setF({ ...f, classId: c.id });
  }

  async function submit(e) {
    e.preventDefault();
    if (!f.guardianName.trim() || !f.studentName.trim()) return notify("error", "Enter the guardian's name and the student's name.");
    if (!isValidNid(f.nid)) return notify("error", "NID number must be 10, 13 or 17 digits.");
    if (!f.classId) return notify("error", "Choose the class you are applying for.");
    const picked = (classes || []).find((c) => c.id === f.classId);
    if (picked && isFull(picked)) return notify("error", `${picked.name} is full. Please choose another class.`);
    setBusy(true);
    try {
      const a = await api.submitApplication(f);
      notify("success", `Application submitted. Student ID ${a.studentId} was generated.`);
      onDone(f.nid);
    } catch (err) { notify("error", err.message); } finally { setBusy(false); }
  }

  return (
    <form className="card form wide" onSubmit={submit}>
      <h3>Admission application</h3>
      <div className="two">
        <label>Guardian name<input value={f.guardianName} onChange={set("guardianName")} placeholder="Full name" /></label>
        <label>Student name<input value={f.studentName} onChange={set("studentName")} placeholder="Child's full name" /></label>
      </div>
      <label>Guardian NID number
        <input inputMode="numeric" maxLength={17} value={f.nid} onChange={set("nid")} placeholder="10, 13 or 17 digits" aria-invalid={f.nid.length > 0 && !isValidNid(f.nid)} />
      </label>
      <fieldset>
        <legend>Choose a class</legend>
        <div className="picks">
          {(classes || []).map((c) => {
            const full = isFull(c);
            return (
              <label key={c.id} className={`pick ${f.classId === c.id ? "on" : ""} ${full ? "full" : ""}`} aria-disabled={full}>
                <input type="radio" name="cls" checked={f.classId === c.id} onChange={() => choose(c)} />
                <b>{c.name}</b>
                {full
                  ? <span className="pick-full">Class is full</span>
                  : <span className="pick-avail">{c.availableSeats} {c.availableSeats === 1 ? "seat" : "seats"} available</span>}
                <span className="muted">{c.totalSeats} total seats</span>
              </label>
            );
          })}
          {classes && classes.length === 0 && <p className="muted">No classes are open yet. Please check back soon.</p>}
        </div>
      </fieldset>
      <p className="muted">After you submit, a Student ID is generated automatically. The school then reviews your application.</p>
      <button className="primary" type="submit" disabled={busy}>{busy ? "Submitting…" : "Submit application"}</button>
    </form>
  );
}

function AppCard({ a, now, notify, reload }) {
  const [busy, setBusy] = useState(null);
  const left = a.expiresAt ? new Date(a.expiresAt).getTime() - now : 0;
  let stage = "lapsed";
  if (a.status === "REJECTED") stage = "rejected";
  else if (a.status === "PENDING") stage = "pending";
  else if (a.holdStatus === "CONFIRMED") stage = "paid";
  else if (a.holdStatus === "HELD" && left > 0) stage = "pay";
  const TEXT = { pending: "Waiting for the school to approve", pay: "Approved. Confirm the fee before the timer ends", paid: "Admission confirmed. Welcome!", rejected: "The school did not approve this application", lapsed: "The payment window ended. Ask the school to approve again" };
  const step = stage === "paid" ? 3 : stage === "pay" || stage === "lapsed" ? 2 : 1;

  async function act(kind) {
    setBusy(kind);
    try {
      if (kind === "pay") { await api.confirmHold(a.holdId, a.studentId); notify("success", "Admission fee confirmed. The seat is yours."); }
      else { await api.cancelHold(a.holdId, a.studentId); notify("success", "Seat released."); }
    } catch (e) { notify("error", e.message); } finally { setBusy(null); reload(); }
  }

  return (
    <article className={`app ${stage}`}>
      <div className="app-main">
        <h3>{a.studentName} · {a.className}</h3>
        <p className="sid">Student ID <b>{a.studentId}</b></p>
        <p className="muted">{TEXT[stage]}</p>
        <ol className="steps">{["Submitted", "Approved", "Fee confirmed"].map((s, i) => <li key={s} className={i < step ? "done" : ""}>{s}</li>)}</ol>
      </div>
      {stage === "pay" && <Ring remaining={left} total={holdTotalMs(a.holdId, left)} />}
      {stage === "pay" && (
        <div className="actions">
          <button className="primary" disabled={busy !== null} onClick={() => act("pay")}>{busy === "pay" ? "Confirming…" : "Confirm admission fee"}</button>
          <button className="ghost" disabled={busy !== null} onClick={() => act("cancel")}>{busy === "cancel" ? "Releasing…" : "Cancel application"}</button>
        </div>
      )}
    </article>
  );
}

function Track({ nid, setNid, notify }) {
  const now = useNow(1000);
  const [draft, setDraft] = useState("");
  const { data, loading, error, reload } = usePolling(() => api.myApplications(nid), [nid], 3000, !!nid);

  if (!nid) return (
    <form className="card gate" onSubmit={(e) => { e.preventDefault(); if (!isValidNid(draft)) return notify("error", "NID number must be 10, 13 or 17 digits."); saveNid(draft); setNid(draft); }}>
      <h3>Find your applications</h3>
      <label>Guardian NID number<input inputMode="numeric" maxLength={17} value={draft} onChange={(e) => setDraft(e.target.value.replace(/\D/g, ""))} placeholder="The NID you applied with" /></label>
      <button className="primary" type="submit">Show my applications</button>
    </form>
  );
  return (
    <section>
      <div className="who"><span>NID <b>{nid}</b></span><button className="link" onClick={() => { saveNid(""); setNid(""); }}>Use a different NID</button><span className="live"><b className="dot" />Live</span></div>
      {error && <div className="banner">{error} <button className="link" onClick={reload}>Try again</button></div>}
      {loading && !data && <div className="app skel" />}
      {data && data.length === 0 && <div className="empty" style={{ marginTop: 0 }}>No applications for this NID yet.</div>}
      <div className="list">{data && data.map((a) => <AppCard key={a.id} a={a} now={now} notify={notify} reload={reload} />)}</div>
    </section>
  );
}

export default function GuardianPortal({ notify }) {
  const [nid, setNid] = useState(loadNid);
  const [tab, setTab] = useState(nid ? "track" : "apply");
  return (
    <>
      <header className="hero"><div className="wrap">
        <p className="brand">Guardian portal</p>
        <h1>Apply, get approved, pay the fee</h1>
        <p className="sub">Submit one application per child. We generate the Student ID for you.</p>
      </div></header>
      <main className="wrap">
        <nav className="tabs" role="tablist">
          {[["apply", "New application"], ["track", "Track and pay"]].map(([k, l]) => (
            <button key={k} role="tab" aria-selected={tab === k} className={tab === k ? "tab on" : "tab"} onClick={() => setTab(k)}>{l}</button>
          ))}
        </nav>
        <div style={{ marginTop: 22 }}>
          {tab === "apply" && <ApplyForm notify={notify} onDone={(n) => { saveNid(n); setNid(n); setTab("track"); }} />}
          {tab === "track" && <Track nid={nid} setNid={setNid} notify={notify} />}
        </div>
      </main>
    </>
  );
}

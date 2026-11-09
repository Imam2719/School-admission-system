import { useState } from "react";
import { api } from "./api.js";
import { usePolling } from "./hooks.js";

const needsAction = (a) => a.status === "PENDING" || (a.status === "APPROVED" && a.holdStatus !== "HELD" && a.holdStatus !== "CONFIRMED");

function label(a) {
  if (a.status === "PENDING") return ["pending", "Waiting for approval"];
  if (a.status === "REJECTED") return ["rejected", "Rejected"];
  if (a.holdStatus === "CONFIRMED") return ["confirmed", "Fee paid, admitted"];
  if (a.holdStatus === "HELD") return ["held", "Approved, waiting for fee"];
  return ["expired", "Payment window ended"];
}

function Row({ a, busy, act }) {
  const [cls, text] = label(a);
  return (
    <article className="card">
      <div className="card-top">
        <div><h3>{a.studentName}</h3><p className="muted">Guardian {a.guardianName} · NID {a.nid}</p></div>
        <span className={`badge ${cls}`}>{text}</span>
      </div>
      <p>{a.className} <span className="muted">· Student ID {a.studentId}</span></p>
      {needsAction(a) && (
        <div className="actions">
          <button className="primary" disabled={busy === a.id} onClick={() => act(a, "approve")}>
            {a.status === "PENDING" ? "Approve application" : "Approve again"}
          </button>
          {a.status === "PENDING" && <button className="ghost" disabled={busy === a.id} onClick={() => act(a, "reject")}>Reject</button>}
        </div>
      )}
    </article>
  );
}

export default function AdminApplications({ notify }) {
  const { data, loading, error, reload } = usePolling(() => api.adminApplications(), [], 3000);
  const [busy, setBusy] = useState(null);

  async function act(a, kind) {
    setBusy(a.id);
    try {
      if (kind === "approve") {
        await api.approveApplication(a.id);
        notify("success", `${a.studentName} approved. The guardian can now pay the fee.`);
      } else {
        await api.rejectApplication(a.id);
        notify("success", `${a.studentName}'s application was rejected.`);
      }
    } catch (e) { notify("error", e.message); } finally { setBusy(null); reload(); }
  }

  const todo = (data || []).filter(needsAction);
  const done = (data || []).filter((a) => !needsAction(a));
  return (
    <section>
      <div className="head"><h2>Needs your decision ({todo.length})</h2><span className="live"><b className="dot" />Live</span></div>
      {error && <div className="banner">{error} <button className="link" onClick={reload}>Try again</button></div>}
      {loading && !data && <div className="card skel" />}
      {data && todo.length === 0 && <div className="empty" style={{ marginTop: 0 }}>Nothing waiting. New guardian applications appear here.</div>}
      <div className="list">{todo.map((a) => <Row key={a.id} a={a} busy={busy} act={act} />)}</div>
      {done.length > 0 && <><div className="head"><h2>Decided</h2></div><div className="list">{done.map((a) => <Row key={a.id} a={a} busy={busy} act={act} />)}</div></>}
    </section>
  );
}

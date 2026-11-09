import { useState } from "react";
import { api } from "./api.js";

export default function AdminPanel({ notify }) {
  const [name, setName] = useState("");
  const [seats, setSeats] = useState(120);
  const [busy, setBusy] = useState(false);

  async function submit(e) {
    e.preventDefault();
    if (!name.trim()) return notify("error", "Enter a class name, for example Class 6 - Morning Shift.");
    if (!Number.isInteger(Number(seats)) || Number(seats) <= 0) return notify("error", "Total seats must be a whole number above 0.");
    setBusy(true);
    try {
      const c = await api.createClass(name.trim(), Number(seats));
      notify("success", `${c.name} created with ${c.totalSeats} seats.`);
      setName("");
    } catch (err) {
      notify("error", err.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <section>
      <div className="head"><h2>Add a class</h2></div>
      <form className="card form" onSubmit={submit}>
        <label>Class name
          <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Class 6 - Morning Shift" />
        </label>
        <label>Total seats
          <input type="number" min="1" value={seats} onChange={(e) => setSeats(e.target.value)} />
        </label>
        <button className="primary" type="submit" disabled={busy}>{busy ? "Creating…" : "Create class"}</button>
      </form>
    </section>
  );
}

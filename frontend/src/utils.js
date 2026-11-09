/** Format milliseconds as MM:SS (never negative). */
export function formatRemaining(ms) {
  const total = Math.max(0, Math.ceil(ms / 1000));
  const m = String(Math.floor(total / 60)).padStart(2, "0");
  const s = String(total % 60).padStart(2, "0");
  return `${m}:${s}`;
}

/** A HELD hold past its expiry is shown as EXPIRED immediately, without waiting for the server. */
export function effectiveStatus(hold, nowMs) {
  if (hold.status === "HELD" && new Date(hold.expiresAt).getTime() <= nowMs) return "EXPIRED";
  return hold.status;
}

export function loadStudentId() {
  try { return localStorage.getItem("studentId") || ""; } catch { return ""; }
}

export function saveStudentId(id) {
  try { localStorage.setItem("studentId", id); } catch { /* ignore */ }
}

/** Spec: the student ID is a 14-digit number. */
export const isValidStudentId = (id) => /^\d{14}$/.test(id);

/** Total hold length is not in the API, so remember the longest remaining time seen per hold. */
export function holdTotalMs(holdId, remainingMs) {
  const key = `holdTotal:${holdId}`;
  try {
    const saved = Number(localStorage.getItem(key)) || 0;
    const total = Math.max(saved, remainingMs);
    if (total !== saved) localStorage.setItem(key, String(total));
    return total;
  } catch { return remainingMs; }
}

export const isValidNid = (v) => /^(\d{10}|\d{13}|\d{17})$/.test(v);
export function loadNid() { try { return localStorage.getItem("guardianNid") || ""; } catch { return ""; } }
export function saveNid(v) { try { localStorage.setItem("guardianNid", v); } catch { /* ignore */ } }

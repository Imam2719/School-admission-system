const API_URL =
  (typeof window !== "undefined" && window.APP_CONFIG && window.APP_CONFIG.API_URL) ||
  import.meta.env.VITE_API_URL ||
  "http://localhost:8080";

async function request(path, { method = "GET", body, studentId } = {}) {
  const headers = {};
  if (body) headers["Content-Type"] = "application/json";
  if (studentId) headers["X-Student-Id"] = studentId;

  let res;
  try {
    res = await fetch(API_URL + path, { method, headers, body: body ? JSON.stringify(body) : undefined });
  } catch {
    throw new Error("Cannot reach the server. Is the backend running?");
  }
  if (res.status === 204) return null;

  let data = null;
  try { data = await res.json(); } catch { /* no body */ }
  if (!res.ok) throw new Error((data && data.error) || (res.status === 400 ? "Some of the information sent was invalid. Check the fields and try again." : res.status >= 500 ? "The server hit a problem. Please try again in a moment." : `Request failed (${res.status})`));
  return data;
}

export const api = {
  listClasses: () => request("/classes"),
  createClass: (name, totalSeats) => request("/classes", { method: "POST", body: { name, totalSeats } }),
  createHold: (classId, studentId) => request(`/classes/${classId}/holds`, { method: "POST", studentId }),
  confirmHold: (holdId, studentId) => request(`/holds/${holdId}/confirm`, { method: "POST", studentId }),
  cancelHold: (holdId, studentId) => request(`/holds/${holdId}`, { method: "DELETE", studentId }),
  myHolds: (studentId) => request("/me/holds", { studentId }),
  submitApplication: (body) => request("/applications", { method: "POST", body }),
  myApplications: (nid) => request(`/applications?nid=${encodeURIComponent(nid)}`),
  adminApplications: () => request("/admin/applications"),
  approveApplication: (id) => request(`/applications/${id}/approve`, { method: "POST" }),
  rejectApplication: (id) => request(`/applications/${id}/reject`, { method: "POST" }),
};

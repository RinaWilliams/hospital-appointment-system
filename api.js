// Point this at wherever your ASP.NET Core API is running.
const API_BASE = "https://localhost:62233/api";

const Session = {
  save(auth) {
    localStorage.setItem("has_token", auth.token);
    localStorage.setItem("has_userId", auth.userId);
    localStorage.setItem("has_fullName", auth.fullName);
    localStorage.setItem("has_role", auth.role);
  },
  token() { return localStorage.getItem("has_token"); },
  role() { return localStorage.getItem("has_role"); },
  fullName() { return localStorage.getItem("has_fullName"); },
  userId() { return localStorage.getItem("has_userId"); },
  clear() { localStorage.clear(); },
  requireRole(expected) {
    if (!Session.token() || Session.role() !== expected) window.location.href = "../index.html";
  },
  dashboardUrlForRole(role) {
    return {
      Admin: "pages/dashboard-admin.html",
      Doctor: "pages/dashboard-doctor.html",
      Patient: "pages/dashboard-patient.html",
      Pharmacist: "pages/dashboard-pharmacist.html",
      LabTechnician: "pages/dashboard-labtech.html"
    }[role] || "index.html";
  }
};

async function apiRequest(path, { method = "GET", body = null, auth = true } = {}) {
  const headers = { "Content-Type": "application/json" };
  if (auth && Session.token()) headers["Authorization"] = `Bearer ${Session.token()}`;

  const res = await fetch(`${API_BASE}${path}`, {
    method, headers, body: body ? JSON.stringify(body) : null
  });

  if (res.status === 401) {
    Session.clear();
    window.location.href = "/index.html";
    return null;
  }

  const text = await res.text();
  const data = text ? JSON.parse(text) : null;
  if (!res.ok) throw new Error((data && data.message) || "Request failed");
  return data;
}

async function downloadFile(path, filename) {
  const res = await fetch(`${API_BASE}${path}`, {
    headers: { "Authorization": `Bearer ${Session.token()}` }
  });
  if (!res.ok) { alert("Could not download the file."); return; }
  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

function logout() {
  Session.clear();
  window.location.href = "../index.html";
}

function fmtDate(d) {
  if (!d) return "-";
  return new Date(d).toLocaleDateString();
}
function fmtDateTime(d) {
  if (!d) return "-";
  return new Date(d).toLocaleString();
}
function escapeHtml(str) {
  const div = document.createElement("div");
  div.textContent = str || "";
  return div.innerHTML;
}
function todayISO() {
  return new Date().toISOString().slice(0, 10);
}


function drName(fullName) {
  if (!fullName) return '';
  const s = String(fullName).trim();
  return /^dr\.?\s/i.test(s) ? s.replace(/^dr\.?\s+/i, '') : s;
}
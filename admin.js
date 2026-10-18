Session.requireRole("Admin");
document.getElementById("welcomeMsg").textContent = `${Session.fullName()} · admin`;

document.addEventListener('DOMContentLoaded', function () {
  document.querySelectorAll('.tab').forEach(function (tab) {
    tab.addEventListener('click', function (e) {
      e.preventDefault();
      document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
      document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
      tab.classList.add('active');
      document.getElementById('panel-' + tab.dataset.tab).classList.add('active');
      const loaders = { reports: loadReports, users: loadUsers, facilities: loadFacilities, drugs: loadDrugs, appointments: loadAppointmentsTab, complaints: loadComplaints, payments: loadPayments };
      loaders[tab.dataset.tab]?.();
    });
  });

  document.getElementById('createUserForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const msgBox = document.getElementById('createUserMessage');
    const role = document.getElementById('newRole').value;

    const fullName = document.getElementById('newFullName').value.trim();
    const email = document.getElementById('newEmail').value.trim();
    const password = document.getElementById('newPassword').value;
    const phone = (document.getElementById('newPhone')?.value || '').trim();

    // Item vii: specific validation
    if (!fullName) { msgBox.innerHTML = '<div class="error-text">Please enter the full name.</div>'; return; }
    if (fullName.length < 3) { msgBox.innerHTML = '<div class="error-text">Full name must be at least 3 characters.</div>'; return; }
    if (!email) { msgBox.innerHTML = '<div class="error-text">Please enter an email address.</div>'; return; }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) { msgBox.innerHTML = '<div class="error-text">That email address looks invalid.</div>'; return; }
    if (!password) { msgBox.innerHTML = '<div class="error-text">Please choose a password.</div>'; return; }
    if (password.length < 6) { msgBox.innerHTML = '<div class="error-text">Password must be at least 6 characters.</div>'; return; }
    if (phone && !/^\d{11}$/.test(phone)) { msgBox.innerHTML = '<div class="error-text">Phone must be exactly 11 digits (numbers only).</div>'; return; }

    try {
      // Item xiii: send flat JSON, matching the backend DTO.
      await apiRequest('/users', {
        method: 'POST',
        body: {
          fullName,
          email,
          password,
          role,
          phone: phone || null,
          specialization: role === 'Doctor' ? (document.getElementById('newSpecialization').value.trim() || null) : null,
          bio: role === 'Doctor' ? (document.getElementById('newBio').value.trim() || null) : null,
          consultationFee: role === 'Doctor' ? parseFloat(document.getElementById('newFee')?.value || 0) : null,
          slotDurationMinutes: role === 'Doctor' ? parseInt(document.getElementById('newSlotDuration')?.value || 30) : null,
          facilityId: role === 'Doctor' && document.getElementById('newFacility')?.value
            ? parseInt(document.getElementById('newFacility').value) : null
        }
      });
      e.target.reset();
      msgBox.innerHTML = '<div class="success-text">Account created.</div>';
      loadUsers();
    } catch (err) { msgBox.innerHTML = `<div class="error-text">${err.message}</div>`; }
  });

  document.getElementById('facilityForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const name = document.getElementById('facName').value.trim();
    if (!name) { alert('Please enter the facility name.'); return; }
    try {
      await apiRequest('/facilities', {
        method: 'POST',
        body: { name, address: document.getElementById('facAddress').value, phone: document.getElementById('facPhone').value }
      });
      e.target.reset();
      loadFacilities();
      loadFacilityOptions();
    } catch (err) { alert(err.message); }
  });

  document.getElementById('drugForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const name = document.getElementById('drugName').value.trim();
    if (!name) { alert('Please enter the drug name.'); return; }
    try {
      await apiRequest('/drugs', {
        method: 'POST',
        body: {
          name,
          form: document.getElementById('drugForm2').value,
          strength: document.getElementById('drugStrength').value
        }
      });
      e.target.reset();
      loadDrugs();
    } catch (err) { alert(err.message); }
  });

  loadReports();
  loadFacilityOptions();
});

function toggleDoctorFields() {
  document.getElementById('doctorFields').style.display = document.getElementById('newRole').value === 'Doctor' ? 'block' : 'none';
}

async function loadFacilityOptions() {
  const facilities = await apiRequest('/facilities', { auth: false });
  document.getElementById('newFacility').innerHTML = '<option value="">-</option>' +
    facilities.map(f => `<option value="${f.id}">${escapeHtml(f.name)}</option>`).join('');
}

async function loadDoctorFilterOptions() {
  const doctors = await apiRequest('/doctors?includeUnavailable=true', { auth: false });
  const select = document.getElementById('apptDoctorFilter');
  select.innerHTML = '<option value="">All doctors</option>' +
    doctors.map(d => `<option value="${d.id}">Dr. ${escapeHtml(d.fullName)}</option>`).join('');
}

async function loadReports() {
  const r = await apiRequest('/reports');
  document.getElementById('statTotal').textContent = r.total;
  document.getElementById('statRevenue').textContent = '₦' + Number(r.revenue).toLocaleString();
  document.getElementById('statPaid').textContent = r.paidCount;
  document.getElementById('statFailed').textContent = r.failedCount;

  const colorFor = {
    PendingPayment: 'var(--violet)', Pending: 'var(--amber)', Approved: 'var(--green)',
    Completed: 'var(--ink)', Rejected: 'var(--red)', Cancelled: 'var(--slate)'
  };
  document.getElementById('quickStats').innerHTML = `
    <div><div class="stat-value">${r.total}</div><div class="stat-label">Total</div></div>` +
    Object.entries(r.statusCounts).map(([status, count]) => `
      <div><div class="stat-value" style="color:${colorFor[status] || 'var(--ink)'};">${count}</div>
      <div class="stat-label">${status.replace(/([A-Z])/g, ' $1').trim()}</div></div>`).join('');

  const statuses = Object.entries(r.statusCounts);
  const maxStatus = Math.max(1, ...statuses.map(([, c]) => c));
  document.getElementById('statusChart').innerHTML = statuses.map(([status, count]) => `
    <div class="bar-row">
      <div class="bar-label">${status.replace(/([A-Z])/g, ' $1').trim()}</div>
      <div class="bar-track"><div class="bar-fill" style="width:${(count / maxStatus) * 100}%;background:${colorFor[status] || 'var(--primary)'};"></div></div>
      <div class="bar-count">${count}</div>
    </div>`).join('');

  const maxDoctor = Math.max(1, ...r.byDoctor.map(d => d.count));
  document.getElementById('doctorChart').innerHTML = r.byDoctor.length ? r.byDoctor.map(d => `
    <div class="bar-row">
      <div class="bar-label">Dr. ${escapeHtml(d.fullName)}</div>
      <div class="bar-track"><div class="bar-fill" style="width:${(d.count / maxDoctor) * 100}%;"></div></div>
      <div class="bar-count">${d.count}</div>
    </div>`).join('') : '<div class="muted">No doctors yet.</div>';
}

async function loadUsers() {
  const users = await apiRequest('/users');
  document.getElementById('userRows').innerHTML = users.map(u => `
    <div class="card">
      <div style="display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:10px;">
        <div>
          <div style="font-weight:700;">${escapeHtml(u.fullName)} <span class="muted" style="font-weight:400;">(ID: ${u.id})</span></div>
          <div class="muted" style="font-size:12.5px;">${escapeHtml(u.email)} · ${u.role} · joined ${fmtDate(u.createdAt)}</div>
        </div>
        <div style="display:flex;gap:8px;align-items:center;flex-wrap:wrap;">
          <span class="stamp stamp-${u.isActive ? 'Completed' : 'Cancelled'}">${u.isActive ? 'Active' : 'Inactive'}</span>
          ${u.role === 'Doctor' ? `<button class="btn btn-ghost" onclick="toggleDoctorSchedule(${u.id})">Schedule</button>` : ''}
          ${u.role === 'Doctor' ? `<button class="btn btn-ghost" onclick="toggleDoctorAvailability(${u.id}, true)">Set available</button>
                                    <button class="btn btn-ghost" onclick="toggleDoctorAvailability(${u.id}, false)">Set unavailable</button>` : ''}
          <button class="btn btn-ghost" onclick="toggleUser(${u.id}, ${!u.isActive})">${u.isActive ? 'Deactivate' : 'Activate'}</button>
          <button class="btn btn-reject" onclick="deleteUser(${u.id})">Delete</button>
        </div>
      </div>
      ${u.role === 'Doctor' ? `<div id="schedule-${u.id}"></div>` : ''}
    </div>`).join('');
}

async function toggleDoctorAvailability(doctorId, isAccepting) {
  try {
    await apiRequest(`/doctor-availability/${doctorId}`, { method: 'PUT', body: { isAcceptingBookings: isAccepting, note: null } });
    loadUsers();
  } catch (err) { alert(err.message); }
}

async function toggleDoctorSchedule(doctorId) {
  const box = document.getElementById(`schedule-${doctorId}`);
  if (box.innerHTML) { box.innerHTML = ''; return; }
  box.innerHTML = '<div class="muted" style="margin-top:12px;">Loading…</div>';
  const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
  const schedule = await apiRequest(`/doctor-schedules/${doctorId}`);
  box.innerHTML = `<div style="margin-top:14px;border-top:1px solid var(--line);padding-top:14px;">` +
    (schedule.length
      ? schedule.map(s => `<div style="font-size:13px;padding:4px 0;">${days[s.dayOfWeek]} · ${s.startTime.slice(0,5)} – ${s.endTime.slice(0,5)}</div>`).join('')
      : '<div class="muted" style="font-size:13px;">No weekly availability set.</div>') + `</div>`;
}

async function toggleUser(id, isActive) {
  await apiRequest(`/users/${id}/toggle`, { method: 'PUT', body: { isActive } });
  loadUsers();
}
async function deleteUser(id) {
  if (!confirm('Delete this user permanently?')) return;
  await apiRequest(`/users/${id}`, { method: 'DELETE' });
  loadUsers();
}

async function loadFacilities() {
  const items = await apiRequest('/facilities', { auth: false });
  document.getElementById('facilityRows').innerHTML = items.map(f => `
    <div class="card" style="display:flex;justify-content:space-between;align-items:center;">
      <div><div style="font-weight:700;">${escapeHtml(f.name)}</div><div class="muted" style="font-size:12.5px;">${escapeHtml(f.address || '')} ${escapeHtml(f.phone || '')}</div></div>
      <button class="btn btn-reject" onclick="removeFacility(${f.id})">Remove</button>
    </div>`).join('') || '<div class="card muted">No facilities yet.</div>';
}
async function removeFacility(id) {
  await apiRequest(`/facilities/${id}`, { method: 'DELETE' });
  loadFacilities();
  loadFacilityOptions();
}

async function loadDrugs() {
  const items = await apiRequest('/drugs?activeOnly=false');
  document.getElementById('drugRows').innerHTML = items.map(d => `
    <div class="card" style="display:flex;justify-content:space-between;align-items:center;">
      <div><div style="font-weight:700;">${escapeHtml(d.name)}</div><div class="muted" style="font-size:12.5px;">${escapeHtml(d.form || '')} ${escapeHtml(d.strength || '')}</div></div>
      <div style="display:flex;gap:8px;align-items:center;">
        <span class="stamp stamp-${d.isActive ? 'Completed' : 'Cancelled'}">${d.isActive ? 'Active' : 'Inactive'}</span>
        <button class="btn btn-ghost" onclick="toggleDrug(${d.id}, ${!d.isActive})">${d.isActive ? 'Deactivate' : 'Activate'}</button>
      </div>
    </div>`).join('') || '<div class="card muted">No drugs yet.</div>';
}
async function toggleDrug(id, isActive) {
  await apiRequest(`/drugs/${id}/toggle`, { method: 'PUT', body: { isActive } });
  loadDrugs();
}

async function loadAppointmentsTab() {
  await loadDoctorFilterOptions();
  loadAppointments();
}

async function loadAppointments() {
  const doctorId = document.getElementById('apptDoctorFilter').value;
  const appts = await apiRequest('/appointments' + (doctorId ? `?doctorId=${doctorId}` : ''));
  document.getElementById('apptRows').innerHTML = appts.map(a => `
    <div class="card">
      <div style="display:flex;justify-content:space-between;gap:10px;flex-wrap:wrap;">
        <div>
          <div style="font-weight:700;">${escapeHtml(a.patient)} → Dr. ${escapeHtml(a.doctor)}</div>
          <div class="muted" style="font-size:12.5px;">${fmtDate(a.appointmentDate)} at ${a.appointmentTime} · ₦${Number(a.feeAmount).toLocaleString()}</div>
        </div>
        <span class="stamp stamp-${a.status}">${a.status}</span>
      </div>
    </div>`).join('') || '<div class="card muted">No appointments match this filter.</div>';
}

async function loadComplaints() {
  const status = document.getElementById('complaintStatusFilter').value;
  const all = await apiRequest('/complaints');
  const items = status ? all.filter(c => c.status === status) : all;
  document.getElementById('complaintRows').innerHTML = items.map(c => `
    <div class="card">
      <div style="display:flex;justify-content:space-between;gap:10px;flex-wrap:wrap;">
        <div>
          <div style="font-weight:700;">${escapeHtml(c.subject)}</div>
          <div class="muted" style="font-size:12.5px;">From ${escapeHtml(c.patient)}${c.doctor ? ' · about Dr. ' + escapeHtml(c.doctor) : ''} · ${fmtDateTime(c.createdAt)}</div>
          <div style="font-size:13px;margin-top:8px;">${escapeHtml(c.message)}</div>
        </div>
        <span class="stamp stamp-${c.status}">${c.status}</span>
      </div>
      <div style="margin-top:14px;border-top:1px solid var(--line);padding-top:14px;">
        <textarea id="resp-${c.id}" placeholder="Response to the patient" style="min-height:60px;margin-bottom:8px;">${escapeHtml(c.adminResponse || '')}</textarea>
        <div style="display:flex;gap:8px;">
          <button class="btn btn-ghost" onclick="respondComplaint(${c.id}, 'InReview')">Mark in review</button>
          <button class="btn btn-approve" onclick="respondComplaint(${c.id}, 'Resolved')">Mark resolved</button>
        </div>
      </div>
    </div>`).join('') || '<div class="card muted">No complaints.</div>';
}
async function respondComplaint(id, status) {
  const response = document.getElementById(`resp-${id}`).value;
  await apiRequest(`/complaints/${id}/respond`, { method: 'PUT', body: { status, adminResponse: response } });
  loadComplaints();
}

async function loadPayments() {
  const items = await apiRequest('/payments');
  document.getElementById('paymentRows').innerHTML = items.map(p => `
    <div class="card" style="display:flex;justify-content:space-between;align-items:center;">
      <div><div style="font-weight:700;">${escapeHtml(p.patient)}</div><div class="muted" style="font-size:12.5px;">Ref: ${escapeHtml(p.reference)} · ${fmtDateTime(p.paymentDate)}</div></div>
      <div style="text-align:right;">
        <div class="fee-tag">₦${Number(p.amount).toLocaleString()}</div>
        <span class="stamp stamp-${p.status}">${p.status}</span>
      </div>
    </div>`).join('') || '<div class="card muted">No payments yet.</div>';
}
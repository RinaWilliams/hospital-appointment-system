Session.requireRole("Patient");
document.getElementById("welcomeMsg").textContent = `${Session.fullName()} · patient`;

document.addEventListener('DOMContentLoaded', function () {
  document.querySelectorAll('.tab').forEach(function (tab) {
    tab.addEventListener('click', function (e) {
      e.preventDefault();
      document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
      document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
      tab.classList.add('active');
      document.getElementById('panel-' + tab.dataset.tab).classList.add('active');
      if (tab.dataset.tab === 'find') loadFilters();
      if (tab.dataset.tab === 'mine') loadAppointments();
      if (tab.dataset.tab === 'records') loadRecords();
      if (tab.dataset.tab === 'complaints') { loadDoctorDropdownForComplaints(); loadComplaints(); }
      if (tab.dataset.tab === 'reminders') renderCalendarSync('calendarSyncMount');
      if (tab.dataset.tab === 'profile') loadProfile();
    });
  });

  document.querySelectorAll('.subtab').forEach(function (t) {
    t.addEventListener('click', function (e) {
      e.preventDefault();
      document.querySelectorAll('.subtab').forEach(x => x.classList.remove('active'));
      t.classList.add('active');
      loadAppointments();
    });
  });

  document.querySelectorAll('.subtab-record').forEach(function (t) {
    t.addEventListener('click', function (e) {
      e.preventDefault();
      document.querySelectorAll('.subtab-record').forEach(x => x.classList.remove('active'));
      t.classList.add('active');
      loadRecords();
    });
  });

  document.getElementById('complaintForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const errorBox = document.getElementById('complaintError');
    errorBox.style.display = 'none';

    const subject = document.getElementById('complaintSubject').value.trim();
    const message = document.getElementById('complaintMessage').value.trim();
    const doctorId = document.getElementById('complaintDoctor').value;

    if (!subject) { errorBox.textContent = 'Please enter a subject for your complaint.'; errorBox.style.display = 'block'; return; }
    if (subject.length < 5) { errorBox.textContent = 'Subject must be at least 5 characters.'; errorBox.style.display = 'block'; return; }
    if (!message) { errorBox.textContent = 'Please describe the issue in the message box.'; errorBox.style.display = 'block'; return; }
    if (message.length < 15) { errorBox.textContent = 'Please give a bit more detail — at least 15 characters.'; errorBox.style.display = 'block'; return; }

    try {
      await apiRequest('/complaints', {
        method: 'POST',
        body: { subject, message, doctorUserId: doctorId ? parseInt(doctorId) : null }
      });
      e.target.reset();
      loadComplaints();
    } catch (err) {
      errorBox.textContent = err.message;
      errorBox.style.display = 'block';
    }
  });

  document.getElementById('profileForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const msgBox = document.getElementById('profileMessage');
    const name = document.getElementById('profileName').value.trim();
    if (!name) { msgBox.innerHTML = '<div class="error-text">Please enter your full name.</div>'; return; }
    if (name.length < 3) { msgBox.innerHTML = '<div class="error-text">Full name must be at least 3 characters.</div>'; return; }
    try {
      await apiRequest('/profile/me', {
        method: 'PUT',
        body: {
          fullName: name,
          phone: document.getElementById('profilePhone').value.trim(),
          medicalHistory: document.getElementById('profileMedicalHistory').value
        }
      });
      msgBox.innerHTML = '<div class="success-text">Profile updated.</div>';
    } catch (err) { msgBox.innerHTML = `<div class="error-text">${err.message}</div>`; }
  });

  loadFilters();
});

async function loadFilters() {
  const [doctors, facilities] = await Promise.all([apiRequest('/doctors', { auth: false }), apiRequest('/facilities', { auth: false })]);
  const specialties = [...new Set(doctors.map(d => d.specialization))];
  document.getElementById('filterSpecialty').innerHTML = '<option value="">All specialties</option>' +
    specialties.map(s => `<option value="${escapeHtml(s)}">${escapeHtml(s)}</option>`).join('');
  document.getElementById('filterFacility').innerHTML = '<option value="">All facilities</option>' +
    facilities.map(f => `<option value="${f.id}">${escapeHtml(f.name)}</option>`).join('');
  renderDoctors(doctors);
}

async function searchDoctors() {
  const specialization = document.getElementById('filterSpecialty').value;
  const facilityId = document.getElementById('filterFacility').value;
  const params = new URLSearchParams();
  if (specialization) params.set('specialization', specialization);
  if (facilityId) params.set('facilityId', facilityId);
  const doctors = await apiRequest('/doctors?' + params.toString(), { auth: false });
  renderDoctors(doctors);
}

function renderDoctors(doctors) {
  const container = document.getElementById('doctorResults');
  if (!doctors.length) { container.innerHTML = '<div class="card muted">No doctors are currently accepting bookings.</div>'; return; }
  container.innerHTML = doctors.map(d => `
    <div class="card" id="doctor-${d.id}">
      <div style="display:flex;justify-content:space-between;align-items:flex-start;gap:12px;flex-wrap:wrap;">
        <div>
          <div style="font-family:var(--font-display);font-weight:700;font-size:16px;">Dr. ${escapeHtml(d.fullName)}</div>
          <div class="muted" style="font-size:12.5px;margin-top:4px;">${escapeHtml(d.specialization)}${d.facility ? ' · ' + escapeHtml(d.facility) : ''}</div>
          ${d.bio ? `<div style="font-size:13px;margin-top:8px;">${escapeHtml(d.bio)}</div>` : ''}
          <div class="fee-tag" style="margin-top:8px;">Consultation fee: ₦${Number(d.consultationFee).toLocaleString()}</div>
        </div>
        <button class="btn" onclick="showBookForm(${d.id})">Book</button>
      </div>
      <div id="book-form-${d.id}"></div>
    </div>
  `).join('');
}

function showBookForm(doctorId) {
  document.getElementById('book-form-' + doctorId).innerHTML = `
    <div style="margin-top:14px;border-top:1px solid var(--line);padding-top:14px;">
      <label class="field">
        <span class="field-label">Date</span>
        <input type="date" id="book-date-${doctorId}" min="${todayISO()}" onchange="loadSlots(${doctorId})">
      </label>
      <label class="field">
        <span class="field-label">Available time</span>
        <select id="book-time-${doctorId}"><option value="">Pick a date first</option></select>
      </label>
      <label class="field">
        <span class="field-label">Reason for visit</span>
        <textarea id="book-reason-${doctorId}" placeholder="Briefly describe why you need this appointment"></textarea>
      </label>
      <div class="error-text" id="book-error-${doctorId}" style="display:none;"></div>
      <div style="display:flex;gap:8px;">
        <button class="btn" onclick="submitBooking(${doctorId})">Request appointment</button>
        <button class="btn btn-ghost" onclick="document.getElementById('book-form-${doctorId}').innerHTML=''">Cancel</button>
      </div>
    </div>`;
}

// ===== Strict past-time slot filter =====
async function loadSlots(doctorId) {
  const dateEl = document.getElementById(`book-date-${doctorId}`);
  const select = document.getElementById(`book-time-${doctorId}`);
  const date = dateEl.value;

  if (!date) { select.innerHTML = '<option value="">Pick a date first</option>'; return; }
  select.innerHTML = '<option value="">Loading…</option>';

  const now = new Date();
  const todayStr = now.getFullYear() + '-' +
    String(now.getMonth() + 1).padStart(2, '0') + '-' +
    String(now.getDate()).padStart(2, '0');

  if (date < todayStr) {
    select.innerHTML = '<option value="">You cannot book a past date</option>';
    return;
  }

  const slots = await apiRequest(`/doctor-schedules/${doctorId}/slots?date=${date}`, { auth: false });

  // If today, hide any slot whose start time is already at or before now.
  let filtered = slots;
  if (date === todayStr) {
    const nowMinutes = now.getHours() * 60 + now.getMinutes();
    filtered = slots.filter(s => {
      const [hh, mm] = s.split(':').map(Number);
      return (hh * 60 + mm) > nowMinutes;
    });
  }

  if (!filtered.length) {
    select.innerHTML = '<option value="">No open slots available for this date</option>';
    return;
  }
  select.innerHTML = filtered.map(s => `<option value="${s}">${s}</option>`).join('');
}

async function submitBooking(doctorId) {
  const errorBox = document.getElementById(`book-error-${doctorId}`);
  errorBox.style.display = 'none';
  const date = document.getElementById(`book-date-${doctorId}`).value;
  const time = document.getElementById(`book-time-${doctorId}`).value;
  const reason = document.getElementById(`book-reason-${doctorId}`).value.trim();

  if (!date) { errorBox.textContent = 'Please pick a date for your appointment.'; errorBox.style.display = 'block'; return; }
  if (!time) { errorBox.textContent = 'Please pick a time slot. If none appear, choose a different date.'; errorBox.style.display = 'block'; return; }
  if (!reason) { errorBox.textContent = 'Please tell the doctor why you need this appointment.'; errorBox.style.display = 'block'; return; }
  if (reason.length < 5) { errorBox.textContent = 'Please add a bit more detail to the reason (at least 5 characters).'; errorBox.style.display = 'block'; return; }

  // Strict past-time check
  const [y, m, d] = date.split('-').map(Number);
  const [hh, mm] = time.split(':').map(Number);
  const chosen = new Date(y, m - 1, d, hh, mm, 0);
  const now = new Date();
  if (chosen.getTime() <= now.getTime()) {
    errorBox.textContent = 'That time has already passed — please pick a later slot.';
    errorBox.style.display = 'block';
    return;
  }

  try {
    const result = await apiRequest('/appointments', { method: 'POST', body: { doctorUserId: doctorId, date, time, reason } });
    document.getElementById(`book-form-${doctorId}`).innerHTML = `
      <div class="payment-box">
        <div style="font-weight:700;margin-bottom:6px;">Payment required to confirm this slot</div>
        <div class="fee-tag" style="margin-bottom:12px;">Consultation fee: ₦${Number(result.feeAmount).toLocaleString()}</div>
        <button class="btn" onclick="payNow(${result.paymentId}, ${doctorId})">Pay now (demo)</button>
      </div>`;
  } catch (err) { errorBox.textContent = err.message; errorBox.style.display = 'block'; }
}

async function payNow(paymentId, doctorId) {
  try {
    await apiRequest(`/payments/${paymentId}/confirm`, { method: 'PUT', body: { succeed: true } });
    document.getElementById(`book-form-${doctorId}`).innerHTML = '<div class="success-text" style="margin-top:14px;">Payment confirmed — your appointment is now awaiting the doctor\'s response. Check "My appointments".</div>';
  } catch (err) { alert(err.message); }
}

async function loadAppointments() {
  const mode = document.querySelector('.subtab.active').dataset.subtab;
  const container = document.getElementById('myAppointments');
  container.innerHTML = '<div class="card muted">Loading…</div>';

  const all = await apiRequest('/appointments');
  const upcomingStatuses = ['PendingPayment', 'Pending', 'Approved'];
  const list = all.filter(a => mode === 'upcoming' ? upcomingStatuses.includes(a.status) : !upcomingStatuses.includes(a.status));

  if (!list.length) { container.innerHTML = `<div class="card muted">No ${mode === 'upcoming' ? 'upcoming appointments' : 'appointment history yet'}.</div>`; return; }

  container.innerHTML = list.map(a => `
    <div class="card" id="appt-${a.id}">
      <div style="display:flex;justify-content:space-between;align-items:flex-start;gap:12px;flex-wrap:wrap;">
        <div>
          <div style="font-family:var(--font-display);font-weight:700;font-size:16px;">Dr. ${escapeHtml(a.doctor)} — ${escapeHtml(a.specialization || '')}</div>
          <div class="muted" style="font-size:12.5px;margin-top:4px;">${fmtDate(a.appointmentDate)} at ${a.appointmentTime}</div>
          <div style="font-size:13px;margin-top:8px;">${escapeHtml(a.reason)}</div>
          ${a.doctorNote ? `<div style="font-size:13px;margin-top:8px;"><b>Doctor's note:</b> ${escapeHtml(a.doctorNote)}</div>` : ''}
          <div class="fee-tag" style="margin-top:6px;">Fee: ₦${Number(a.feeAmount).toLocaleString()}${a.paymentReference ? ' · Ref: ' + a.paymentReference : ''}</div>
        </div>
        <div style="display:flex;flex-direction:column;align-items:flex-end;gap:8px;">
          <span class="stamp stamp-${a.status}">${a.status.replace(/([A-Z])/g, ' $1').trim()}</span>
          <div style="display:flex;gap:6px;flex-wrap:wrap;justify-content:flex-end;">
            ${a.status === 'PendingPayment' ? `<button class="btn" onclick="retryPayment(${a.id})">Pay now</button>` : ''}
            ${['Pending', 'Approved'].includes(a.status) ? `<button class="btn btn-ghost" onclick="showRescheduleForm(${a.id}, ${a.doctorUserId})">Reschedule</button>` : ''}
            ${['PendingPayment', 'Pending', 'Approved'].includes(a.status) ? `<button class="btn btn-reject" onclick="cancelAppt(${a.id})">Cancel</button>` : ''}
            ${['Approved', 'Completed'].includes(a.status) ? `<button class="btn btn-ghost" onclick="downloadFile('/appointments/${a.id}/ics', 'appointment-${a.id}.ics')">Add to calendar</button>` : ''}
          </div>
        </div>
      </div>
      <div id="reschedule-form-${a.id}"></div>
    </div>
  `).join('');
}

async function retryPayment(apptId) {
  const payments = await apiRequest('/payments');
  const payment = payments.find(p => p.appointmentId === apptId);
  if (!payment) { alert('Could not find the linked payment.'); return; }
  try {
    await apiRequest(`/payments/${payment.id}/confirm`, { method: 'PUT', body: { succeed: true } });
    loadAppointments();
  } catch (err) { alert(err.message); }
}

function showRescheduleForm(apptId, doctorId) {
  document.getElementById(`reschedule-form-${apptId}`).innerHTML = `
    <div style="margin-top:14px;border-top:1px solid var(--line);padding-top:14px;">
      <label class="field">
        <span class="field-label">New date</span>
        <input type="date" id="resch-date-${apptId}" min="${todayISO()}" onchange="loadRescheduleSlots(${apptId}, ${doctorId})">
      </label>
      <label class="field">
        <span class="field-label">New time</span>
        <select id="resch-time-${apptId}"><option value="">Pick a date first</option></select>
      </label>
      <div class="error-text" id="resch-error-${apptId}" style="display:none;"></div>
      <div style="display:flex;gap:8px;">
        <button class="btn" onclick="submitReschedule(${apptId})">Confirm new time</button>
        <button class="btn btn-ghost" onclick="document.getElementById('reschedule-form-${apptId}').innerHTML=''">Cancel</button>
      </div>
    </div>`;
}

async function loadRescheduleSlots(apptId, doctorId) {
  const date = document.getElementById(`resch-date-${apptId}`).value;
  const select = document.getElementById(`resch-time-${apptId}`);
  if (!date) return;
  select.innerHTML = '<option value="">Loading…</option>';
  const slots = await apiRequest(`/doctor-schedules/${doctorId}/slots?date=${date}&excludeAppointmentId=${apptId}`, { auth: false });
  select.innerHTML = slots.length ? slots.map(s => `<option value="${s}">${s}</option>`).join('') : '<option value="">No open slots that day</option>';
}

async function submitReschedule(apptId) {
  const errorBox = document.getElementById(`resch-error-${apptId}`);
  errorBox.style.display = 'none';
  const date = document.getElementById(`resch-date-${apptId}`).value;
  const time = document.getElementById(`resch-time-${apptId}`).value;
  if (!date) { errorBox.textContent = 'Please pick a new date.'; errorBox.style.display = 'block'; return; }
  if (!time) { errorBox.textContent = 'Please pick a new time slot.'; errorBox.style.display = 'block'; return; }
  try {
    await apiRequest(`/appointments/${apptId}/reschedule`, { method: 'PUT', body: { date, time } });
    loadAppointments();
  } catch (err) { errorBox.textContent = err.message; errorBox.style.display = 'block'; }
}

async function cancelAppt(id) {
  if (!confirm('Cancel this appointment?')) return;
  try { await apiRequest(`/appointments/${id}/cancel`, { method: 'PUT' }); loadAppointments(); }
  catch (err) { alert(err.message); }
}

async function loadRecords() {
  const mode = document.querySelector('.subtab-record.active').dataset.record;
  const container = document.getElementById('recordsList');
  container.innerHTML = '<div class="card muted">Loading…</div>';

  if (mode === 'prescriptions') {
    const items = await apiRequest('/prescriptions');
    container.innerHTML = items.length ? items.map(p => `
      <div class="card">
        <div style="font-weight:700;font-size:15px;">${escapeHtml(p.medication)} ${p.quantity ? '· Qty ' + p.quantity : ''}</div>
        <div class="muted" style="font-size:12.5px;margin-top:4px;">Prescribed by ${escapeHtml(p.doctor)} · ${fmtDateTime(p.issuedDate)}</div>
        <div style="font-size:13px;margin-top:8px;">${escapeHtml(p.dosage)}${p.instructions ? ' — ' + escapeHtml(p.instructions) : ''}</div>
        <span class="stamp stamp-${p.status}" style="margin-top:8px;">${p.status}</span>
      </div>`).join('') : '<div class="card muted">No prescriptions yet.</div>';
  } else {
    const items = await apiRequest('/laborders');
    container.innerHTML = items.length ? items.map(t => `
      <div class="card">
        <div style="font-weight:700;font-size:15px;">${escapeHtml(t.testName)}</div>
        <div class="muted" style="font-size:12.5px;margin-top:4px;">Ordered by ${escapeHtml(t.doctor)} · ${fmtDateTime(t.requestedDate)}</div>
        ${t.resultText ? `<div style="font-size:13px;margin-top:8px;white-space:pre-wrap;"><b>Result:</b> ${escapeHtml(t.resultText)}</div>` : ''}
        <span class="stamp stamp-${t.status}" style="margin-top:8px;">${t.status}</span>
      </div>`).join('') : '<div class="card muted">No lab orders yet.</div>';
  }
}

async function loadDoctorDropdownForComplaints() {
  const select = document.getElementById('complaintDoctor');
  if (!select) return;
  select.innerHTML = '<option value="">Loading doctors…</option>';
  try {
    const doctors = await apiRequest('/doctors/all');
    if (!doctors || !doctors.length) {
      select.innerHTML = '<option value="">No registered doctors yet</option>';
      return;
    }
    select.innerHTML = '<option value="">Not about a specific doctor</option>' +
      doctors.map(d => `<option value="${d.id}">Dr. ${escapeHtml(d.fullName)}${d.specialization ? ' — ' + escapeHtml(d.specialization) : ''}</option>`).join('');
  } catch (err) {
    console.error('Doctor dropdown failed:', err);
    select.innerHTML = '<option value="">Not about a specific doctor</option>';
  }
}

async function loadComplaints() {
  const items = await apiRequest('/complaints');
  document.getElementById('complaintsList').innerHTML = items.length ? items.map(c => `
    <div class="card">
      <div style="display:flex;justify-content:space-between;gap:10px;flex-wrap:wrap;">
        <div style="font-weight:700;">${escapeHtml(c.subject)}</div>
        <span class="stamp stamp-${c.status}">${c.status}</span>
      </div>
      ${c.doctor ? `<div class="muted" style="font-size:12.5px;margin-top:4px;">About: Dr. ${escapeHtml(c.doctor)}</div>` : ''}
      <div style="font-size:13px;margin-top:8px;">${escapeHtml(c.message)}</div>
      ${c.adminResponse ? `<div style="font-size:13px;margin-top:8px;"><b>Response:</b> ${escapeHtml(c.adminResponse)}</div>` : ''}
      <div class="fee-tag" style="margin-top:6px;">${fmtDateTime(c.createdAt)}</div>
    </div>`).join('') : '<div class="card muted">No complaints submitted.</div>';
}

async function loadProfile() {
  const me = await apiRequest('/profile/me');
  document.getElementById('profileName').value = me.fullName;
  document.getElementById('profileEmail').value = me.email;
  document.getElementById('profilePhone').value = me.phone || '';
  document.getElementById('profileMedicalHistory').value = me.medicalHistory || '';
}
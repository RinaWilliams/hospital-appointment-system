Session.requireRole("Doctor");
document.getElementById("welcomeMsg").textContent = `Dr. ${Session.fullName()} · doctor`;

document.addEventListener('DOMContentLoaded', function () {
  document.querySelectorAll('.tab').forEach(function (tab) {
    tab.addEventListener('click', function (e) {
      e.preventDefault();
      document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
      document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
      tab.classList.add('active');
      document.getElementById('panel-' + tab.dataset.tab).classList.add('active');
      if (tab.dataset.tab === 'requests' || tab.dataset.tab === 'upcoming') loadAppointments();
      if (tab.dataset.tab === 'availability') { loadSchedule(); loadTimeOff(); loadAvailability(); }
      if (tab.dataset.tab === 'prescribe') { loadPatientPrescriptionsAndLabs(); setupDrugAutocomplete(); }
      if (tab.dataset.tab === 'records') loadPatientRecords();
      if (tab.dataset.tab === 'profile') loadProfile();
    });
  });

  document.getElementById('scheduleForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const errorBox = document.getElementById('scheduleError');
    errorBox.style.display = 'none';
    const startTime = document.getElementById('startTime').value;
    const endTime = document.getElementById('endTime').value;
    if (!startTime) { errorBox.textContent = 'Please enter a start time.'; errorBox.style.display = 'block'; return; }
    if (!endTime) { errorBox.textContent = 'Please enter an end time.'; errorBox.style.display = 'block'; return; }
    if (startTime >= endTime) { errorBox.textContent = 'Start time must be before end time.'; errorBox.style.display = 'block'; return; }
    try {
      await apiRequest('/doctor-schedules', {
        method: 'POST',
        body: { dayOfWeek: parseInt(document.getElementById('dayOfWeek').value), startTime, endTime }
      });
      e.target.reset();
      loadSchedule();
    } catch (err) { errorBox.textContent = err.message; errorBox.style.display = 'block'; }
  });

  document.getElementById('timeOffForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const errorBox = document.getElementById('timeOffError');
    errorBox.style.display = 'none';
    const date = document.getElementById('timeOffDate').value;
    if (!date) { errorBox.textContent = 'Please pick the date you will be unavailable.'; errorBox.style.display = 'block'; return; }
    try {
      await apiRequest('/doctor-schedules/time-off', {
        method: 'POST',
        body: {
          date,
          startTime: document.getElementById('timeOffStart').value || null,
          endTime: document.getElementById('timeOffEnd').value || null,
          reason: document.getElementById('timeOffReason').value
        }
      });
      e.target.reset();
      loadTimeOff();
    } catch (err) { errorBox.textContent = err.message; errorBox.style.display = 'block'; }
  });

  document.getElementById('prescForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const msgBox = document.getElementById('prescMessage');
    const patientId = document.getElementById('patientId').value;
    const medication = document.getElementById('medication').value.trim();
    const dosage = document.getElementById('dosage').value.trim();

    if (!patientId) { msgBox.innerHTML = '<div class="error-text">Please enter the patient\'s user ID.</div>'; return; }
    if (!medication) { msgBox.innerHTML = '<div class="error-text">Please enter a medication name.</div>'; return; }
    if (!dosage) { msgBox.innerHTML = '<div class="error-text">Please enter the dosage (e.g. 500mg twice daily).</div>'; return; }

    try {
      await apiRequest('/prescriptions', {
        method: 'POST',
        body: {
          patientUserId: parseInt(patientId),
          drugId: document.getElementById('drugId').value ? parseInt(document.getElementById('drugId').value) : null,
          medication,
          dosage,
          instructions: document.getElementById('instructions').value,
          quantity: document.getElementById('quantity').value ? parseInt(document.getElementById('quantity').value) : null
        }
      });
      e.target.reset();
      document.getElementById('patientId').value = patientId;
      document.getElementById('drugId').value = '';
      msgBox.innerHTML = '<div class="success-text">Prescription issued.</div>';
      loadPatientPrescriptionsAndLabs();
    } catch (err) { msgBox.innerHTML = `<div class="error-text">${err.message}</div>`; }
  });

  document.getElementById('labForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const msgBox = document.getElementById('labMessage');
    const patientId = document.getElementById('labPatientId').value;
    const testName = document.getElementById('testName').value.trim();
    if (!patientId) { msgBox.innerHTML = '<div class="error-text">Please enter the patient\'s user ID.</div>'; return; }
    if (!testName) { msgBox.innerHTML = '<div class="error-text">Please enter the name of the test.</div>'; return; }
    try {
      await apiRequest('/laborders', {
        method: 'POST',
        body: { patientUserId: parseInt(patientId), testName, notes: document.getElementById('labNotes').value }
      });
      e.target.reset();
      document.getElementById('labPatientId').value = patientId;
      document.getElementById('patientId').value = patientId;
      msgBox.innerHTML = '<div class="success-text">Test ordered.</div>';
      loadPatientPrescriptionsAndLabs();
    } catch (err) { msgBox.innerHTML = `<div class="error-text">${err.message}</div>`; }
  });

  document.getElementById('recordForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const msgBox = document.getElementById('recordMessage');
    const patientId = document.getElementById('recordPatientId').value;
    const notes = document.getElementById('notes').value.trim();
    if (!patientId) { msgBox.innerHTML = '<div class="error-text">Please enter the patient\'s user ID.</div>'; return; }
    if (!notes) { msgBox.innerHTML = '<div class="error-text">Please write the patient notes before saving.</div>'; return; }
    try {
      await apiRequest('/patientrecords', { method: 'POST', body: { patientUserId: parseInt(patientId), notes } });
      e.target.reset();
      document.getElementById('recordPatientId').value = patientId;
      msgBox.innerHTML = '<div class="success-text">Note saved.</div>';
      loadPatientRecords();
    } catch (err) { msgBox.innerHTML = `<div class="error-text">${err.message}</div>`; }
  });

  document.getElementById('profileForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const msgBox = document.getElementById('profileMessage');
    const name = document.getElementById('profileName').value.trim();
    if (!name) { msgBox.innerHTML = '<div class="error-text">Please enter your name.</div>'; return; }
    try {
      await apiRequest('/profile/me', {
        method: 'PUT',
        body: {
          fullName: name,
          phone: document.getElementById('profilePhone').value,
          bio: document.getElementById('profileBio').value,
          consultationFee: parseFloat(document.getElementById('profileFee').value || 0),
          slotDurationMinutes: parseInt(document.getElementById('profileSlotDuration').value || 30),
          facilityId: document.getElementById('profileFacility').value ? parseInt(document.getElementById('profileFacility').value) : null
        }
      });
      msgBox.innerHTML = '<div class="success-text">Profile updated.</div>';
    } catch (err) { msgBox.innerHTML = `<div class="error-text">${err.message}</div>`; }
  });

  loadAppointments();
});


async function loadAvailability() {
  try {
    const info = await apiRequest('/doctor-availability/me');
    const toggle = document.getElementById('availabilityToggle');
    const note = document.getElementById('availabilityNote');
    const status = document.getElementById('availabilityStatus');
    if (!toggle) return;
    toggle.checked = !!info.isAcceptingBookings;
    if (note) note.value = info.note || '';
    renderAvailabilityStatus(info.isAcceptingBookings, status);
  } catch (err) {
    const status = document.getElementById('availabilityStatus');
    if (status) status.innerHTML = `<span style="color:var(--red);font-size:12.5px;">Could not load availability: ${escapeHtml(err.message)}</span>`;
  }
}

function renderAvailabilityStatus(on, el) {
  if (!el) return;
  el.innerHTML = on
    ? '<span style="color:var(--primary-700);font-weight:600;font-size:13px;">✓ Available — patients can book you right now.</span>'
    : '<span style="color:var(--amber);font-weight:600;font-size:13px;">⏸ Paused — patients cannot book you. Turn this on when you are ready.</span>';
}

async function toggleAvailability() {
  const toggle = document.getElementById('availabilityToggle');
  const note = document.getElementById('availabilityNote');
  const status = document.getElementById('availabilityStatus');
  const wantsOn = toggle.checked;
  try {
    await apiRequest('/doctor-availability/me', {
      method: 'PUT',
      body: { isAcceptingBookings: wantsOn, note: note ? note.value : null }
    });
    renderAvailabilityStatus(wantsOn, status);
  } catch (err) {
    toggle.checked = !wantsOn;
    alert(err.message || 'Could not update availability.');
  }
}

async function saveAvailabilityNote() {
  const toggle = document.getElementById('availabilityToggle');
  const note = document.getElementById('availabilityNote');
  try {
    await apiRequest('/doctor-availability/me', {
      method: 'PUT',
      body: { isAcceptingBookings: toggle.checked, note: note.value }
    });
    alert('Note saved.');
  } catch (err) { alert(err.message); }
}


async function loadAppointments() {
  const all = await apiRequest('/appointments');
  const requests = all.filter(a => a.status === 'Pending');
  const upcoming = all.filter(a => a.status === 'Approved');

  document.getElementById('requestsList').innerHTML = requests.length ? requests.map(a => `
    <div class="card">
      <div style="display:flex;justify-content:space-between;align-items:flex-start;gap:12px;flex-wrap:wrap;">
        <div>
          <div style="font-weight:700;font-size:16px;">${escapeHtml(a.patient)} (ID: ${a.patientUserId})${a.patientPhone ? ' · ' + escapeHtml(a.patientPhone) : ''}</div>
          <div class="muted" style="font-size:12.5px;margin-top:4px;">${fmtDate(a.appointmentDate)} at ${a.appointmentTime}</div>
          <div style="font-size:13px;margin-top:8px;">${escapeHtml(a.reason)}</div>
        </div>
        <span class="stamp stamp-${a.status}">${a.status}</span>
      </div>
      <div style="margin-top:14px;border-top:1px solid var(--line);padding-top:14px;">
        <textarea id="note-${a.id}" placeholder="Optional note to the patient" style="min-height:50px;margin-bottom:8px;"></textarea>
        <div style="display:flex;gap:8px;flex-wrap:wrap;">
          <button class="btn btn-approve" onclick="respond(${a.id}, 'Approved')">Approve</button>
          <button class="btn btn-reject" onclick="respond(${a.id}, 'Rejected')">Reject</button>
          <button class="btn btn-ghost" onclick="goToPatient('records', ${a.patientUserId})">View record</button>
        </div>
      </div>
    </div>`).join('') : '<div class="card muted">No pending requests.</div>';

  document.getElementById('upcomingList').innerHTML = upcoming.length ? upcoming.map(a => `
    <div class="card">
      <div style="display:flex;justify-content:space-between;align-items:flex-start;gap:12px;flex-wrap:wrap;">
        <div>
          <div style="font-weight:700;font-size:16px;">${escapeHtml(a.patient)} (ID: ${a.patientUserId})</div>
          <div class="muted" style="font-size:12.5px;margin-top:4px;">${fmtDate(a.appointmentDate)} at ${a.appointmentTime}</div>
          <div style="font-size:13px;margin-top:8px;">${escapeHtml(a.reason)}</div>
        </div>
        <div style="display:flex;flex-direction:column;align-items:flex-end;gap:8px;">
          <span class="stamp stamp-${a.status}">${a.status}</span>
          <button class="btn" onclick="complete(${a.id})">Mark completed</button>
        </div>
      </div>
      <div style="margin-top:14px;border-top:1px solid var(--line);padding-top:14px;display:flex;gap:8px;flex-wrap:wrap;">
        <button class="btn btn-ghost" onclick="goToPatient('records', ${a.patientUserId})">View record</button>
        <button class="btn btn-ghost" onclick="goToPatient('prescribe', ${a.patientUserId})">Prescriptions &amp; labs</button>
        <button class="btn btn-ghost" onclick="downloadFile('/appointments/${a.id}/ics', 'appointment-${a.id}.ics')">Add to calendar</button>
      </div>
    </div>`).join('') : '<div class="card muted">No approved appointments yet.</div>';
}

function goToPatient(tabName, patientUserId) {
  document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
  document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
  document.querySelector(`.tab[data-tab="${tabName}"]`).classList.add('active');
  document.getElementById('panel-' + tabName).classList.add('active');
  if (tabName === 'records') {
    document.getElementById('recordPatientId').value = patientUserId;
    loadPatientRecords();
  } else {
    document.getElementById('patientId').value = patientUserId;
    document.getElementById('labPatientId').value = patientUserId;
    loadPatientPrescriptionsAndLabs();
  }
}

async function loadPatientPrescriptionsAndLabs() {
  const patientUserId = document.getElementById('patientId').value;
  const container = document.getElementById('patientPrescriptionsAndLabs');
  if (!patientUserId) { container.innerHTML = '<div class="card muted">Enter a patient user ID above to see their history here.</div>'; return; }
  container.innerHTML = '<div class="card muted">Loading…</div>';
  const [prescriptions, labs] = await Promise.all([
    apiRequest(`/prescriptions?patientUserId=${patientUserId}`),
    apiRequest(`/laborders?patientUserId=${patientUserId}`)
  ]);
  const rows = [
    ...prescriptions.map(p => `
      <div class="card">
        <div style="font-weight:700;">💊 ${escapeHtml(p.medication)}${p.quantity ? ' · Qty ' + p.quantity : ''}</div>
        <div class="muted" style="font-size:12.5px;margin-top:4px;">${escapeHtml(p.dosage)}${p.instructions ? ' — ' + escapeHtml(p.instructions) : ''} · ${fmtDateTime(p.issuedDate)}</div>
        <span class="stamp stamp-${p.status}" style="margin-top:6px;">${p.status}</span>
      </div>`),
    ...labs.map(t => `
      <div class="card">
        <div style="font-weight:700;">🧪 ${escapeHtml(t.testName)}</div>
        <div class="muted" style="font-size:12.5px;margin-top:4px;">${fmtDateTime(t.requestedDate)}</div>
        ${t.resultText ? `<div style="font-size:13px;margin-top:6px;"><b>Result:</b> ${escapeHtml(t.resultText)}</div>` : ''}
        <span class="stamp stamp-${t.status}" style="margin-top:6px;">${t.status}</span>
      </div>`)
  ];
  container.innerHTML = rows.join('') || '<div class="card muted">No prescriptions or lab orders for this patient yet.</div>';
}

async function loadPatientRecords() {
  const patientUserId = document.getElementById('recordPatientId').value;
  const container = document.getElementById('patientRecordsList');
  if (!patientUserId) { container.innerHTML = '<div class="card muted">Enter a patient user ID above to see their notes here.</div>'; return; }
  container.innerHTML = '<div class="card muted">Loading…</div>';
  const records = await apiRequest(`/patientrecords?patientUserId=${patientUserId}`);
  container.innerHTML = records.map(r => `
    <div class="card">
      <div style="font-size:13px;white-space:pre-wrap;">${escapeHtml(r.notes)}</div>
      <div class="fee-tag" style="margin-top:8px;">${fmtDateTime(r.recordDate)}</div>
    </div>`).join('') || '<div class="card muted">No notes for this patient yet.</div>';
}

async function respond(id, status) {
  const note = document.getElementById(`note-${id}`)?.value.trim() || null;
  if (status === 'Rejected' && !note) {
    alert('Please add a short note explaining why you are rejecting this appointment — the patient will see it.');
    return;
  }
  try {
    await apiRequest(`/appointments/${id}/respond`, { method: 'PUT', body: { status, doctorNote: note } });
    loadAppointments();
  } catch (err) { alert(err.message); }
}

async function complete(id) {
  try { await apiRequest(`/appointments/${id}/complete`, { method: 'PUT' }); loadAppointments(); }
  catch (err) { alert(err.message); }
}

async function loadSchedule() {
  const items = await apiRequest('/doctor-schedules/mine');
  const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
  document.getElementById('scheduleList').innerHTML = items.length ? items.map(s => `
    <div class="card" style="display:flex;justify-content:space-between;align-items:center;">
      <span>${days[s.dayOfWeek]} · ${s.startTime.slice(0,5)} - ${s.endTime.slice(0,5)}</span>
      <button class="btn btn-ghost" onclick="removeSlot(${s.id})">Remove</button>
    </div>`).join('') : '<div class="card muted">No availability set yet.</div>';
}

async function removeSlot(id) {
  await apiRequest(`/doctor-schedules/${id}`, { method: 'DELETE' });
  loadSchedule();
}

async function loadTimeOff() {
  const items = await apiRequest('/doctor-schedules/time-off/mine');
  document.getElementById('timeOffList').innerHTML = items.length ? items.map(t => `
    <div class="card" style="display:flex;justify-content:space-between;align-items:center;">
      <div>
        <div style="font-weight:700;">${fmtDate(t.date)}</div>
        <div class="muted" style="font-size:12.5px;">${t.startTime && t.endTime ? t.startTime.slice(0,5) + ' - ' + t.endTime.slice(0,5) : 'Whole day'}${t.reason ? ' · ' + escapeHtml(t.reason) : ''}</div>
      </div>
      <button class="btn btn-ghost" onclick="removeTimeOff(${t.id})">Remove</button>
    </div>`).join('') : '<div class="card muted">No time off scheduled.</div>';
}

async function removeTimeOff(id) {
  await apiRequest(`/doctor-schedules/time-off/${id}`, { method: 'DELETE' });
  loadTimeOff();
}

async function loadProfile() {
  renderCalendarSync('calendarSyncMount');
  const [me, facilities] = await Promise.all([apiRequest('/profile/me'), apiRequest('/facilities', { auth: false })]);
  document.getElementById('profileName').value = me.fullName;
  document.getElementById('profilePhone').value = me.phone || '';
  document.getElementById('profileBio').value = me.bio || '';
  document.getElementById('profileFee').value = me.consultationFee || '';
  document.getElementById('profileSlotDuration').value = me.slotDurationMinutes || 30;
  document.getElementById('profileFacility').innerHTML = '<option value="">-</option>' +
    facilities.map(f => `<option value="${f.id}" ${f.id === me.facilityId ? 'selected' : ''}>${escapeHtml(f.name)}</option>`).join('');
}


function setupDrugAutocomplete() {
  const input = document.getElementById('medication');
  if (!input) return;
  if (input.dataset.autocompleteReady === '1') return;
  input.dataset.autocompleteReady = '1';

  let panel = document.getElementById('drugSuggestions');
  if (!panel) {
    panel = document.createElement('div');
    panel.id = 'drugSuggestions';
    panel.style.cssText = 'position:absolute;z-index:100;background:#fff;border:1.5px solid var(--border);border-radius:10px;box-shadow:var(--shadow-md);max-height:260px;overflow-y:auto;display:none;min-width:320px;';
    input.parentElement.style.position = 'relative';
    input.parentElement.appendChild(panel);
  }

  let timer;
  input.addEventListener('input', () => {
    clearTimeout(timer);
    const q = input.value.trim();
    if (q.length < 2) { panel.style.display = 'none'; return; }
    timer = setTimeout(async () => {
      try {
        const results = await apiRequest('/drugs/search?q=' + encodeURIComponent(q), { auth: false });
        if (!results.length) {
          panel.innerHTML = '<div style="padding:10px;font-size:12.5px;color:var(--muted);">No matches in the formulary.</div>';
          panel.style.display = 'block';
          return;
        }
        panel.innerHTML = results.map(r => {
          const strength = r.strength || '';
          const form = r.form || '';
          const sub = [strength, form].filter(x => x).join(' ');
          return `<div data-id="${r.id}" data-name="${escapeHtml(r.name)}" data-strength="${escapeHtml(strength)}" data-form="${escapeHtml(form)}"
                     style="padding:10px 12px;cursor:pointer;font-size:13px;border-bottom:1px solid var(--border);"
                     onmouseover="this.style.background='var(--primary-tint)'"
                     onmouseout="this.style.background='#fff'">
                    <b>${escapeHtml(r.name)}</b> <span class="muted">${escapeHtml(sub)}</span>
                  </div>`;
        }).join('');
        panel.style.display = 'block';

        panel.querySelectorAll('div[data-id]').forEach(el => {
          el.addEventListener('click', () => {
            const fullName = [el.dataset.name, el.dataset.strength, el.dataset.form].filter(x => x).join(' ');
            input.value = fullName;
            document.getElementById('drugId').value = el.dataset.id;
            // Focus dosage — the doctor types the schedule (e.g. "twice daily")
            const dose = document.getElementById('dosage');
            if (dose) dose.focus();
            panel.style.display = 'none';
          });
        });
      } catch { panel.style.display = 'none'; }
    }, 220);
  });

  input.addEventListener('blur', () => setTimeout(() => { panel.style.display = 'none'; }, 180));
}
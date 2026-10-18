Session.requireRole("Pharmacist");
document.getElementById("welcomeMsg").textContent = `${Session.fullName()} · pharmacist`;

document.addEventListener('DOMContentLoaded', function () {
  document.querySelectorAll('.tab').forEach(function (tab) {
    tab.addEventListener('click', function (e) {
      e.preventDefault();
      document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
      document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
      tab.classList.add('active');
      document.getElementById('panel-' + tab.dataset.tab).classList.add('active');
      if (tab.dataset.tab === 'formulary') loadFormulary();
      else loadPrescriptions();
    });
  });

  const drugForm = document.getElementById('drugForm');
  if (drugForm) {
    drugForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const name = document.getElementById('drugName').value.trim();
      const form = document.getElementById('drugForm2').value.trim();
      const strength = document.getElementById('drugStrength').value.trim();
      if (!name) { alert('Please enter the drug name.'); return; }
      try {
        await apiRequest('/drugs', { method: 'POST', body: { name, form, strength } });
        e.target.reset();
        loadFormulary();
      } catch (err) { alert(err.message); }
    });
  }

  loadPrescriptions();

  setInterval(function () {
    if (document.activeElement && ['INPUT', 'TEXTAREA', 'SELECT'].includes(document.activeElement.tagName)) return;
    loadPrescriptions();
  }, 30000);
});

async function loadPrescriptions() {
  const mode = document.querySelector('.tab.active').dataset.tab;
  if (mode === 'formulary') return;
  const containerId = mode === 'pending' ? 'pendingList' : 'dispensedList';
  const container = document.getElementById(containerId);
  container.innerHTML = '<div class="card muted">Loading…</div>';

  const all = await apiRequest('/prescriptions');
  const list = all.filter(p => mode === 'pending' ? p.status === 'Pending' : p.status === 'Dispensed');

  if (!list.length) { container.innerHTML = `<div class="card muted">No ${mode === 'pending' ? 'prescriptions waiting' : 'dispensed history yet'}.</div>`; return; }

  container.innerHTML = list.map(p => `
    <div class="card">
      <div style="display:flex;justify-content:space-between;align-items:flex-start;gap:12px;flex-wrap:wrap;">
        <div>
          <div style="font-family:var(--font-display);font-weight:700;font-size:16px;">${escapeHtml(p.medication)}${p.quantity ? ' · Qty ' + p.quantity : ''}</div>
          <div class="muted" style="font-size:12.5px;margin-top:4px;">For ${escapeHtml(p.patient)}${p.patientPhone ? ' · ' + escapeHtml(p.patientPhone) : ''} · prescribed by ${escapeHtml(p.doctor)}</div>
          <div style="font-size:13px;margin-top:8px;">${escapeHtml(p.dosage)}${p.instructions ? ' — ' + escapeHtml(p.instructions) : ''}</div>
          ${p.status === 'Dispensed' ? `<div class="fee-tag" style="margin-top:6px;">Dispensed ${p.dispensedDate ? fmtDateTime(p.dispensedDate) : ''}</div>` : ''}
        </div>
        <div style="display:flex;flex-direction:column;align-items:flex-end;gap:8px;">
          <span class="stamp stamp-${p.status}">${p.status}</span>
          ${p.status === 'Pending' ? `<button class="btn" onclick="dispense(${p.id})">Mark dispensed</button>` : ''}
        </div>
      </div>
    </div>`).join('');
}

async function dispense(id) {
  if (!confirm('Confirm this medication has been handed to the patient?')) return;
  try { await apiRequest(`/prescriptions/${id}/dispense`, { method: 'PUT' }); loadPrescriptions(); }
  catch (err) { alert(err.message); }
}

async function loadFormulary() {
  const container = document.getElementById('formularyList');
  container.innerHTML = '<div class="card muted">Loading…</div>';
  const items = await apiRequest('/drugs?activeOnly=false');
  container.innerHTML = items.map(d => `
    <div class="card" style="display:flex;justify-content:space-between;align-items:center;">
      <div>
        <div style="font-weight:700;">${escapeHtml(d.name)}</div>
        <div class="muted" style="font-size:12.5px;">${escapeHtml(d.form || '')} ${escapeHtml(d.strength || '')}</div>
      </div>
      <div style="display:flex;gap:8px;align-items:center;">
        <span class="stamp stamp-${d.isActive ? 'Completed' : 'Cancelled'}">${d.isActive ? 'Active' : 'Inactive'}</span>
        <button class="btn btn-ghost" onclick="toggleDrug(${d.id}, ${!d.isActive})">${d.isActive ? 'Deactivate' : 'Activate'}</button>
      </div>
    </div>`).join('') || '<div class="card muted">No drugs yet.</div>';
}

async function toggleDrug(id, isActive) {
  await apiRequest(`/drugs/${id}/toggle`, { method: 'PUT', body: { isActive } });
  loadFormulary();
}
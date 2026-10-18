Session.requireRole("LabTechnician");
document.getElementById("welcomeMsg").textContent = `${Session.fullName()} · lab technician`;

document.addEventListener('DOMContentLoaded', function () {
  document.querySelectorAll('.tab').forEach(function (tab) {
    tab.addEventListener('click', function (e) {
      e.preventDefault();
      document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
      document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
      tab.classList.add('active');
      document.getElementById('panel-' + tab.dataset.tab).classList.add('active');
      loadOrders();
    });
  });

  loadOrders();

  setInterval(function () {
    if (document.activeElement && ['INPUT', 'TEXTAREA', 'SELECT'].includes(document.activeElement.tagName)) return;
    if (document.querySelector('.result-form:not(:empty)')) return;
    loadOrders();
  }, 30000);
});

async function loadOrders() {
  const mode = document.querySelector('.tab.active').dataset.tab;
  const containerId = mode === 'ordered' ? 'orderedList' : 'completedList';
  const container = document.getElementById(containerId);
  container.innerHTML = '<div class="card muted">Loading…</div>';

  const all = await apiRequest('/laborders');
  const list = all.filter(o => mode === 'ordered' ? o.status === 'Ordered' : o.status === 'Completed');

  if (!list.length) { container.innerHTML = `<div class="card muted">No ${mode === 'ordered' ? 'tests waiting' : 'completed results yet'}.</div>`; return; }

  container.innerHTML = list.map(o => `
    <div class="card" id="order-${o.id}">
      <div style="display:flex;justify-content:space-between;align-items:flex-start;gap:12px;flex-wrap:wrap;">
        <div>
          <div style="font-family:var(--font-display);font-weight:700;font-size:16px;">${escapeHtml(o.testName)}</div>
          <div class="muted" style="font-size:12.5px;margin-top:4px;">For ${escapeHtml(o.patient)}${o.patientPhone ? ' · ' + escapeHtml(o.patientPhone) : ''} · ordered by ${escapeHtml(o.doctor)}</div>
          ${o.notes ? `<div style="font-size:13px;margin-top:8px;">${escapeHtml(o.notes)}</div>` : ''}
          ${o.status === 'Completed' ? `<div style="font-size:13px;margin-top:8px;white-space:pre-wrap;"><b>Result:</b> ${escapeHtml(o.resultText)}</div>
            <div class="fee-tag" style="margin-top:6px;">Completed by ${escapeHtml(o.completedByName || '')} ${o.completedAt ? '· ' + fmtDateTime(o.completedAt) : ''}</div>` : ''}
        </div>
        <div style="display:flex;flex-direction:column;align-items:flex-end;gap:8px;">
          <span class="stamp stamp-${o.status}">${o.status}</span>
          ${o.status === 'Ordered' ? `<button class="btn" onclick="showResultForm(${o.id})">Enter result</button>` : ''}
        </div>
      </div>
      <div class="result-form" id="result-form-${o.id}"></div>
    </div>
  `).join('');
}

function showResultForm(id) {
  document.getElementById('result-form-' + id).innerHTML = `
    <div style="margin-top:14px;border-top:1px solid var(--line);padding-top:14px;">
      <label class="field">
        <span class="field-label">Result</span>
        <textarea id="result-text-${id}" style="min-height:80px;" placeholder="Enter the test result / findings"></textarea>
      </label>
      <div style="display:flex;gap:8px;">
        <button class="btn" onclick="submitResult(${id})">Save result</button>
        <button class="btn btn-ghost" onclick="document.getElementById('result-form-${id}').innerHTML=''">Cancel</button>
      </div>
    </div>
  `;
}

async function submitResult(id) {
  const resultText = document.getElementById('result-text-' + id).value.trim();
  if (!resultText) { alert('Enter a result before saving.'); return; }
  try {
    await apiRequest(`/laborders/${id}/complete`, { method: 'POST', body: { resultText } });
    loadOrders();
  } catch (err) { alert(err.message || 'Could not save result'); }
}

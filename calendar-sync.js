// Phone reminder sync: shows the user's private calendar-subscription link and
// how to add it to iPhone / Android / Google Calendar. Works fully offline.
const CAL_ICONS = {
  bell: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/></svg>',
  phone: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="5" y="2" width="14" height="20" rx="2"/><path d="M12 18h.01"/></svg>',
  copy: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>',
  refresh: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 12a9 9 0 0 1 15-6.7L21 8"/><path d="M21 3v5h-5"/><path d="M21 12a9 9 0 0 1-15 6.7L3 16"/><path d="M3 21v-5h5"/></svg>',
  download: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><path d="m7 10 5 5 5-5"/><path d="M12 15V3"/></svg>'
};

async function renderCalendarSync(containerId) {
  const box = document.getElementById(containerId);
  if (!box) return;
  box.innerHTML = '<div class="muted">Loading…</div>';
  try {
    const info = await apiRequest('/calendar/link');
    drawCalendarSync(box, info);
  } catch (err) {
    box.innerHTML = `<div class="error-text">${escapeHtml(err.message)}</div>`;
  }
}

function drawCalendarSync(box, info) {
  box.innerHTML = `
    <p class="muted" style="margin-top:0;font-size:13.5px;">
      Add this private link to your phone's calendar once. Your upcoming appointments then appear on your phone
      with alerts <b>1 day before</b> and <b>1 hour before</b>, and they update by themselves when an appointment is
      rescheduled or cancelled.
    </p>
    <label class="field"><span class="field-label">Your private calendar link</span>
      <div class="sync-link">
        <input type="text" id="calLinkInput" readonly value="${escapeHtml(info.url)}">
        <button type="button" class="btn" id="calCopyBtn">${CAL_ICONS.copy}Copy</button>
      </div>
    </label>
    <div class="sync-actions">
      <a class="btn" href="${escapeHtml(info.webcalUrl)}">${CAL_ICONS.phone}Open in phone calendar</a>
      <button type="button" class="btn btn-ghost" id="calDownloadBtn">${CAL_ICONS.download}Download .ics (one-time)</button>
      <button type="button" class="btn btn-ghost" id="calRegenBtn">${CAL_ICONS.refresh}New link</button>
    </div>
    <div class="sync-steps">
      <details open><summary>iPhone / iPad</summary>
        <ol><li>Tap <b>Open in phone calendar</b> on your phone (or paste the link into Safari).</li>
        <li>Tap <b>Subscribe</b>, then <b>Add</b>. Done - open the Calendar app to see your appointments.</li></ol>
        <p class="muted">Manual way: Settings → Calendar → Accounts → Add Account → Other → Add Subscribed Calendar → paste the link.</p></details>
      <details><summary>Android / Google Calendar</summary>
        <ol><li>On a computer, open <b>calendar.google.com</b> → <b>Other calendars (+)</b> → <b>From URL</b>.</li>
        <li>Paste the link (use the https one above) and click <b>Add calendar</b>.</li>
        <li>It now shows in the Google Calendar app on your Android phone. Turn on notifications for it under Settings.</li></ol>
        <p class="muted">Google refreshes subscribed calendars every several hours, so changes can take a little while to appear.</p></details>
      <details><summary>Outlook / other apps</summary>
        <p class="muted" style="margin:6px 0 0;">Look for "Subscribe to calendar" / "Add calendar from internet" and paste the link.</p></details>
    </div>
    <p class="muted" style="font-size:12px;margin-bottom:0;">Keep this link private - anyone who has it can see your appointment times. Press "New link" to cut off access.</p>`;

  document.getElementById('calCopyBtn').addEventListener('click', async () => {
    const input = document.getElementById('calLinkInput');
    try { await navigator.clipboard.writeText(input.value); }
    catch (e) { input.select(); document.execCommand('copy'); }
    const btn = document.getElementById('calCopyBtn');
    btn.lastChild.textContent = 'Copied!';
    setTimeout(() => { btn.lastChild.textContent = 'Copy'; }, 1800);
  });
  document.getElementById('calDownloadBtn').addEventListener('click', () => downloadFile('/calendar/mine.ics', 'my-appointments.ics'));
  document.getElementById('calRegenBtn').addEventListener('click', async () => {
    if (!confirm('Create a new link? Phones using the old link will stop updating until you add the new one.')) return;
    try { drawCalendarSync(box, await apiRequest('/calendar/regenerate', { method: 'POST' })); }
    catch (err) { alert(err.message); }
  });
}
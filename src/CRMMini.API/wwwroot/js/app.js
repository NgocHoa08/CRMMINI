(() => {
  'use strict';
  const API = '/api/v1';
  const $ = (s, r = document) => r.querySelector(s);
  const esc = v => String(v ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const money = v => new Intl.NumberFormat('vi-VN').format(v ?? 0) + ' ₫';
  const dt = v => v ? new Date(v).toLocaleString('vi-VN') : '';
  const toLocalInput = v => { if (!v) return ''; const d = new Date(v); d.setMinutes(d.getMinutes() - d.getTimezoneOffset()); return d.toISOString().slice(0, 16); };

  // ---------- auth / http ----------
  const store = {
    get auth() { try { return JSON.parse(localStorage.getItem('crm.auth')); } catch { return null; } },
    set auth(v) { v ? localStorage.setItem('crm.auth', JSON.stringify(v)) : localStorage.removeItem('crm.auth'); }
  };
  let refreshing = null;
  async function refresh() {
    const a = store.auth; if (!a) return false;
    refreshing ??= fetch(API + '/auth/refresh', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ refreshToken: a.refreshToken }) })
      .then(async r => { if (!r.ok) return false; store.auth = await r.json(); return true; }).catch(() => false).finally(() => refreshing = null);
    return refreshing;
  }
  async function api(path, opt = {}, retry = true) {
    const a = store.auth;
    const res = await fetch(API + path, {
      method: opt.method || 'GET',
      headers: { 'Content-Type': 'application/json', ...(a ? { Authorization: 'Bearer ' + a.accessToken } : {}) },
      body: opt.body ? JSON.stringify(opt.body) : undefined
    });
    if (res.status === 401 && retry && a && await refresh()) return api(path, opt, false);
    if (res.status === 401 && a) { logout(); throw new Error('Phiên đăng nhập đã hết hạn.'); }
    if (!res.ok) {
      let msg = 'Lỗi ' + res.status;
      try {
        const p = await res.json();
        msg = p.detail || p.title || msg;
        if (p.errors) msg += ' — ' + Object.values(p.errors).flat().join('; ');
      } catch { /* ignore */ }
      throw new Error(msg);
    }
    return res.status === 204 ? null : res.json();
  }
  const qs = o => { const p = new URLSearchParams(); Object.entries(o).forEach(([k, v]) => v !== '' && v != null && p.set(k, v)); return p.toString(); };

  function toast(msg, err) {
    const t = $('#toast'); t.textContent = msg; t.className = 'toast' + (err ? ' err' : '');
    clearTimeout(toast.h); toast.h = setTimeout(() => t.classList.add('hidden'), 4200);
  }
  const wrap = fn => async (...a) => { try { await fn(...a); } catch (e) { toast(e.message, true); } };

  // ---------- enums ----------
  const E = {
    custStatus: ['Active', 'Inactive'],
    leadSource: ['Website', 'Referral', 'SocialMedia', 'ColdCall', 'Event', 'Partner', 'Other'],
    leadStatus: ['New', 'Contacted', 'Qualified', 'Converted', 'Disqualified'],
    stage: ['Qualification', 'NeedsAnalysis', 'Proposal', 'Negotiation', 'ClosedWon', 'ClosedLost'],
    actType: ['Call', 'Email', 'Meeting', 'Task', 'Reminder'],
    actStatus: ['Pending', 'Completed', 'Cancelled']
  };
  const badgeCls = v => ({ Active: 'ok', Qualified: 'ok', Converted: 'ok', ClosedWon: 'ok', Completed: 'ok', Inactive: 'bad', Disqualified: 'bad', ClosedLost: 'bad', Cancelled: 'bad', Pending: 'warn', Contacted: 'warn', Negotiation: 'warn' }[v] || '');
  const badge = v => `<span class="badge ${badgeCls(v)}">${esc(v)}</span>`;

  // ---------- modal form ----------
  function openForm({ title, fields, values = {}, submitText = 'Lưu', onSubmit }) {
    $('#modal-title').textContent = title;
    const form = $('#modal-form');
    form.innerHTML = fields.map(f => {
      const v = values[f.key] ?? '';
      const req = f.required ? 'required' : '';
      let input;
      if (f.type === 'select') input = `<select name="${f.key}" ${req}>${f.optional ? '<option value="">— Không chọn —</option>' : ''}${(f.options || []).map(o => { const [val, lbl] = Array.isArray(o) ? o : [o, o]; return `<option value="${esc(val)}" ${String(val) === String(v) ? 'selected' : ''}>${esc(lbl)}</option>`; }).join('')}</select>`;
      else if (f.type === 'textarea') input = `<textarea name="${f.key}" ${req}>${esc(v)}</textarea>`;
      else if (f.type === 'checkbox') input = `<input type="checkbox" name="${f.key}" ${v ? 'checked' : ''} />`;
      else input = `<input name="${f.key}" type="${f.type || 'text'}" value="${esc(f.type === 'datetime-local' ? toLocalInput(v) : v)}" ${req} ${f.type === 'number' ? 'step="any" min="0"' : ''} />`;
      return `<label>${esc(f.label)}${f.required ? ' *' : ''}${input}</label>`;
    }).join('') + `<div class="error" id="form-error"></div><div class="form-actions"><button type="button" class="btn" id="form-cancel">Hủy</button><button class="btn primary" type="submit">${esc(submitText)}</button></div>`;
    $('#modal').classList.remove('hidden');
    $('#form-cancel').onclick = closeModal;
    form.onsubmit = async ev => {
      ev.preventDefault();
      const body = {};
      for (const f of fields) {
        const el = form.elements[f.key]; let val;
        if (f.type === 'checkbox') val = el.checked;
        else { val = el.value.trim(); if (val === '') val = null; else if (f.type === 'number') val = Number(val); else if (f.type === 'datetime-local') val = new Date(val).toISOString(); }
        body[f.key] = val;
      }
      try { await onSubmit(body); closeModal(); } catch (e) { $('#form-error').textContent = e.message; }
    };
    const first = form.querySelector('input,select,textarea'); first && first.focus();
  }
  const closeModal = () => $('#modal').classList.add('hidden');
  $('#modal').addEventListener('mousedown', e => { if (e.target.id === 'modal') closeModal(); });

  async function lookup(path, labelKey) {
    const r = await api(`${path}?pageSize=100&sortBy=createdAt&sortDir=desc`);
    return r.items.map(i => [i.id, i[labelKey] || i.title || i.name]);
  }

  // ---------- generic resource page ----------
  const R = {
    customers: {
      title: 'Khách hàng', path: '/customers', search: true,
      cols: [['fullName', 'Họ tên'], ['email', 'Email'], ['phone', 'SĐT'], ['companyName', 'Doanh nghiệp'], ['status', 'Trạng thái', badge], ['ownerName', 'Phụ trách']],
      fields: async () => [
        { key: 'fullName', label: 'Họ tên', required: true }, { key: 'email', label: 'Email', type: 'email' }, { key: 'phone', label: 'Số điện thoại' },
        { key: 'jobTitle', label: 'Chức danh' }, { key: 'address', label: 'Địa chỉ' },
        { key: 'status', label: 'Trạng thái', type: 'select', options: E.custStatus, required: true },
        { key: 'companyId', label: 'Doanh nghiệp', type: 'select', optional: true, options: await lookup('/companies', 'name') },
        { key: 'notes', label: 'Ghi chú', type: 'textarea' }],
      defaults: { status: 'Active' }
    },
    companies: {
      title: 'Doanh nghiệp', path: '/companies', search: true,
      cols: [['name', 'Tên'], ['taxCode', 'MST'], ['industry', 'Ngành'], ['phone', 'SĐT'], ['customerCount', 'Số KH'], ['ownerName', 'Phụ trách']],
      fields: async () => [
        { key: 'name', label: 'Tên doanh nghiệp', required: true }, { key: 'taxCode', label: 'Mã số thuế' }, { key: 'industry', label: 'Ngành nghề' },
        { key: 'website', label: 'Website' }, { key: 'phone', label: 'Số điện thoại' }, { key: 'email', label: 'Email', type: 'email' }, { key: 'address', label: 'Địa chỉ' }]
    },
    leads: {
      title: 'Lead', path: '/leads', search: true,
      cols: [['fullName', 'Họ tên'], ['email', 'Email'], ['phone', 'SĐT'], ['companyName', 'Công ty'], ['source', 'Nguồn'], ['status', 'Trạng thái', badge], ['estimatedValue', 'Giá trị dự kiến', money]],
      fields: async mode => [
        { key: 'fullName', label: 'Họ tên', required: true }, { key: 'email', label: 'Email', type: 'email' }, { key: 'phone', label: 'Số điện thoại' },
        { key: 'companyName', label: 'Công ty' }, { key: 'source', label: 'Nguồn', type: 'select', options: E.leadSource, required: true },
        ...(mode === 'edit' ? [{ key: 'status', label: 'Trạng thái', type: 'select', options: E.leadStatus.filter(s => s !== 'Converted'), required: true }] : []),
        { key: 'estimatedValue', label: 'Giá trị dự kiến (₫)', type: 'number' }, { key: 'notes', label: 'Ghi chú', type: 'textarea' }],
      defaults: { source: 'Website' },
      extra: (row, reload) => row.status === 'Qualified' ? [['Chuyển đổi', () => convertLead(row, reload)]] : [],
      canEdit: row => row.status !== 'Converted'
    },
    opportunities: {
      title: 'Cơ hội kinh doanh', path: '/opportunities', search: true,
      cols: [['title', 'Tiêu đề'], ['customerName', 'Khách hàng'], ['value', 'Giá trị', money], ['stage', 'Giai đoạn', badge], ['probability', 'Xác suất', v => v + '%'], ['expectedCloseDate', 'Dự kiến chốt', dt], ['ownerName', 'Phụ trách']],
      fields: async mode => [
        { key: 'title', label: 'Tiêu đề', required: true },
        { key: 'customerId', label: 'Khách hàng', type: 'select', required: true, options: await lookup('/customers', 'fullName') },
        { key: 'companyId', label: 'Doanh nghiệp', type: 'select', optional: true, options: await lookup('/companies', 'name') },
        { key: 'value', label: 'Giá trị (₫)', type: 'number', required: true },
        ...(mode === 'create' ? [{ key: 'stage', label: 'Giai đoạn', type: 'select', options: E.stage.slice(0, 4), required: true }] : []),
        { key: 'expectedCloseDate', label: 'Ngày dự kiến chốt', type: 'datetime-local', required: true },
        { key: 'notes', label: 'Ghi chú', type: 'textarea' }],
      defaults: { stage: 'Qualification' },
      extra: (row, reload) => [['Đổi giai đoạn', () => changeStage(row, reload)]],
      canEdit: row => !['ClosedWon', 'ClosedLost'].includes(row.stage)
    },
    activities: {
      title: 'Hoạt động', path: '/activities', search: true,
      cols: [['type', 'Loại', badge], ['subject', 'Nội dung'], ['status', 'Trạng thái', badge], ['scheduledAt', 'Thời gian', dt], ['customerName', 'Khách hàng'], ['opportunityTitle', 'Cơ hội'], ['ownerName', 'Phụ trách']],
      fields: async mode => [
        { key: 'type', label: 'Loại', type: 'select', options: E.actType, required: true },
        { key: 'subject', label: 'Nội dung', required: true }, { key: 'description', label: 'Mô tả', type: 'textarea' },
        ...(mode === 'edit' ? [{ key: 'status', label: 'Trạng thái', type: 'select', options: E.actStatus, required: true }] : []),
        { key: 'scheduledAt', label: 'Thời gian thực hiện', type: 'datetime-local', required: true },
        { key: 'reminderAt', label: 'Nhắc lịch lúc', type: 'datetime-local' },
        ...(mode === 'create' ? [
          { key: 'customerId', label: 'Khách hàng', type: 'select', optional: true, options: await lookup('/customers', 'fullName') },
          { key: 'leadId', label: 'Lead', type: 'select', optional: true, options: await lookup('/leads', 'fullName') },
          { key: 'opportunityId', label: 'Cơ hội', type: 'select', optional: true, options: await lookup('/opportunities', 'title') }] : [])],
      defaults: { type: 'Call' },
      extra: (row, reload) => row.status === 'Pending' ? [['Hoàn thành', async () => { await api(`/activities/${row.id}`, { method: 'PUT', body: { type: row.type, subject: row.subject, description: row.description, status: 'Completed', scheduledAt: row.scheduledAt, reminderAt: row.reminderAt, completedAt: new Date().toISOString() } }); toast('Đã đánh dấu hoàn thành'); reload(); }]] : []
    }
  };

  async function convertLead(row, reload) {
    openForm({
      title: `Chuyển đổi Lead: ${row.fullName}`, submitText: 'Chuyển đổi',
      fields: [
        { key: 'createCompanyIfMissing', label: 'Tạo doanh nghiệp nếu chưa có', type: 'checkbox' },
        { key: 'createOpportunity', label: 'Tạo cơ hội kinh doanh', type: 'checkbox' },
        { key: 'opportunityTitle', label: 'Tiêu đề cơ hội (nếu tạo)' },
        { key: 'opportunityValue', label: 'Giá trị cơ hội (₫)', type: 'number' }],
      values: { createCompanyIfMissing: true, createOpportunity: true, opportunityTitle: `Cơ hội - ${row.fullName}`, opportunityValue: row.estimatedValue },
      onSubmit: async b => { await api(`/leads/${row.id}/convert`, { method: 'POST', body: b }); toast('Đã chuyển đổi Lead thành khách hàng'); reload(); }
    });
  }
  async function changeStage(row, reload) {
    openForm({
      title: `Đổi giai đoạn: ${row.title}`,
      fields: [
        { key: 'targetStage', label: 'Giai đoạn mới', type: 'select', options: E.stage, required: true },
        { key: 'lossReason', label: 'Lý do thua (bắt buộc nếu ClosedLost)' },
        { key: 'notes', label: 'Ghi chú', type: 'textarea' }],
      values: { targetStage: row.stage },
      onSubmit: async b => { await api(`/opportunities/${row.id}/stage`, { method: 'POST', body: b }); toast('Đã cập nhật giai đoạn'); reload(); }
    });
  }

  async function resourcePage(key) {
    const cfg = R[key]; const st = { page: 1, search: '' };
    $('#page').innerHTML = `<div class="toolbar"><input id="q" placeholder="Tìm kiếm..." /><button class="btn" id="qbtn">Tìm</button></div><div id="tbl"></div><div class="pager" id="pager"></div>`;
    $('#page-actions').innerHTML = `<button class="btn primary" id="add">+ Thêm mới</button>`;
    const load = wrap(async () => {
      const r = await api(`${cfg.path}?${qs({ page: st.page, pageSize: 10, search: st.search, sortBy: 'createdAt', sortDir: 'desc' })}`);
      $('#tbl').innerHTML = r.items.length ? `<table><thead><tr>${cfg.cols.map(c => `<th>${c[1]}</th>`).join('')}<th></th></tr></thead><tbody>${r.items.map((row, i) =>
        `<tr>${cfg.cols.map(c => `<td>${c[2] ? c[2](row[c[0]]) : esc(row[c[0]])}</td>`).join('')}<td class="actions" data-i="${i}"></td></tr>`).join('')}</tbody></table>` : '<div class="empty card">Chưa có dữ liệu.</div>';
      $('#tbl').querySelectorAll('td.actions').forEach(td => {
        const row = r.items[td.dataset.i];
        const btns = [...(cfg.extra ? cfg.extra(row, load) : [])];
        if (!cfg.canEdit || cfg.canEdit(row)) btns.push(['Sửa', () => editRow(row)]);
        btns.push(['Xóa', () => delRow(row), 'danger']);
        btns.forEach(([label, fn, cls]) => { const b = document.createElement('button'); b.className = 'btn sm ' + (cls || ''); b.textContent = label; b.onclick = wrap(fn); td.appendChild(b); });
      });
      $('#pager').innerHTML = `<span>Tổng ${r.totalCount} bản ghi</span><button class="btn sm" id="prev" ${r.hasPreviousPage ? '' : 'disabled'}>‹ Trước</button><span>Trang ${r.page}/${Math.max(r.totalPages, 1)}</span><button class="btn sm" id="next" ${r.hasNextPage ? '' : 'disabled'}>Sau ›</button>`;
      $('#prev').onclick = () => { st.page--; load(); }; $('#next').onclick = () => { st.page++; load(); };
    });
    const editRow = async row => openForm({
      title: 'Cập nhật ' + cfg.title.toLowerCase(), fields: await cfg.fields('edit'), values: row,
      onSubmit: async b => { await api(`${cfg.path}/${row.id}`, { method: 'PUT', body: b }); toast('Đã cập nhật'); load(); }
    });
    const delRow = async row => { if (!confirm('Xác nhận xóa bản ghi này?')) return; await api(`${cfg.path}/${row.id}`, { method: 'DELETE' }); toast('Đã xóa'); load(); };
    $('#add').onclick = wrap(async () => openForm({
      title: 'Thêm ' + cfg.title.toLowerCase(), fields: await cfg.fields('create'), values: cfg.defaults || {},
      onSubmit: async b => { await api(cfg.path, { method: 'POST', body: b }); toast('Đã tạo mới'); st.page = 1; load(); }
    }));
    const doSearch = () => { st.search = $('#q').value.trim(); st.page = 1; load(); };
    $('#qbtn').onclick = doSearch; $('#q').onkeydown = e => e.key === 'Enter' && doSearch();
    load();
  }

  // ---------- dashboard ----------
  async function dashboardPage() {
    $('#page-actions').innerHTML = '';
    $('#page').innerHTML = '<div class="empty">Đang tải...</div>';
    const tz = -new Date().getTimezoneOffset();
    const [s, p, a] = await Promise.all([api(`/dashboard/summary?timeZoneOffsetMinutes=${tz}`), api('/dashboard/pipeline'), api(`/dashboard/activities?timeZoneOffsetMinutes=${tz}`)]);
    const stat = (l, v) => `<div class="card stat"><div class="lbl">${l}</div><div class="val">${v}</div></div>`;
    const maxVal = Math.max(...p.stages.map(x => x.totalValue), 1);
    const actItem = i => `<li>${badge(i.type)} ${esc(i.subject)} <span class="muted small">— ${dt(i.scheduledAt)}</span></li>`;
    $('#page').innerHTML = `
      <div class="cards">
        ${stat('Khách hàng', s.totalCustomers)}${stat('Khách mới trong kỳ', s.newCustomersInPeriod)}${stat('Tổng Lead', s.totalLeads)}${stat('Tỷ lệ chuyển đổi', s.leadConversionRatePercent + '%')}
        ${stat('Cơ hội đang mở', s.openOpportunitiesCount)}${stat('Giá trị pipeline', money(s.openPipelineValue))}${stat('Pipeline có trọng số', money(s.weightedPipelineValue))}
        ${stat('Thắng trong kỳ', s.wonOpportunitiesInPeriod + ' · ' + money(s.wonValueInPeriod))}${stat('Thua trong kỳ', s.lostOpportunitiesInPeriod)}
        ${stat('Việc quá hạn', s.overdueActivitiesCount)}${stat('Việc hôm nay', s.dueTodayActivitiesCount)}
      </div>
      <div class="grid2">
        <div class="card"><h3>Pipeline theo giai đoạn</h3>${p.stages.map(x => `<div class="row-bar"><span>${badge(x.stage)}</span><div class="bar"><i style="width:${(x.totalValue / maxVal * 100).toFixed(1)}%"></i></div><span class="small">${x.count} · ${money(x.totalValue)}</span></div>`).join('')}</div>
        <div class="card"><h3>Lead theo trạng thái</h3>${s.leadsByStatus.map(x => `<div class="row-bar"><span>${badge(x.status)}</span><div class="bar"><i style="width:${(x.count / Math.max(s.totalLeads, 1) * 100).toFixed(1)}%"></i></div><span class="small">${x.count}</span></div>`).join('')}</div>
        <div class="card"><h3>Công việc quá hạn (${a.overdueCount})</h3>${a.overdueItems.length ? `<ul class="list">${a.overdueItems.map(actItem).join('')}</ul>` : '<span class="muted">Không có.</span>'}</div>
        <div class="card"><h3>Công việc sắp tới (${a.upcomingCount})</h3>${a.upcomingItems.length ? `<ul class="list">${a.upcomingItems.map(actItem).join('')}</ul>` : '<span class="muted">Không có.</span>'}</div>
        ${p.byOwner.length ? `<div class="card" style="grid-column:1/-1"><h3>Hiệu suất theo nhân viên</h3><table><thead><tr><th>Nhân viên</th><th>Đang mở</th><th>Giá trị mở</th><th>Đã thắng</th><th>Giá trị thắng</th></tr></thead><tbody>${p.byOwner.map(o => `<tr><td>${esc(o.ownerName)}</td><td>${o.openCount}</td><td>${money(o.openValue)}</td><td>${o.wonCount}</td><td>${money(o.wonValue)}</td></tr>`).join('')}</tbody></table></div>` : ''}
      </div>`;
  }

  // ---------- shell / routing ----------
  const PAGES = [['dashboard', 'Tổng quan', dashboardPage], ['customers', 'Khách hàng'], ['companies', 'Doanh nghiệp'], ['leads', 'Lead'], ['opportunities', 'Cơ hội'], ['activities', 'Hoạt động']];
  function route() {
    const auth = store.auth;
    $('#login-view').classList.toggle('hidden', !!auth);
    $('#app-view').classList.toggle('hidden', !auth);
    if (!auth) return;
    const u = auth.user;
    $('#me').innerHTML = `<b>${esc(u.fullName)}</b><br>${esc(u.roles.join(', '))}${u.teamName ? '<br>' + esc(u.teamName) : ''}`;
    const cur = (location.hash.slice(1) || 'dashboard');
    const page = PAGES.find(p => p[0] === cur) || PAGES[0];
    $('#nav').innerHTML = PAGES.map(p => `<a href="#${p[0]}" class="${p[0] === page[0] ? 'active' : ''}">${p[1]}</a>`).join('');
    $('#page-title').textContent = page[1];
    wrap(async () => page[2] ? page[2]() : resourcePage(page[0]))();
  }
  function logout() {
    const a = store.auth;
    if (a) fetch(API + '/auth/logout', { method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + a.accessToken }, body: JSON.stringify({ refreshToken: a.refreshToken }) }).catch(() => { });
    store.auth = null; route();
  }
  $('#logout').onclick = logout;
  $('#login-form').onsubmit = async e => {
    e.preventDefault(); $('#login-error').textContent = '';
    try {
      const r = await fetch(API + '/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userNameOrEmail: e.target.id.value, password: e.target.pw.value }) });
      if (!r.ok) { const p = await r.json().catch(() => ({})); throw new Error(p.detail || 'Đăng nhập thất bại'); }
      store.auth = await r.json(); location.hash = '#dashboard'; route();
    } catch (err) { $('#login-error').textContent = err.message; }
  };
  window.addEventListener('hashchange', route);
  route();
})();

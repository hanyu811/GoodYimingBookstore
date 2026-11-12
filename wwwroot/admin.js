const token = localStorage.getItem('authToken');
const role = localStorage.getItem('role');

if (role !== 'admin' || !token) {
  alert('您没有管理员权限，即将返回登录页。');
  window.location.replace('/');
}

document.getElementById('logoutBtn').addEventListener('click', () => {
  localStorage.clear();
  window.location.replace('/');
});

const listEl = document.getElementById('adminUserList');
const msgEl = document.getElementById('message');
const statReadersEl = document.getElementById('statReaders');
const statSellersEl = document.getElementById('statSellers');
const statAdminsEl = document.getElementById('statAdmins');
const statTotalAccountsEl = document.getElementById('statTotalAccounts');
const userChipEl = document.getElementById('userChip');
const paginationEl = document.getElementById('adminUsersPagination');

let currentUsers = [];
let currentPage = 1;
const PAGE_SIZE = 10;

function renderUserChip() {
  const displayName = localStorage.getItem('displayName') || localStorage.getItem('userName') || '管理员';
  if (userChipEl) {
    userChipEl.textContent = `${displayName} · 管理员`;
  }
}

function showMsg(text, isError = false) {
  msgEl.textContent = text;
  msgEl.style.color = isError ? '#e74c3c' : '#27ae60';
  setTimeout(() => {
    msgEl.textContent = '';
  }, 3000);
}

function resetForm() {
  document.getElementById('userId').value = '';
  document.getElementById('userName').value = '';
  document.getElementById('displayName').value = '';
  document.getElementById('role').value = 'reader';
  document.getElementById('password').value = '';
  document.getElementById('userName').disabled = false;
}

function fillForm(id) {
  const u = currentUsers.find(x => x.id === id);
  if (!u) return;

  document.getElementById('userId').value = u.id;
  document.getElementById('userName').value = u.userName;
  document.getElementById('displayName').value = u.displayName || '';
  document.getElementById('role').value = u.role;
  document.getElementById('password').value = '';
  document.getElementById('userName').disabled = true;

  window.scrollTo({ top: 0, behavior: 'smooth' });
}

function updateStats(users) {
  const readers = users.filter(u => u.role === 'reader').length;
  const sellers = users.filter(u => u.role === 'seller').length;
  const admins = users.filter(u => u.role === 'admin').length;

  statReadersEl.textContent = readers;
  statSellersEl.textContent = sellers;
  statAdminsEl.textContent = admins;
  if (statTotalAccountsEl) {
    statTotalAccountsEl.textContent = String(users.length);
  }
}

function renderUserRows(users) {
  if (!users.length) {
    return '<tr><td colspan="5" class="admin-users-empty">暂无账号</td></tr>';
  }

  return users.map(u => `
    <tr>
      <td class="admin-user-cell-id">${u.id}</td>
      <td class="admin-user-cell-name"><span class="admin-user-name-text">${u.userName}</span></td>
      <td class="admin-user-cell-display"><span class="admin-user-display-text">${u.displayName || '-'}</span></td>
      <td class="admin-user-cell-role">${u.role}</td>
      <td class="admin-user-cell-actions">
        <div class="action-btns">
          <button class="btn-edit" onclick="fillForm(${u.id})">编辑</button>
          <button class="btn-delete" onclick="deleteUser(${u.id})">删除账号</button>
        </div>
      </td>
    </tr>
  `).join('');
}

function renderUsersPagination(totalItems) {
  if (!paginationEl) return;

  const totalPages = Math.max(1, Math.ceil(totalItems / PAGE_SIZE));
  if (totalItems <= PAGE_SIZE) {
    paginationEl.innerHTML = '';
    return;
  }

  const pageButtons = [];
  const start = Math.max(1, currentPage - 2);
  const end = Math.min(totalPages, currentPage + 2);

  for (let page = start; page <= end; page += 1) {
    pageButtons.push(`<button class="page-btn ${page === currentPage ? 'active' : ''}" onclick="goToAdminUsersPage(${page})">${page}</button>`);
  }

  paginationEl.innerHTML = `
    <button class="page-btn" onclick="goToAdminUsersPage(${currentPage - 1})" ${currentPage === 1 ? 'disabled' : ''}>上一页</button>
    ${pageButtons.join('')}
    <button class="page-btn" onclick="goToAdminUsersPage(${currentPage + 1})" ${currentPage === totalPages ? 'disabled' : ''}>下一页</button>
    <div class="page-meta">第 ${currentPage} / ${totalPages} 页，共 ${totalItems} 个账号</div>
  `;
}

function renderUsersPage() {
  const totalPages = Math.max(1, Math.ceil(currentUsers.length / PAGE_SIZE));
  currentPage = Math.min(Math.max(1, currentPage), totalPages);

  const start = (currentPage - 1) * PAGE_SIZE;
  const pageItems = currentUsers.slice(start, start + PAGE_SIZE);

  listEl.innerHTML = renderUserRows(pageItems);
  renderUsersPagination(currentUsers.length);
}

function goToAdminUsersPage(page) {
  const totalPages = Math.max(1, Math.ceil(currentUsers.length / PAGE_SIZE));
  if (page < 1 || page > totalPages) return;

  currentPage = page;
  renderUsersPage();
  window.scrollTo({ top: 0, behavior: 'smooth' });
}

async function loadUsers(resetPage = true) {
  try {
    const resp = await fetch('/api/admin/users', {
      headers: { 'X-Auth-Token': token }
    });

    if (!resp.ok) throw new Error('Auth failed');

    currentUsers = await resp.json();

    if (resetPage) {
      currentPage = 1;
    }

    renderUsersPage();

    updateStats(currentUsers);
  } catch (err) {
    alert('会话过期或非管理员，请重新登录');
    localStorage.clear();
    window.location.replace('/');
  }
}

async function deleteUser(id) {
  if (!confirm('确定删除该账号吗？')) return;

  const resp = await fetch(`/api/admin/users/${id}`, {
    method: 'DELETE',
    headers: { 'X-Auth-Token': token }
  });

  if (resp.ok) {
    const result = await resp.json();
    if (!result?.verifyDeleted) {
      showMsg('❌ 删除校验失败，请刷新后重试', true);
      return;
    }
    showMsg('✅ 删除成功');
    loadUsers(false);
  } else {
    let text = '❌ 删除失败';
    try {
      const err = await resp.json();
      if (err?.message) text = `❌ ${err.message}`;
    } catch {
      // ignore
    }
    showMsg(text, true);
  }
}

document.getElementById('saveBtn').addEventListener('click', async () => {
  const id = document.getElementById('userId').value;
  const userName = document.getElementById('userName').value.trim();
  const displayName = document.getElementById('displayName').value.trim();
  const selectedRole = document.getElementById('role').value;
  const password = document.getElementById('password').value;

  const isEdit = !!id;
  let url = '/api/admin/users';
  let method = 'POST';
  let payload;

  if (isEdit) {
    url = `/api/admin/users/${id}`;
    method = 'PUT';
    payload = {
      role: selectedRole,
      displayName,
      password: password || null
    };
  } else {
    if (!userName || !password) {
      showMsg('❌ 新建账号时用户名和密码必填', true);
      return;
    }

    payload = {
      userName,
      password,
      role: selectedRole,
      displayName
    };
  }

  const resp = await fetch(url, {
    method,
    headers: {
      'Content-Type': 'application/json',
      'X-Auth-Token': token
    },
    body: JSON.stringify(payload)
  });

  if (resp.ok) {
    showMsg(isEdit ? '✅ 账号更新成功' : '✅ 账号创建成功');
    resetForm();
    loadUsers();
  } else {
    let text = '❌ 保存失败';
    try {
      const err = await resp.json();
      if (err?.message) text = `❌ ${err.message}`;
    } catch {
      // ignore
    }
    showMsg(text, true);
  }
});

document.getElementById('resetBtn').addEventListener('click', resetForm);

window.goToAdminUsersPage = goToAdminUsersPage;
renderUserChip();
loadUsers();

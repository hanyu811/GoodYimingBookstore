const role = localStorage.getItem('role');
const token = localStorage.getItem('authToken');

if (role !== 'reader' || !token) {
  alert('您没有读者权限，即将返回登录页。');
  window.location.replace('/');
}

document.getElementById('logoutBtn').addEventListener('click', () => {
  localStorage.clear();
  window.location.replace('/');
});

const listEl = document.getElementById('orderList');
const msgEl = document.getElementById('orderMessage');
const userChipEl = document.getElementById('userChip');
const orderCountEl = document.getElementById('orderCount');
const orderAmountEl = document.getElementById('orderAmount');
const receivedOrderCountEl = document.getElementById('receivedOrderCount');
const paginationEl = document.getElementById('readerOrdersPagination');

let currentOrders = [];
let currentPage = 1;
const PAGE_SIZE = 10;

function renderUserChip() {
  const displayName = localStorage.getItem('displayName') || localStorage.getItem('userName') || '读者';
  if (userChipEl) {
    userChipEl.textContent = `${displayName} · 读者`;
  }
}

function normalizeStatus(status) {
  return String(status || '').trim().toLowerCase();
}

function toStatusText(status) {
  const normalized = normalizeStatus(status);
  if (normalized === 'unshipped') return '未发货';
  if (normalized === 'shipped') return '已发货';
  if (normalized === 'received') return '已收货';
  return status || '未知';
}

function getStatusClass(status) {
  const normalized = normalizeStatus(status);
  if (normalized === 'unshipped') return 'is-unshipped';
  if (normalized === 'shipped') return 'is-shipped';
  if (normalized === 'received') return 'is-received';
  return 'is-other';
}

function updateOrderStats(orders) {
  const safeOrders = Array.isArray(orders) ? orders : [];
  const totalAmount = safeOrders.reduce((sum, item) => sum + Number(item.totalAmount || 0), 0);
  const receivedCount = safeOrders.filter(item => normalizeStatus(item.status) === 'received').length;

  if (orderCountEl) orderCountEl.textContent = String(safeOrders.length);
  if (orderAmountEl) orderAmountEl.textContent = totalAmount.toFixed(2);
  if (receivedOrderCountEl) receivedOrderCountEl.textContent = String(receivedCount);
}

function showMsg(text, isError = false) {
  msgEl.textContent = text;
  msgEl.style.color = isError ? '#e74c3c' : '#27ae60';
  setTimeout(() => {
    msgEl.textContent = '';
  }, 3000);
}

function renderActions(order) {
  const normalized = normalizeStatus(order.status);

  if (normalized === 'shipped') {
    return `
      <div class="action-btns">
        <button class="btn-receive" onclick="receiveOrder(${order.id})">确认收货</button>
      </div>
    `;
  }

  if (normalized === 'unshipped') {
    return `
      <div class="action-btns">
        <button class="btn-delete" onclick="deleteOrder(${order.id})">删除记录</button>
      </div>
    `;
  }

  return '<span class="order-action-done">已完成</span>';
}

function renderOrderRows(orders) {
  if (!orders.length) {
    return '<tr><td colspan="8" class="reader-orders-empty">暂无订单记录</td></tr>';
  }

  return orders.map(o => {
    const details = (o.items || []).map(i => `${i.title} x${i.quantity}`).join('<br/>');
    const date = new Date(o.orderDate).toLocaleString();
    const statusClass = getStatusClass(o.status);
    const shippingAddress = o.shippingAddress || '-';
    const shippingPhone = o.shippingPhone || '-';

    return `
      <tr>
        <td class="reader-cell-order-id">#${o.id}</td>
        <td class="reader-cell-order-date">${date}</td>
        <td class="reader-cell-address">${shippingAddress}</td>
        <td class="reader-cell-phone">${shippingPhone}</td>
        <td class="reader-cell-items"><div class="reader-order-items">${details || '-'}</div></td>
        <td class="reader-cell-amount">¥${Number(o.totalAmount).toFixed(2)}</td>
        <td class="reader-cell-status"><span class="reader-order-status-chip ${statusClass}">${toStatusText(o.status)}</span></td>
        <td class="reader-cell-actions">${renderActions(o)}</td>
      </tr>
    `;
  }).join('');
}

function renderOrdersPagination(totalItems) {
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
    pageButtons.push(`<button class="page-btn ${page === currentPage ? 'active' : ''}" onclick="goToReaderOrdersPage(${page})">${page}</button>`);
  }

  paginationEl.innerHTML = `
    <button class="page-btn" onclick="goToReaderOrdersPage(${currentPage - 1})" ${currentPage === 1 ? 'disabled' : ''}>上一页</button>
    ${pageButtons.join('')}
    <button class="page-btn" onclick="goToReaderOrdersPage(${currentPage + 1})" ${currentPage === totalPages ? 'disabled' : ''}>下一页</button>
    <div class="page-meta">第 ${currentPage} / ${totalPages} 页，共 ${totalItems} 条订单</div>
  `;
}

function renderOrdersPage() {
  const totalPages = Math.max(1, Math.ceil(currentOrders.length / PAGE_SIZE));
  currentPage = Math.min(Math.max(1, currentPage), totalPages);

  const start = (currentPage - 1) * PAGE_SIZE;
  const pageItems = currentOrders.slice(start, start + PAGE_SIZE);

  listEl.innerHTML = renderOrderRows(pageItems);
  renderOrdersPagination(currentOrders.length);
}

function goToReaderOrdersPage(page) {
  const totalPages = Math.max(1, Math.ceil(currentOrders.length / PAGE_SIZE));
  if (page < 1 || page > totalPages) return;

  currentPage = page;
  renderOrdersPage();
  window.scrollTo({ top: 0, behavior: 'smooth' });
}

async function loadOrders(resetPage = true) {
  try {
    const resp = await fetch('/api/reader/orders', {
      headers: { 'X-Auth-Token': token }
    });

    if (!resp.ok) {
      showMsg('❌ 订单加载失败，请重新登录', true);
      listEl.innerHTML = '<tr><td colspan="8" class="reader-orders-load-error">订单加载失败</td></tr>';
      if (paginationEl) {
        paginationEl.innerHTML = '';
      }
      return;
    }

    currentOrders = await resp.json();
    updateOrderStats(currentOrders);

    if (resetPage) {
      currentPage = 1;
    }

    renderOrdersPage();
  } catch {
    showMsg('❌ 网络异常，请稍后重试', true);
    listEl.innerHTML = '<tr><td colspan="8" class="reader-orders-load-error">网络异常，订单加载失败</td></tr>';
    if (paginationEl) {
      paginationEl.innerHTML = '';
    }
  }
}

async function receiveOrder(id) {
  if (!confirm('确认已收到该订单商品吗？')) {
    return;
  }

  const resp = await fetch(`/api/reader/orders/${id}/receive`, {
    method: 'PATCH',
    headers: { 'X-Auth-Token': token }
  });

  if (resp.ok) {
    showMsg('✅ 已确认收货');
    loadOrders(false);
    return;
  }

  let text = '❌ 收货确认失败';
  try {
    const err = await resp.json();
    if (err?.message) text = `❌ ${err.message}`;
  } catch {
    // ignore
  }
  showMsg(text, true);
}

async function deleteOrder(id) {
  if (!confirm('确定删除该未发货订单记录吗？')) {
    return;
  }

  const resp = await fetch(`/api/reader/orders/${id}`, {
    method: 'DELETE',
    headers: { 'X-Auth-Token': token }
  });

  if (resp.ok) {
    showMsg('✅ 订单记录已删除');
    loadOrders(false);
    return;
  }

  let text = '❌ 删除失败';
  try {
    const err = await resp.json();
    if (err?.message) text = `❌ ${err.message}`;
  } catch {
    // ignore
  }
  showMsg(text, true);
}

window.loadOrders = loadOrders;
window.deleteOrder = deleteOrder;
window.receiveOrder = receiveOrder;
window.goToReaderOrdersPage = goToReaderOrdersPage;

renderUserChip();
loadOrders();

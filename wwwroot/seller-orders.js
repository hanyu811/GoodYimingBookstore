const token = localStorage.getItem('authToken');
const role = localStorage.getItem('role');

if (role !== 'seller' || !token) {
  alert('您没有商家权限，即将返回登录页。');
  window.location.replace('/');
}

document.getElementById('logoutBtn').addEventListener('click', () => {
  localStorage.clear();
  window.location.replace('/');
});

const listEl = document.getElementById('sellerOrderList');
const msgEl = document.getElementById('message');
const userChipEl = document.getElementById('userChip');
const sellerOrderCountEl = document.getElementById('sellerOrderCount');
const sellerOrderAmountEl = document.getElementById('sellerOrderAmount');
const sellerUnshippedOrderCountEl = document.getElementById('sellerUnshippedOrderCount');
const paginationEl = document.getElementById('sellerOrdersPagination');

let currentOrders = [];
let currentPage = 1;
const PAGE_SIZE = 10;

function renderUserChip() {
  const displayName = localStorage.getItem('displayName') || localStorage.getItem('userName') || '商家';
  if (userChipEl) {
    userChipEl.textContent = `${displayName} · 商家`;
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

function updateSellerOrderStats(orders) {
  const safeOrders = Array.isArray(orders) ? orders : [];
  const totalAmount = safeOrders.reduce((sum, item) => sum + Number(item.totalAmount || 0), 0);
  const unshippedCount = safeOrders.filter(item => normalizeStatus(item.status) === 'unshipped').length;

  if (sellerOrderCountEl) sellerOrderCountEl.textContent = String(safeOrders.length);
  if (sellerOrderAmountEl) sellerOrderAmountEl.textContent = totalAmount.toFixed(2);
  if (sellerUnshippedOrderCountEl) sellerUnshippedOrderCountEl.textContent = String(unshippedCount);
}

function renderActions(order) {
  const normalized = normalizeStatus(order.status);

  if (normalized === 'unshipped') {
    return `<button class="btn-ship" onclick="shipOrder(${order.id})">立即发货</button>`;
  }

  if (normalized === 'shipped') {
    return '<span class="order-action-muted">等待读者收货</span>';
  }

  return '<span class="order-action-done">已完成</span>';
}

function renderOrderRows(orders) {
  if (!orders.length) {
    return '<tr><td colspan="9" class="seller-orders-empty">暂无订单</td></tr>';
  }

  return orders.map(o => {
    const details = (o.items || []).map(i => `${i.title} x${i.quantity}`).join('<br/>');
    const buyer = o.buyer || o.reader || {};
    const buyerDisplayName = buyer.displayName || buyer.userName || '-';
    const buyerUserName = buyer.userName || '-';
    const date = new Date(o.orderDate).toLocaleString();
    const statusClass = getStatusClass(o.status);

    return `
      <tr>
        <td class="seller-cell-order-id">#${o.id}</td>
        <td class="seller-cell-order-date">${date}</td>
        <td class="seller-cell-buyer">
          <div class="seller-buyer-name">${buyerDisplayName}</div>
          <div class="seller-buyer-account">账号：${buyerUserName}</div>
        </td>
        <td class="seller-cell-address">${o.shippingAddress || '-'}</td>
        <td class="seller-cell-phone">${o.shippingPhone || '-'}</td>
        <td class="seller-cell-items"><div class="seller-order-items">${details || '-'}</div></td>
        <td class="seller-cell-amount">¥${Number(o.totalAmount).toFixed(2)}</td>
        <td class="seller-cell-status"><span class="reader-order-status-chip ${statusClass}">${toStatusText(o.status)}</span></td>
        <td class="seller-cell-actions">${renderActions(o)}</td>
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
    pageButtons.push(`<button class="page-btn ${page === currentPage ? 'active' : ''}" onclick="goToSellerOrdersPage(${page})">${page}</button>`);
  }

  paginationEl.innerHTML = `
    <button class="page-btn" onclick="goToSellerOrdersPage(${currentPage - 1})" ${currentPage === 1 ? 'disabled' : ''}>上一页</button>
    ${pageButtons.join('')}
    <button class="page-btn" onclick="goToSellerOrdersPage(${currentPage + 1})" ${currentPage === totalPages ? 'disabled' : ''}>下一页</button>
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

function goToSellerOrdersPage(page) {
  const totalPages = Math.max(1, Math.ceil(currentOrders.length / PAGE_SIZE));
  if (page < 1 || page > totalPages) return;

  currentPage = page;
  renderOrdersPage();
  window.scrollTo({ top: 0, behavior: 'smooth' });
}

function showMsg(text, isError = false) {
  msgEl.textContent = text;
  msgEl.style.color = isError ? '#e74c3c' : '#27ae60';
  setTimeout(() => {
    msgEl.textContent = '';
  }, 3000);
}

async function loadOrders(resetPage = true) {
  try {
    const resp = await fetch('/api/seller/orders', {
      headers: { 'X-Auth-Token': token }
    });

    if (!resp.ok) throw new Error('load orders failed');

    currentOrders = await resp.json();
    if (resetPage) {
      currentPage = 1;
    }

    updateSellerOrderStats(currentOrders);
    renderOrdersPage();
  } catch {
    listEl.innerHTML = '<tr><td colspan="9" class="seller-orders-load-error">订单加载失败</td></tr>';
    if (paginationEl) {
      paginationEl.innerHTML = '';
    }
    showMsg('❌ 订单加载失败，请稍后重试', true);
  }
}

async function shipOrder(id) {
  if (!confirm('确认将该订单标记为已发货吗？')) {
    return;
  }

  const resp = await fetch(`/api/seller/orders/${id}/ship`, {
    method: 'PATCH',
    headers: { 'X-Auth-Token': token }
  });

  if (resp.ok) {
    showMsg('✅ 发货成功');
    loadOrders(false);
    return;
  }

  let text = '❌ 发货失败';
  try {
    const err = await resp.json();
    if (err?.message) text = `❌ ${err.message}`;
  } catch {
    // ignore
  }
  showMsg(text, true);
}

window.loadOrders = loadOrders;
window.shipOrder = shipOrder;
window.goToSellerOrdersPage = goToSellerOrdersPage;

renderUserChip();
loadOrders();

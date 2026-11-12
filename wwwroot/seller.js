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

const listEl = document.getElementById('sellerBookList');
const msgEl = document.getElementById('message');
const statTotalBooksEl = document.getElementById('statTotalBooks');
const statTotalStockEl = document.getElementById('statTotalStock');
const statLowStockEl = document.getElementById('statLowStock');
const userChipEl = document.getElementById('userChip');
const paginationEl = document.getElementById('sellerBooksPagination');

let currentBooks = [];
let currentPage = 1;
const PAGE_SIZE = 10;

function renderUserChip() {
  const displayName = localStorage.getItem('displayName') || localStorage.getItem('userName') || '商家';
  if (userChipEl) {
    userChipEl.textContent = `${displayName} · 商家`;
  }
}

function showMsg(text, isError = false) {
  msgEl.textContent = text;
  msgEl.style.color = isError ? '#e74c3c' : '#27ae60';
  setTimeout(() => msgEl.textContent = '', 3000);
}

function parseDecimal(input) {
  const normalized = String(input ?? '').trim().replace(',', '.');
  const value = Number.parseFloat(normalized);
  if (Number.isNaN(value)) return 0;
  return Math.round(value * 100) / 100;
}

function resetForm() {
  document.getElementById('bookId').value = '';
  document.getElementById('title').value = '';
  document.getElementById('author').value = '';
  document.getElementById('category').value = '';
  document.getElementById('price').value = '';
  document.getElementById('stock').value = '';
  document.getElementById('imageUrl').value = '';
  document.getElementById('description').value = '';
}

function fillForm(id) {
  const b = currentBooks.find(x => x.id === id);
  if (!b) return;
  document.getElementById('bookId').value = b.id;
  document.getElementById('title').value = b.title;
  document.getElementById('author').value = b.author;
  document.getElementById('category').value = b.category || '';
  document.getElementById('price').value = Number(b.price).toFixed(2);
  document.getElementById('stock').value = b.stock;
  document.getElementById('imageUrl').value = b.imageUrl;
  document.getElementById('description').value = b.description;
  window.scrollTo({ top: 0, behavior: 'smooth' });
}

async function loadStats() {
  const resp = await fetch('/api/seller/books/stats', {
    headers: { 'X-Auth-Token': token }
  });

  if (!resp.ok) {
    return;
  }

  const stats = await resp.json();
  statTotalBooksEl.textContent = stats.totalBooks;
  statTotalStockEl.textContent = stats.totalStock;

  if (statLowStockEl) {
    const lowStockCount = currentBooks.filter(b => Number(b.stock) <= 5).length;
    statLowStockEl.textContent = String(lowStockCount);
  }
}

function renderBookRows(books) {
  if (!books.length) {
    return '<tr><td colspan="7" class="seller-books-empty">暂无图书</td></tr>';
  }

  return books.map(b => `
    <tr>
      <td class="seller-book-cell-cover"><img src="${b.imageUrl || 'https://via.placeholder.com/50x65'}" alt="cover" /></td>
      <td class="seller-book-cell-title"><span class="seller-book-title-text">${b.title}</span></td>
      <td class="seller-book-cell-author">${b.author}</td>
      <td class="seller-book-cell-category">${b.category || '未分类'}</td>
      <td class="seller-book-cell-price">¥${Number(b.price).toFixed(2)}</td>
      <td class="seller-book-cell-stock">${b.stock}</td>
      <td class="seller-book-cell-actions">
        <div class="action-btns">
          <button class="btn-edit" onclick="fillForm(${b.id})">编辑</button>
          <button class="btn-delete" onclick="deleteBook(${b.id})">删除</button>
        </div>
      </td>
    </tr>
  `).join('');
}

function renderBooksPagination(totalItems) {
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
    pageButtons.push(`<button class="page-btn ${page === currentPage ? 'active' : ''}" onclick="goToSellerBooksPage(${page})">${page}</button>`);
  }

  paginationEl.innerHTML = `
    <button class="page-btn" onclick="goToSellerBooksPage(${currentPage - 1})" ${currentPage === 1 ? 'disabled' : ''}>上一页</button>
    ${pageButtons.join('')}
    <button class="page-btn" onclick="goToSellerBooksPage(${currentPage + 1})" ${currentPage === totalPages ? 'disabled' : ''}>下一页</button>
    <div class="page-meta">第 ${currentPage} / ${totalPages} 页，共 ${totalItems} 本图书</div>
  `;
}

function renderBooksPage() {
  const totalPages = Math.max(1, Math.ceil(currentBooks.length / PAGE_SIZE));
  currentPage = Math.min(Math.max(1, currentPage), totalPages);

  const start = (currentPage - 1) * PAGE_SIZE;
  const pageItems = currentBooks.slice(start, start + PAGE_SIZE);

  listEl.innerHTML = renderBookRows(pageItems);
  renderBooksPagination(currentBooks.length);
}

function goToSellerBooksPage(page) {
  const totalPages = Math.max(1, Math.ceil(currentBooks.length / PAGE_SIZE));
  if (page < 1 || page > totalPages) return;
  currentPage = page;
  renderBooksPage();
  window.scrollTo({ top: 0, behavior: 'smooth' });
}

async function loadBooks(resetPage = true) {
  try {
    const resp = await fetch('/api/seller/books', { headers: { 'X-Auth-Token': token } });
    if (!resp.ok) throw new Error('Auth failed');
    currentBooks = await resp.json();

    if (resetPage) {
      currentPage = 1;
    }

    renderBooksPage();

    loadStats();
  } catch (err) {
    alert('会话过期或非商家，请重新登录');
    localStorage.clear();
    window.location.replace('/');
  }
}

async function deleteBook(id) {
  const impactResp = await fetch(`/api/seller/books/${id}/delete-impact`, {
    headers: { 'X-Auth-Token': token }
  });

  if (!impactResp.ok) {
    showMsg('❌ 无法读取删除影响信息，请刷新后重试', true);
    return;
  }

  const impact = await impactResp.json();
  const title = impact?.title || `ID=${id}`;
  const cartItemCount = Number(impact?.cartItemCount || 0);
  const orderItemCount = Number(impact?.orderItemCount || 0);

  const firstConfirm = confirm(
    `即将删除图书：《${title}》\n` +
    `关联购物车项：${cartItemCount}\n` +
    `关联订单明细：${orderItemCount}\n\n` +
    '确认继续删除吗？'
  );
  if (!firstConfirm) return;

  const secondConfirm = confirm('请再次确认：该操作会从数据库彻底删除该图书及其关联明细。');
  if (!secondConfirm) return;

  const resp = await fetch(`/api/seller/books/${id}`, {
    method: 'DELETE',
    headers: { 'X-Auth-Token': token }
  });

  if (resp.ok) {
    const result = await resp.json();
    if (!result?.verifyDeleted) {
      showMsg('❌ 删除校验失败，请刷新后重试', true);
      return;
    }
    showMsg('✅ 删除成功，图书已从数据库彻底移除');
    loadBooks(false);
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

document.getElementById('saveBtn').addEventListener('click', async () => {
  const id = document.getElementById('bookId').value;
  const priceInput = document.getElementById('price').value;
  const stockInput = document.getElementById('stock').value;

  const payload = {
    title: document.getElementById('title').value.trim(),
    author: document.getElementById('author').value.trim(),
    category: document.getElementById('category').value.trim(),
    price: parseDecimal(priceInput),
    stock: Number.parseInt(stockInput || '0', 10) || 0,
    imageUrl: document.getElementById('imageUrl').value.trim(),
    description: document.getElementById('description').value.trim()
  };

  if (!payload.title || !payload.author) {
    showMsg('❌ 请至少填写书名和作者', true);
    return;
  }

  if (payload.price < 0 || payload.stock < 0) {
    showMsg('❌ 价格和库存不能为负数', true);
    return;
  }

  const isEdit = !!id;
  const url = isEdit ? `/api/seller/books/${id}` : '/api/seller/books';
  const method = isEdit ? 'PUT' : 'POST';

  const resp = await fetch(url, {
    method,
    headers: { 'Content-Type': 'application/json', 'X-Auth-Token': token },
    body: JSON.stringify(payload)
  });

  if (resp.ok) {
    showMsg(isEdit ? '✅ 更新成功，图书已在商城展示' : '✅ 保存成功，图书已在商城展示');
    resetForm();
    loadBooks();
  } else {
    showMsg('❌ 保存失败，请检查输入格式', true);
  }
});

document.getElementById('resetBtn').addEventListener('click', resetForm);

window.goToSellerBooksPage = goToSellerBooksPage;
renderUserChip();
loadBooks();

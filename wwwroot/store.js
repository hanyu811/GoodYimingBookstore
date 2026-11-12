const role = localStorage.getItem('role');
const token = localStorage.getItem('authToken');

if (!role || !token) {
  window.location.replace('/');
}

if (role === 'admin') {
  window.location.replace('/admin.html');
}

if (role === 'seller') {
  window.location.replace('/seller.html');
}

document.getElementById('logoutBtn').addEventListener('click', () => {
  localStorage.clear();
  window.location.replace('/');
});

const bookListEl = document.getElementById('bookList');
const keywordEl = document.getElementById('keyword');
const searchBtn = document.getElementById('searchBtn');
const storeMessageEl = document.getElementById('storeMessage');
const userChipEl = document.getElementById('userChip');
const paginationEl = document.getElementById('pagination');

const buyModalEl = document.getElementById('buyModal');
const modalTitleEl = document.getElementById('modalTitle');
const modalAuthorEl = document.getElementById('modalAuthor');
const modalDescEl = document.getElementById('modalDesc');
const modalPriceEl = document.getElementById('modalPrice');
const modalStockEl = document.getElementById('modalStock');
const modalQtyEl = document.getElementById('modalQty');
const modalQtyMinusBtn = document.getElementById('modalQtyMinusBtn');
const modalQtyPlusBtn = document.getElementById('modalQtyPlusBtn');
const modalMessageEl = document.getElementById('modalMessage');
const modalCancelBtn = document.getElementById('modalCancelBtn');
const modalConfirmBtn = document.getElementById('modalConfirmBtn');

let selectedBook = null;
let currentBooks = [];
let currentPage = 1;
const PAGE_SIZE = 20;
let hasInitialized = false;
let hasSuccessfulBookFetch = false;
let autoLoadTimerId = null;

function renderUserChip() {
  const displayName = localStorage.getItem('displayName') || localStorage.getItem('userName') || '读者';
  if (userChipEl) {
    userChipEl.textContent = `${displayName} · 读者`;
  }
}

function showStoreMessage(text, isError = false) {
  storeMessageEl.textContent = text;
  storeMessageEl.style.color = isError ? '#e74c3c' : '#27ae60';
  setTimeout(() => {
    storeMessageEl.textContent = '';
  }, 3000);
}

function showModalMessage(text, isError = false) {
  if (!modalMessageEl) return;
  modalMessageEl.textContent = text;
  modalMessageEl.style.color = isError ? '#e74c3c' : '#27ae60';
}

function renderBooks(books) {
  if (!books.length) {
    bookListEl.innerHTML = '<div style="grid-column: 1/-1; text-align:center; padding: 60px; color:#7f8c8d; font-size:18px;">😔 抱歉，未找到相关图书。</div>';
    return;
  }

  bookListEl.innerHTML = books.map(b => `
    <article class="book-card">
      <img class="book-cover" src="${b.imageUrl || 'https://via.placeholder.com/320x420?text=No+Cover'}" alt="${b.title}" />
      <div class="book-info">
        <h3 class="book-title">${b.title}</h3>
        <p class="book-author">✍️ ${b.author || '未知作者'}</p>
        <p class="book-desc">${b.description || '暂无简介...'}</p>
        <div class="book-footer">
          <span class="book-price">¥ ${Number(b.price).toFixed(2)}</span>
          <span style="color:#7f8c8d; font-size:13px;">库存: ${b.stock}</span>
        </div>
        <button class="nav-btn btn-block" onclick="openBuyModal(${b.id})">查看简介并购买</button>
      </div>
    </article>
  `).join('');
}

function renderPagination(totalItems) {
  const totalPages = Math.max(1, Math.ceil(totalItems / PAGE_SIZE));

  if (totalItems <= PAGE_SIZE) {
    paginationEl.innerHTML = '';
    return;
  }

  const pageButtons = [];
  const start = Math.max(1, currentPage - 2);
  const end = Math.min(totalPages, currentPage + 2);

  for (let p = start; p <= end; p += 1) {
    pageButtons.push(`
      <button class="page-btn ${p === currentPage ? 'active' : ''}" onclick="goToPage(${p})">${p}</button>
    `);
  }

  paginationEl.innerHTML = `
    <button class="page-btn" onclick="goToPage(${currentPage - 1})" ${currentPage === 1 ? 'disabled' : ''}>上一页</button>
    ${pageButtons.join('')}
    <button class="page-btn" onclick="goToPage(${currentPage + 1})" ${currentPage === totalPages ? 'disabled' : ''}>下一页</button>
    <div class="page-meta">第 ${currentPage} / ${totalPages} 页，共 ${totalItems} 本图书</div>
  `;
}

function renderCurrentPage() {
  const totalPages = Math.max(1, Math.ceil(currentBooks.length / PAGE_SIZE));
  currentPage = Math.min(Math.max(1, currentPage), totalPages);

  const startIndex = (currentPage - 1) * PAGE_SIZE;
  const pageBooks = currentBooks.slice(startIndex, startIndex + PAGE_SIZE);

  renderBooks(pageBooks);
  renderPagination(currentBooks.length);
}

function goToPage(page) {
  const totalPages = Math.max(1, Math.ceil(currentBooks.length / PAGE_SIZE));
  if (page < 1 || page > totalPages) return;
  currentPage = page;
  renderCurrentPage();
  window.scrollTo({ top: 0, behavior: 'smooth' });
}

async function fetchBooks() {
  return fetchBooksWithRetry(0);
}

async function fetchBooksWithRetry(retryCount) {
  const keyword = keywordEl.value.trim();
  const url = keyword ? `/api/books?keyword=${encodeURIComponent(keyword)}` : '/api/books';
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), 8000);

  try {
    bookListEl.innerHTML = '<div style="grid-column:1/-1;text-align:center;">加载中...</div>';
    const resp = await fetch(url, {
      cache: 'no-store',
      signal: controller.signal,
      headers: {
        'Cache-Control': 'no-cache'
      }
    });
    if (!resp.ok) throw new Error('Failed');
    const books = await resp.json();
    currentBooks = books;
    currentPage = 1;
    window.__latestBooks = books;
    renderCurrentPage();
    hasSuccessfulBookFetch = true;

    if (autoLoadTimerId) {
      clearInterval(autoLoadTimerId);
      autoLoadTimerId = null;
    }
    return;
  } catch (err) {
    if (retryCount < 2) {
      setTimeout(() => {
        fetchBooksWithRetry(retryCount + 1);
      }, 350 * (retryCount + 1));
      return;
    }

    bookListEl.innerHTML = '<div style="grid-column:1/-1;color:red;text-align:center;">加载失败，请重试。</div>';
    paginationEl.innerHTML = '';
  } finally {
    clearTimeout(timeoutId);
  }
}

function startAutoLoadWatchdog() {
  if (autoLoadTimerId) return;

  autoLoadTimerId = setInterval(() => {
    if (hasSuccessfulBookFetch) {
      clearInterval(autoLoadTimerId);
      autoLoadTimerId = null;
      return;
    }

    if (!document.hidden) {
      fetchBooks();
    }
  }, 1500);
}

async function addToCart(bookId, quantity) {
  modalConfirmBtn.disabled = true;
  const prevText = modalConfirmBtn.textContent;
  modalConfirmBtn.textContent = '加入中...';
  showModalMessage('');

  try {
    const resp = await fetch('/api/reader/cart/items', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Auth-Token': token
      },
      body: JSON.stringify({ bookId, quantity })
    });

    if (resp.ok) {
      showModalMessage('✅ 已加入购物车');
      showStoreMessage('✅ 已加入购物车，可前往“我的购物车”查看');
      setTimeout(() => {
        closeBuyModal();
      }, 400);
      return;
    }

    if (resp.status === 401 || resp.status === 403) {
      alert('登录状态已失效，请重新登录。');
      localStorage.clear();
      window.location.replace('/');
      return;
    }

    let text = '❌ 加入失败';
    try {
      const err = await resp.json();
      if (err?.message) text = `❌ ${err.message}`;
    } catch {
      // ignore
    }
    showModalMessage(text, true);
    showStoreMessage(text, true);
  } catch {
    showModalMessage('❌ 网络异常，请稍后重试', true);
    showStoreMessage('❌ 网络异常，请稍后重试', true);
  } finally {
    modalConfirmBtn.disabled = false;
    modalConfirmBtn.textContent = prevText;
  }
}

function adjustModalQty(delta) {
  if (!selectedBook) return;
  const current = Number.parseInt(modalQtyEl.value || '1', 10);
  const safeCurrent = Number.isFinite(current) ? current : 1;
  const max = Math.max(1, Number(selectedBook.stock || 1));
  const next = Math.min(max, Math.max(1, safeCurrent + delta));
  modalQtyEl.value = String(next);
}

function openBuyModal(bookId) {
  const allCards = window.__latestBooks || [];
  const target = allCards.find(b => b.id === bookId);
  if (!target) return;

  selectedBook = target;
  modalTitleEl.textContent = target.title;
  modalAuthorEl.textContent = `作者：${target.author || '未知作者'}  |  分类：${target.category || '未分类'}`;
  modalDescEl.textContent = target.description || '暂无简介';
  modalPriceEl.textContent = `¥${Number(target.price).toFixed(2)}`;
  modalStockEl.textContent = `库存：${target.stock}`;
  modalQtyEl.value = '1';
  modalQtyEl.max = String(Math.max(1, target.stock));
  showModalMessage('');

  buyModalEl.style.display = 'flex';
}

function closeBuyModal() {
  buyModalEl.style.display = 'none';
  selectedBook = null;
}

modalCancelBtn.addEventListener('click', closeBuyModal);
buyModalEl.addEventListener('click', (e) => {
  if (e.target === buyModalEl) {
    closeBuyModal();
  }
});

modalConfirmBtn.addEventListener('click', () => {
  if (!selectedBook) return;
  const quantity = Number.parseInt(modalQtyEl.value || '1', 10);
  if (!Number.isFinite(quantity) || quantity <= 0) {
    showModalMessage('❌ 购买数量必须大于 0', true);
    return;
  }

  if (quantity > Number(selectedBook.stock || 0)) {
    showModalMessage('❌ 超出库存，无法加入购物车', true);
    return;
  }

  addToCart(selectedBook.id, quantity);
});

if (modalQtyMinusBtn && modalQtyPlusBtn && modalQtyEl) {
  modalQtyMinusBtn.addEventListener('click', () => adjustModalQty(-1));
  modalQtyPlusBtn.addEventListener('click', () => adjustModalQty(1));

  modalQtyEl.addEventListener('change', () => {
    if (!selectedBook) return;
    const qty = Number.parseInt(modalQtyEl.value || '1', 10);
    if (!Number.isFinite(qty) || qty <= 0) {
      modalQtyEl.value = '1';
      return;
    }

    const max = Math.max(1, Number(selectedBook.stock || 1));
    if (qty > max) {
      modalQtyEl.value = String(max);
    }
  });
}

searchBtn.addEventListener('click', fetchBooks);
keywordEl.addEventListener('keypress', e => {
  if (e.key === 'Enter') fetchBooks();
});

window.addToCart = addToCart;
window.openBuyModal = openBuyModal;
window.goToPage = goToPage;

async function initializeStorePage() {
  if (hasInitialized) return;
  hasInitialized = true;

  renderUserChip();
  keywordEl.value = '';
  startAutoLoadWatchdog();
  await fetchBooks();

  // 某些环境下首个请求可能被浏览器中断，兜底重试一次。
  if (!currentBooks.length && !keywordEl.value.trim()) {
    setTimeout(() => {
      if (!currentBooks.length) {
        fetchBooks();
      }
    }, 500);
  }
}

initializeStorePage();

window.addEventListener('pageshow', () => {
  if (!hasSuccessfulBookFetch || !currentBooks.length) {
    fetchBooks();
  }
});

window.addEventListener('focus', () => {
  if (!hasSuccessfulBookFetch || !currentBooks.length) {
    fetchBooks();
  }
});

document.addEventListener('visibilitychange', () => {
  if (!document.hidden && (!hasSuccessfulBookFetch || !currentBooks.length)) {
    fetchBooks();
  }
});

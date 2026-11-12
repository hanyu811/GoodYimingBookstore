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

const cartListEl = document.getElementById('cartList');
const cartTotalTextEl = document.getElementById('cartTotalText');
const cartMessageEl = document.getElementById('cartMessage');
const refreshCartBtn = document.getElementById('refreshCartBtn');
const checkoutBtn = document.getElementById('checkoutBtn');
const userChipEl = document.getElementById('userChip');
const cartItemCountEl = document.getElementById('cartItemCount');
const cartBookTypeCountEl = document.getElementById('cartBookTypeCount');
const cartTotalStatEl = document.getElementById('cartTotalStat');
const shippingAddressEl = document.getElementById('shippingAddress');
const shippingPhoneEl = document.getElementById('shippingPhone');

function isLikelyPhone(value) {
  const normalized = (value || '').trim();
  if (normalized.length < 6 || normalized.length > 20) return false;
  return /^[0-9+\-()\s]+$/.test(normalized);
}

function renderUserChip() {
  const displayName = localStorage.getItem('displayName') || localStorage.getItem('userName') || '读者';
  if (userChipEl) {
    userChipEl.textContent = `${displayName} · 读者`;
  }
}

function updateCartStats(payload) {
  const items = payload.items || [];
  const totalAmount = Number(payload.totalAmount || 0);
  if (cartItemCountEl) cartItemCountEl.textContent = String(items.length);
  if (cartBookTypeCountEl) cartBookTypeCountEl.textContent = String(new Set(items.map(x => x.bookId)).size);
  if (cartTotalStatEl) cartTotalStatEl.textContent = totalAmount.toFixed(2);
}

function showCartMessage(text, isError = false) {
  cartMessageEl.textContent = text;
  cartMessageEl.style.color = isError ? '#e74c3c' : '#27ae60';
  setTimeout(() => {
    cartMessageEl.textContent = '';
  }, 3000);
}

function renderCart(payload) {
  updateCartStats(payload);

  const items = payload.items || [];
  if (!items.length) {
    cartListEl.innerHTML = '<tr><td colspan="5" style="text-align:center; color:#7f8c8d;">购物车为空</td></tr>';
    cartTotalTextEl.textContent = '合计：¥0.00';
    return;
  }

  cartListEl.innerHTML = items.map(item => `
    <tr>
      <td class="cart-cell-book">${item.bookTitle}</td>
      <td class="cart-cell-price">¥${Number(item.price).toFixed(2)}</td>
      <td class="cart-cell-qty">
        <div class="qty-control qty-control-right qty-control-center" aria-label="数量调整">
          <input id="qty-${item.id}" type="text" value="${item.quantity}" class="form-control qty-input" readonly aria-label="当前数量" />
          <div class="qty-stepper">
            <button type="button" class="qty-btn qty-side-btn" onclick="changeCartQuantity(${item.id}, ${item.stock}, 1)">+</button>
            <button type="button" class="qty-btn qty-side-btn" onclick="changeCartQuantity(${item.id}, ${item.stock}, -1)">-</button>
          </div>
        </div>
      </td>
      <td class="cart-cell-subtotal">¥${Number(item.lineTotal).toFixed(2)}</td>
      <td class="cart-cell-action">
        <div class="action-btns">
          <button class="btn-delete" onclick="removeCartItem(${item.id})">删除</button>
        </div>
      </td>
    </tr>
  `).join('');

  cartTotalTextEl.textContent = `合计：¥${Number(payload.totalAmount || 0).toFixed(2)}`;
}

function changeCartQuantity(itemId, stock, delta) {
  const qtyInput = document.getElementById(`qty-${itemId}`);
  if (!qtyInput) return;

  const currentQty = Number.parseInt(qtyInput.value || '1', 10);
  const safeCurrent = Number.isFinite(currentQty) ? currentQty : 1;
  const nextQty = Math.min(Math.max(1, safeCurrent + delta), Math.max(1, Number(stock || 1)));

  qtyInput.value = String(nextQty);
  autoUpdateCartItem(itemId, nextQty);
}

async function loadCart() {
  const resp = await fetch('/api/reader/cart', {
    headers: { 'X-Auth-Token': token }
  });

  if (!resp.ok) {
    showCartMessage('❌ 购物车加载失败，请重新登录', true);
    return;
  }

  const payload = await resp.json();
  renderCart(payload);

  const cachedAddress = localStorage.getItem('shippingAddress') || '';
  const cachedPhone = localStorage.getItem('shippingPhone') || '';
  const defaultAddress = payload.defaultShippingAddress || cachedAddress;
  const defaultPhone = payload.defaultShippingPhone || cachedPhone;

  if (shippingAddressEl && !shippingAddressEl.value.trim()) {
    shippingAddressEl.value = defaultAddress;
  }

  if (shippingPhoneEl && !shippingPhoneEl.value.trim()) {
    shippingPhoneEl.value = defaultPhone;
  }
}

async function autoUpdateCartItem(itemId, value) {
  const quantity = Number.parseInt(String(value || '1'), 10);
  if (!Number.isFinite(quantity) || quantity <= 0) {
    showCartMessage('❌ 数量必须大于 0；如需删除请点删除', true);
    loadCart();
    return;
  }

  const resp = await fetch(`/api/reader/cart/items/${itemId}`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      'X-Auth-Token': token
    },
    body: JSON.stringify({ quantity })
  });

  if (resp.ok) {
    showCartMessage('✅ 数量已自动更新');
    loadCart();
    return;
  }

  let text = '❌ 更新失败';
  try {
    const err = await resp.json();
    if (err?.message) text = `❌ ${err.message}`;
  } catch {
    // ignore
  }
  showCartMessage(text, true);
}

async function removeCartItem(itemId) {
  const resp = await fetch(`/api/reader/cart/items/${itemId}`, {
    method: 'DELETE',
    headers: { 'X-Auth-Token': token }
  });

  if (resp.ok) {
    showCartMessage('✅ 已移除商品');
    loadCart();
    return;
  }

  let text = '❌ 删除失败';
  try {
    const err = await resp.json();
    if (err?.message) text = `❌ ${err.message}`;
  } catch {
    // ignore
  }
  showCartMessage(text, true);
}

async function checkout() {
  const shippingAddress = (shippingAddressEl?.value || '').trim();
  const shippingPhone = (shippingPhoneEl?.value || '').trim();

  if (!shippingAddress || !shippingPhone) {
    showCartMessage('❌ 请先填写收货地址和联系电话', true);
    return;
  }

  if (!isLikelyPhone(shippingPhone)) {
    showCartMessage('❌ 联系电话格式不正确', true);
    return;
  }

  const confirmed = confirm(`确认结算并生成订单吗？\n收货地址：${shippingAddress}\n联系电话：${shippingPhone}`);
  if (!confirmed) return;

  const resp = await fetch('/api/reader/cart/checkout', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Auth-Token': token
    },
    body: JSON.stringify({
      shippingAddress,
      shippingPhone
    })
  });

  if (resp.ok) {
    const result = await resp.json();
    localStorage.setItem('shippingAddress', shippingAddress);
    localStorage.setItem('shippingPhone', shippingPhone);
    showCartMessage(`✅ 结算成功，订单号 #${result.orderId}`);
    loadCart();
    return;
  }

  let text = '❌ 结算失败';
  try {
    const err = await resp.json();
    if (err?.message) text = `❌ ${err.message}`;
  } catch {
    // ignore
  }
  showCartMessage(text, true);
}

refreshCartBtn.addEventListener('click', loadCart);
checkoutBtn.addEventListener('click', checkout);

window.removeCartItem = removeCartItem;
window.changeCartQuantity = changeCartQuantity;

renderUserChip();
loadCart();

function routeByRole(role) {
  if (role === 'admin') return '/admin.html';
  if (role === 'seller') return '/seller.html';
  return '/store.html';
}

const savedRole = localStorage.getItem('role');
const savedToken = localStorage.getItem('authToken');
if (savedRole && savedToken) {
  window.location.replace(routeByRole(savedRole));
}

const loginBtn = document.getElementById('loginBtn');
const messageEl = document.getElementById('message');
const roleEl = document.getElementById('role');
const usernameEl = document.getElementById('username');
const passwordEl = document.getElementById('password');

function setLoginMessage(text, isError) {
  messageEl.textContent = text;
  messageEl.style.color = isError ? 'var(--danger)' : '#166534';
}

async function getErrorMessage(resp, fallbackText) {
  try {
    const err = await resp.json();
    if (err?.message) {
      return `❌ ${err.message}`;
    }
  } catch {
    // ignore
  }
  return fallbackText;
}

loginBtn.addEventListener('click', async () => {
  setLoginMessage('', true);
  loginBtn.textContent = '验证中...';
  loginBtn.disabled = true;

  const payload = {
    userName: usernameEl.value.trim(),
    password: passwordEl.value,
    role: roleEl.value
  };

  if (!payload.userName || !payload.password) {
    setLoginMessage('❌ 请填写账号和密码', true);
    loginBtn.textContent = '进入系统';
    loginBtn.disabled = false;
    return;
  }

  try {
    const resp = await fetch('/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    if (!resp.ok) {
      setLoginMessage(await getErrorMessage(resp, '❌ 登录失败：账号/密码错误，或身份选择不匹配'), true);
      loginBtn.textContent = '进入系统';
      loginBtn.disabled = false;
      return;
    }

    const result = await resp.json();
    localStorage.setItem('role', result.role);
    localStorage.setItem('userName', payload.userName);
    if (result.displayName) {
      localStorage.setItem('displayName', result.displayName);
    }
    if (result.token) {
      localStorage.setItem('authToken', result.token);
    }

    if (typeof result.address === 'string' && result.address.trim()) {
      localStorage.setItem('shippingAddress', result.address.trim());
    }

    if (typeof result.phoneNumber === 'string' && result.phoneNumber.trim()) {
      localStorage.setItem('shippingPhone', result.phoneNumber.trim());
    }

    window.location.replace(result.redirectUrl || routeByRole(result.role));
  } catch {
    setLoginMessage('❌ 连接失败：请先确认后端服务已启动（http://localhost:5229）', true);
    loginBtn.textContent = '进入系统';
    loginBtn.disabled = false;
  }
});

// 支持回车提交登录
document.addEventListener('keypress', function (e) {
  if (e.key === 'Enter') {
    loginBtn.click();
  }
});

roleEl.addEventListener('change', () => {
  if (roleEl.value === 'reader') usernameEl.value = 'reader1';
  if (roleEl.value === 'seller') usernameEl.value = 'seller1';
  if (roleEl.value === 'admin') usernameEl.value = 'admin';
  if (roleEl.value !== 'reader') {
    passwordEl.value = '';
  }
});

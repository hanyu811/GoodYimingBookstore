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

const registerBtn = document.getElementById('registerBtn');
const registerMessageEl = document.getElementById('registerMessage');

const registerUserNameEl = document.getElementById('registerUserName');
const registerDisplayNameEl = document.getElementById('registerDisplayName');
const registerPasswordEl = document.getElementById('registerPassword');
const registerConfirmPasswordEl = document.getElementById('registerConfirmPassword');
const registerAddressEl = document.getElementById('registerAddress');
const registerPhoneNumberEl = document.getElementById('registerPhoneNumber');

function setRegisterMessage(text, isError) {
  registerMessageEl.textContent = text;
  registerMessageEl.style.color = isError ? 'var(--danger)' : '#166534';
}

function isLikelyPhone(value) {
  if (!value) return true;
  const normalized = value.trim();
  if (normalized.length < 6 || normalized.length > 20) return false;
  return /^[0-9+\-()\s]+$/.test(normalized);
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

registerBtn.addEventListener('click', async () => {
  setRegisterMessage('', true);

  const payload = {
    userName: registerUserNameEl.value.trim(),
    displayName: registerDisplayNameEl.value.trim(),
    password: registerPasswordEl.value,
    address: registerAddressEl.value.trim(),
    phoneNumber: registerPhoneNumberEl.value.trim()
  };

  const confirmPassword = registerConfirmPasswordEl.value;

  if (!payload.userName || !payload.password) {
    setRegisterMessage('❌ 请输入读者用户名和注册密码', true);
    return;
  }

  if (payload.password.length < 6) {
    setRegisterMessage('❌ 密码长度至少为 6 位', true);
    return;
  }

  if (payload.password !== confirmPassword) {
    setRegisterMessage('❌ 两次输入的密码不一致', true);
    return;
  }

  if (!isLikelyPhone(payload.phoneNumber)) {
    setRegisterMessage('❌ 联系电话格式不正确', true);
    return;
  }

  registerBtn.textContent = '注册中...';
  registerBtn.disabled = true;

  try {
    const resp = await fetch('/api/auth/register-reader', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    if (!resp.ok) {
      setRegisterMessage(await getErrorMessage(resp, '❌ 注册失败，请稍后重试'), true);
      registerBtn.textContent = '注册为读者';
      registerBtn.disabled = false;
      return;
    }

    setRegisterMessage('✅ 注册成功，正在跳转到登录页...', false);
    setTimeout(() => {
      window.location.replace('/');
    }, 900);
  } catch {
    setRegisterMessage('❌ 连接失败：请先确认后端服务已启动（http://localhost:5229）', true);
  }

  registerBtn.textContent = '注册为读者';
  registerBtn.disabled = false;
});

document.addEventListener('keypress', function (e) {
  if (e.key === 'Enter') {
    registerBtn.click();
  }
});

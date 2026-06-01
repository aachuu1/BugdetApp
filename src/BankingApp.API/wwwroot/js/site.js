window.authFetch = function (url, options = {}) {
    const token = localStorage.getItem('authToken');
    return fetch(url, {
        ...options,
        headers: { 'Content-Type': 'application/json', ...(token ? { 'Authorization': 'Bearer ' + token } : {}), ...(options.headers || {}) }
    });
};

window.showToast = function (msg, type = 'success') {
    let c = document.getElementById('_toasts');
    if (!c) { c = document.createElement('div'); c.id = '_toasts'; c.style.cssText = 'position:fixed;bottom:20px;right:20px;z-index:9999;display:flex;flex-direction:column;gap:8px;'; document.body.appendChild(c); }
    const colors = { success: '#059669', danger: '#dc2626', warning: '#d97706', info: '#9333ea' };
    const icons = { success: 'bi-check-circle-fill', danger: 'bi-exclamation-circle-fill', warning: 'bi-exclamation-triangle-fill', info: 'bi-info-circle-fill' };
    const t = document.createElement('div');
    t.style.cssText = `background:white;border-left:3px solid ${colors[type] || '#9333ea'};border-radius:8px;padding:10px 14px;box-shadow:0 4px 20px rgba(0,0,0,0.12);display:flex;align-items:center;gap:8px;min-width:260px;font-size:13px;font-family:Inter,sans-serif;animation:slideIn 0.25s ease;`;
    t.innerHTML = `<i class="bi ${icons[type] || 'bi-info-circle-fill'}" style="color:${colors[type]};font-size:14px;flex-shrink:0;"></i><span style="flex:1;color:#2d1b4e;font-weight:500;">${msg}</span><button onclick="this.parentElement.remove()" style="background:none;border:none;cursor:pointer;color:#b8a8d4;font-size:13px;padding:0;">✕</button>`;
    c.appendChild(t);
    setTimeout(() => { if (t.parentNode) t.remove(); }, 4000);
};

const style = document.createElement('style');
style.textContent = '@keyframes slideIn{from{opacity:0;transform:translateX(40px)}to{opacity:1;transform:translateX(0)}}';
document.head.appendChild(style);
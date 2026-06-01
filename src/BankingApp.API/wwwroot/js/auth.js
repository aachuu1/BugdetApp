(function () {
    function init() {
        const token = localStorage.getItem('authToken');
        const userRaw = localStorage.getItem('authUser');
        const isAuth = window.location.pathname.startsWith('/Account/') || window.location.pathname === '/';
        const topnav = document.getElementById('topnav');

        if (token && userRaw) {
            try {
                const user = JSON.parse(userRaw);
                if (topnav) topnav.classList.remove('d-none');
                const av = document.getElementById('navAvatar');
                const nm = document.getElementById('navUserName');
                if (av) av.textContent = (user.fullName || 'U').split(' ').map(w => w[0]).join('').slice(0, 2).toUpperCase();
                if (nm) nm.textContent = user.fullName || user.email;
                const path = window.location.pathname;
                document.querySelectorAll('.nav-link[data-page]').forEach(link => {
                    link.classList.toggle('active', path.startsWith('/' + link.dataset.page));
                });
            } catch (e) { logout(); }
        } else {
            if (topnav) topnav.classList.add('d-none');
        }
    }

    window.logout = function () {
        localStorage.removeItem('authToken');
        localStorage.removeItem('authUser');
        window.location = '/Account/Login';
    };

    window.requireAuth = function () {
        if (!localStorage.getItem('authToken')) window.location = '/Account/Login';
    };

    document.addEventListener('DOMContentLoaded', init);
})();
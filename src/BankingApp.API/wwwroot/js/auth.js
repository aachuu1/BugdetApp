// immediately invoked function to avoid polluting the global scope
(function () {

    function init() {
        const token = localStorage.getItem('authToken');
        const userRaw = localStorage.getItem('authUser');
        const isAuth = window.location.pathname.startsWith('/Account/') || window.location.pathname === '/';
        const topnav = document.getElementById('topnav');

        if (token && userRaw) {
            try {
                const user = JSON.parse(userRaw);

                // show the authenticated nav if the user is logged in
                if (topnav) topnav.classList.remove('d-none');

                // set the avatar initials from the user's full name e.g. "Ion Popescu" -> "IP"
                const av = document.getElementById('navAvatar');
                const nm = document.getElementById('navUserName');
                if (av) av.textContent = (user.fullName || 'U').split(' ').map(w => w[0]).join('').slice(0, 2).toUpperCase();
                if (nm) nm.textContent = user.fullName || user.email;

                // highlight the active nav link based on the current url
                const path = window.location.pathname;
                document.querySelectorAll('.nav-link[data-page]').forEach(link => {
                    link.classList.toggle('active', path.startsWith('/' + link.dataset.page));
                });

            } catch (e) {
                // if the stored user data is corrupted, log out and clean up
                logout();
            }
        } else {
            // hide the nav for unauthenticated users
            if (topnav) topnav.classList.add('d-none');
        }
    }

    // clears the jwt token and user data from localstorage then redirects to login
    window.logout = function () {
        localStorage.removeItem('authToken');
        localStorage.removeItem('authUser');
        window.location = '/Account/Login';
    };

    // used on protected pages to redirect unauthenticated users to login
    window.requireAuth = function () {
        if (!localStorage.getItem('authToken')) window.location = '/Account/Login';
    };

    // run init after the dom is fully loaded
    document.addEventListener('DOMContentLoaded', init);
})();
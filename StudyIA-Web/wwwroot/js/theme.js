// theme.js — dark / light mode helpers for Blazor WASM

window.themeManager = {
    // Toggle between dark and light, persist to localStorage, return true if now dark
    toggle: function () {
        const html   = document.documentElement;
        const nowDark = !html.classList.contains('dark');
        if (nowDark) {
            html.classList.add('dark');
            localStorage.setItem('theme', 'dark');
        } else {
            html.classList.remove('dark');
            localStorage.setItem('theme', 'light');
        }
        return nowDark;
    },

    isDark: function () {
        return document.documentElement.classList.contains('dark');
    }
};

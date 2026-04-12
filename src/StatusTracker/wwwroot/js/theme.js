window.setAccentColor = function (color) {
    document.documentElement.style.setProperty('--accent-color', color);
};

window.getBrowserTimezone = function () {
    try {
        return Intl.DateTimeFormat().resolvedOptions().timeZone;
    } catch (e) {
        return null;
    }
};

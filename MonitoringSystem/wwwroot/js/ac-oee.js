(function () {
    'use strict';

    var dateElement = document.getElementById('oeeDate');
    var timeElement = document.getElementById('oeeTime');
    var themeToggle = document.getElementById('oeeThemeToggle');
    var themeLabel = document.getElementById('oeeThemeLabel');

    function updateDateTime() {
        var now = new Date();
        dateElement.textContent = new Intl.DateTimeFormat('en-GB', {
            weekday: 'long',
            day: '2-digit',
            month: 'long',
            year: 'numeric'
        }).format(now).replace(/^([A-Za-z]+), /, '$1, ');
        timeElement.textContent = new Intl.DateTimeFormat('en-GB', {
            hour: '2-digit',
            minute: '2-digit',
            hour12: false
        }).format(now);
    }

    themeToggle.addEventListener('click', function () {
        var isLight = document.body.classList.toggle('light-theme');
        themeLabel.textContent = isLight ? 'Light' : 'Dark';
    });

    updateDateTime();
    window.setInterval(updateDateTime, 30000);
})();

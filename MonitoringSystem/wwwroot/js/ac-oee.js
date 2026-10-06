(function () {
    'use strict';

    var dateElement = document.getElementById('oeeDate');
    var timeElement = document.getElementById('oeeTime');
    var themeToggle = document.getElementById('oeeThemeToggle');
    var themeLabel = document.getElementById('oeeThemeLabel');
    var sidebarThemeToggle = document.getElementById('oeeSidebarThemeToggle');
    var sidebarThemeIcon = document.getElementById('oeeSidebarThemeIcon');

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
        if (sidebarThemeIcon) {
            sidebarThemeIcon.className = isLight ? 'fa-solid fa-moon' : 'fa-solid fa-sun';
        }
    });

    if (sidebarThemeToggle) {
        sidebarThemeToggle.addEventListener('click', function () {
            themeToggle.click();
        });
    }

    var moreBtn = document.getElementById('moreMenuBtn');
    var morePopup = document.getElementById('moreMenuPopup');
    var machineItem = document.getElementById('machineNavItem');
    var machineBtn = document.getElementById('machineMenuBtn');

    if (moreBtn && morePopup) {
        moreBtn.addEventListener('click', function (event) {
            event.preventDefault();
            event.stopPropagation();
            var isOpen = morePopup.style.display === 'flex';
            morePopup.style.display = isOpen ? 'none' : 'flex';
            moreBtn.setAttribute('aria-expanded', isOpen ? 'false' : 'true');
        });
    }

    if (machineItem && machineBtn) {
        machineBtn.addEventListener('click', function (event) {
            event.preventDefault();
            event.stopPropagation();
            var isOpen = machineItem.classList.toggle('menu-open');
            machineBtn.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
        });
    }

    document.addEventListener('click', function (event) {
        if (moreBtn && morePopup && !moreBtn.contains(event.target) && !morePopup.contains(event.target)) {
            morePopup.style.display = 'none';
            moreBtn.setAttribute('aria-expanded', 'false');
        }

        if (machineItem && machineBtn && !machineItem.contains(event.target)) {
            machineItem.classList.remove('menu-open');
            machineBtn.setAttribute('aria-expanded', 'false');
        }
    });

    var machinePageCards = document.querySelectorAll('.machine-card[data-machine-page]');
    var machinePagePrevious = document.getElementById('machinePagePrevious');
    var machinePageNext = document.getElementById('machinePageNext');
    var machinePageIndicator = document.getElementById('machinePageIndicator');
    var currentMachinePage = 1;
    var totalMachinePages = 0;

    machinePageCards.forEach(function (card) {
        totalMachinePages = Math.max(totalMachinePages, Number(card.dataset.machinePage) || 1);
    });

    function showMachinePage(pageNumber) {
        currentMachinePage = Math.max(1, Math.min(totalMachinePages, pageNumber));

        machinePageCards.forEach(function (card) {
            var isVisible = Number(card.dataset.machinePage) === currentMachinePage;
            card.classList.toggle('is-hidden', !isVisible);
            card.setAttribute('aria-hidden', isVisible ? 'false' : 'true');
        });

        if (machinePageIndicator) {
            machinePageIndicator.textContent = currentMachinePage;
        }
        if (machinePagePrevious) {
            machinePagePrevious.disabled = currentMachinePage <= 1;
        }
        if (machinePageNext) {
            machinePageNext.disabled = currentMachinePage >= totalMachinePages;
        }
    }

    if (totalMachinePages > 0 && machinePagePrevious && machinePageNext) {
        machinePagePrevious.addEventListener('click', function () {
            showMachinePage(currentMachinePage - 1);
        });
        machinePageNext.addEventListener('click', function () {
            showMachinePage(currentMachinePage + 1);
        });
        showMachinePage(1);
    }

    updateDateTime();
    window.setInterval(updateDateTime, 30000);
})();

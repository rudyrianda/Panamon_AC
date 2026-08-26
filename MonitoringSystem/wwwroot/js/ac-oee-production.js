(function () {
    'use strict';

    var dateElement = document.getElementById('productionDate');
    var datePicker = document.getElementById('productionDatePicker');
    var timeElement = document.getElementById('productionTime');
    var machineTitle = document.getElementById('productionMachineTitle');
    var modelName = document.getElementById('productionModelName');
    var productionPlanValue = document.getElementById('productionPlanValue');
    var productionActualValue = document.getElementById('productionActualValue');
    var productionDifference = document.getElementById('productionDifference');
    var productionDifferenceValue = document.getElementById('productionDifferenceValue');
    var machineTabsList = document.getElementById('productionMachineTabs');
    var machineTabs = document.querySelectorAll('.machine-tab');
    var machineTabsControls = document.querySelector('.machine-tabs-controls');
    var previousMachinesButton = document.querySelector('.machine-tabs-previous');
    var nextMachinesButton = document.querySelector('.machine-tabs-next');
    var selectedDate = null;
    var machineDataRequest = 0;

    function updateMachineTabControls() {
        var maximumScroll = Math.max(0, machineTabsList.scrollWidth - machineTabsList.clientWidth);
        var hasOverflow = maximumScroll > 1;

        machineTabsControls.hidden = !hasOverflow;
        previousMachinesButton.disabled = !hasOverflow || machineTabsList.scrollLeft <= 1;
        nextMachinesButton.disabled = !hasOverflow || machineTabsList.scrollLeft >= maximumScroll - 1;
    }

    function scrollMachineTabs(direction) {
        var firstTab = machineTabs[0];
        if (!firstTab) {
            return;
        }

        var tabStyle = window.getComputedStyle(firstTab);
        var tabStep = firstTab.getBoundingClientRect().width
            + parseFloat(tabStyle.marginLeft)
            + parseFloat(tabStyle.marginRight);
        var visibleTabs = Math.max(1, Math.floor(machineTabsList.clientWidth / tabStep));

        machineTabsList.scrollBy({
            left: direction * tabStep * Math.max(1, visibleTabs - 1),
            behavior: 'smooth'
        });
    }

    function toInputDate(date) {
        var year = date.getFullYear();
        var month = String(date.getMonth() + 1).padStart(2, '0');
        var day = String(date.getDate()).padStart(2, '0');
        return year + '-' + month + '-' + day;
    }

    function openDatePicker() {
        if (typeof datePicker.showPicker === 'function') {
            datePicker.showPicker();
        } else {
            datePicker.focus();
        }
    }

    function updateClock() {
        var now = new Date();
        var displayDate = selectedDate || now;
        var date = new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: 'long' }).format(displayDate);
        var weekday = new Intl.DateTimeFormat('en-GB', { weekday: 'long' }).format(displayDate);
        var parts = new Intl.DateTimeFormat('en-GB', {
            hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false
        }).formatToParts(now);
        var values = {};
        parts.forEach(function (part) { values[part.type] = part.value; });
        dateElement.innerHTML = date + ',<br />' + weekday;
        timeElement.textContent = values.hour + ' : ' + values.minute + ' : ' + values.second;
    }

    function formatQuantity(value) {
        return Number(value || 0).toLocaleString('id-ID');
    }

    async function loadMachineData(tab) {
        if (tab.dataset.usesDatabaseModel !== 'true') {
            modelName.textContent = 'BC-MetalPiece1/1';
            productionPlanValue.textContent = '0';
            productionActualValue.textContent = '2106';
            productionDifferenceValue.textContent = '-2106';
            productionDifference.classList.add('negative');
            return;
        }

        var requestNumber = ++machineDataRequest;
        var requestedDate = datePicker.value || toInputDate(new Date());
        modelName.textContent = tab.dataset.model || '-';
        productionPlanValue.textContent = '...';
        productionActualValue.textContent = '...';
        productionDifferenceValue.textContent = '...';

        try {
            var query = new URLSearchParams({
                handler: 'MachineData',
                machine: tab.dataset.machine,
                productionDate: requestedDate
            });
            var response = await fetch(window.location.pathname + '?' + query.toString(), {
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                cache: 'no-store'
            });

            if (!response.ok) {
                throw new Error('Production request failed with status ' + response.status);
            }

            var data = await response.json();
            if (requestNumber !== machineDataRequest || !tab.classList.contains('active') || datePicker.value !== requestedDate) {
                return;
            }

            tab.dataset.model = data.model || '';
            modelName.textContent = data.model || '-';
            productionPlanValue.textContent = formatQuantity(data.productionPlan);
            productionActualValue.textContent = formatQuantity(data.actual);
            var difference = Number(data.productionPlan || 0) - Number(data.actual || 0);
            productionDifferenceValue.textContent = formatQuantity(difference);
            productionDifference.classList.toggle('negative', difference < 0);
        } catch (error) {
            if (requestNumber !== machineDataRequest || !tab.classList.contains('active')) {
                return;
            }

            console.error(error);
            productionPlanValue.textContent = '-';
            productionActualValue.textContent = '-';
            productionDifferenceValue.textContent = '-';
            productionDifference.classList.remove('negative');
        }
    }

    datePicker.value = toInputDate(new Date());
    datePicker.addEventListener('click', function () {
        openDatePicker();
    });
    dateElement.addEventListener('keydown', function (event) {
        if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            openDatePicker();
        }
    });
    datePicker.addEventListener('change', function () {
        if (!datePicker.value) {
            selectedDate = null;
        } else {
            var parts = datePicker.value.split('-').map(Number);
            selectedDate = new Date(parts[0], parts[1] - 1, parts[2]);
        }
        updateClock();

        var activeTab = document.querySelector('.machine-tab.active');
        if (activeTab) {
            loadMachineData(activeTab);
        }
    });

    machineTabs.forEach(function (tab) {
        tab.addEventListener('click', function () {
            machineTabs.forEach(function (item) { item.classList.remove('active'); });
            tab.classList.add('active');
            machineTitle.textContent = tab.dataset.machine.toUpperCase();
            loadMachineData(tab);
        });
        tab.addEventListener('focus', function () {
            tab.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'nearest' });
        });
    });

    previousMachinesButton.addEventListener('click', function () { scrollMachineTabs(-1); });
    nextMachinesButton.addEventListener('click', function () { scrollMachineTabs(1); });
    machineTabsList.addEventListener('scroll', updateMachineTabControls, { passive: true });
    window.addEventListener('resize', updateMachineTabControls);

    updateClock();
    window.requestAnimationFrame(updateMachineTabControls);
    window.setInterval(updateClock, 1000);
}());

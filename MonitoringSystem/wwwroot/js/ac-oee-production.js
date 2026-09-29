(function () {
    'use strict';

    var datePicker = document.getElementById('productionDatePicker');
    var machineTitle = document.getElementById('productionMachineTitle');
    var modelName = document.getElementById('productionModelName');
    var productionPlanValue = document.getElementById('productionPlanValue');
    var planBySutValue = document.getElementById('planBySutValue');
    var productionActualValue = document.getElementById('productionActualValue');
    var productionDifference = document.getElementById('productionDifference');
    var productionDifferenceValue = document.getElementById('productionDifferenceValue');
    var productionRemainingValue = document.getElementById('productionRemainingValue');
    var machineTabsList = document.getElementById('productionMachineTabs');
    var machineTabs = document.querySelectorAll('.machine-tab');
    var machineTabsControls = document.querySelector('.machine-tabs-controls');
    var previousMachinesButton = document.querySelector('.machine-tabs-previous');
    var nextMachinesButton = document.querySelector('.machine-tabs-next');
    var machineDataRequest = 0;
    var productionOeeGauge = document.getElementById('productionOeeGauge');
    var productionOeeValue = document.getElementById('productionOeeValue');
    var productionOeeLegend = document.getElementById('productionOeeLegend');
    var productionAbilityLegend = document.getElementById('productionAbilityLegend');
    var productionOperatingLegend = document.getElementById('productionOperatingLegend');
    var productionQualityLegend = document.getElementById('productionQualityLegend');
    var defaultOeeMetrics = { oee: 128, ability: 180.1, operating: 71.1, quality: 100 };

    function formatPercent(value, keepDecimal) {
        var formatted = Number(value).toFixed(1);
        if (!keepDecimal) {
            formatted = formatted.replace(/\.0$/, '');
        }
        return formatted;
    }

    function setRingValue(ringId, value) {
        var ring = document.getElementById(ringId);
        if (!ring) {
            return;
        }

        var boundedValue = Math.max(0, Math.min(100, Number(value) || 0));
        ring.style.strokeDasharray = boundedValue + ' ' + (100 - boundedValue);
    }

    function updateOeeMetrics(metrics) {
        var oee = Number(metrics.oee) || 0;
        var ability = Number(metrics.ability) || 0;
        var operating = Number(metrics.operating) || 0;
        var quality = Number(metrics.quality) || 0;
        var oeeText = formatPercent(oee, false) + '%';
        var abilityText = formatPercent(ability, true) + '%';
        var operatingText = formatPercent(operating, false) + '%';
        var qualityText = formatPercent(quality, false) + '%';

        setRingValue('productionOeeRing', oee);
        setRingValue('productionOperatingRing', operating);
        setRingValue('productionAbilityRing', ability);
        setRingValue('productionQualityRing', quality);

        productionOeeValue.textContent = oeeText;
        productionOeeLegend.textContent = 'OEE : ' + oeeText;
        productionAbilityLegend.textContent = 'Ability : ' + abilityText;
        productionOperatingLegend.textContent = 'Operating : ' + operatingText;
        productionQualityLegend.textContent = 'Quality : ' + qualityText;
        productionOeeGauge.setAttribute('aria-label', 'OEE ' + oeeText + ' percent');
    }

    function resetOeeMetrics() {
        updateOeeMetrics(defaultOeeMetrics);
    }

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

    function formatQuantity(value) {
        return Number(value || 0).toLocaleString('id-ID');
    }

    function showEvaporatorMetrics(data) {
        var productionPlan = Number(data.productionPlan || 0);
        var planBySut = 1170;
        var actual = Number(data.actual || 0);
        var difference = actual - productionPlan;
        var remaining = productionPlan - actual;

        productionPlanValue.textContent = formatQuantity(productionPlan);
        planBySutValue.textContent = formatQuantity(planBySut);
        productionActualValue.textContent = formatQuantity(actual);
        productionDifferenceValue.textContent = formatQuantity(difference);
        productionDifference.classList.toggle('negative', difference < 0);
        productionRemainingValue.querySelector('output').textContent = formatQuantity(remaining);
        productionRemainingValue.classList.toggle('negative', remaining < 0);
    }

    function showOtherMachineMetrics(data) {
        var productionPlan = Number(data.productionPlan || 0);
        var actual = Number(data.actual || 0);
        var difference = productionPlan - actual;

        productionPlanValue.textContent = formatQuantity(productionPlan);
        planBySutValue.textContent = '1170';
        productionActualValue.textContent = formatQuantity(actual);
        productionDifferenceValue.textContent = formatQuantity(difference);
        productionDifference.classList.toggle('negative', difference < 0);
        productionRemainingValue.querySelector('output').textContent = '-2106';
        productionRemainingValue.classList.add('negative');
    }

    async function loadMachineData(tab) {
        var requestNumber = ++machineDataRequest;
        var isEvaporator = tab.dataset.machine === 'Evaporator';
        if (!isEvaporator) {
            resetOeeMetrics();
        }

        if (tab.dataset.usesDatabaseModel !== 'true') {
            modelName.textContent = 'BC-MetalPiece1/1';
            productionPlanValue.textContent = '0';
            planBySutValue.textContent = '1170';
            productionActualValue.textContent = '2106';
            productionDifferenceValue.textContent = '-2106';
            productionDifference.classList.add('negative');
            productionRemainingValue.querySelector('output').textContent = '-2106';
            productionRemainingValue.classList.add('negative');
            return;
        }

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
            if (isEvaporator) {
                showEvaporatorMetrics(data);
            } else {
                showOtherMachineMetrics(data);
            }

            if (data.oeeMetrics) {
                updateOeeMetrics(data.oeeMetrics);
            } else {
                resetOeeMetrics();
            }
        } catch (error) {
            if (requestNumber !== machineDataRequest || !tab.classList.contains('active')) {
                return;
            }

            console.error(error);
            productionPlanValue.textContent = '-';
            productionActualValue.textContent = '-';
            productionDifferenceValue.textContent = '-';
            productionDifference.classList.remove('negative');
            productionRemainingValue.querySelector('output').textContent = '-';
            productionRemainingValue.classList.remove('negative');
            resetOeeMetrics();
        }
    }

    var initialProductionDate = new Date();
    if (initialProductionDate.getHours() < 7) {
        initialProductionDate.setDate(initialProductionDate.getDate() - 1);
    }
    datePicker.value = toInputDate(initialProductionDate);

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

    var initialActiveTab = document.querySelector('.machine-tab.active');
    if (initialActiveTab) {
        machineTitle.textContent = initialActiveTab.dataset.machine.toUpperCase();
        loadMachineData(initialActiveTab);
    }
    window.requestAnimationFrame(updateMachineTabControls);
}());

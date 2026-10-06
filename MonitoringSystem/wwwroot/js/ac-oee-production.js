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

    // DIFFERENCE: actual lebih = "+3", kurang = "-3", sama = "0"
    function formatDifference(value) {
        var n = Number(value || 0);
        return (n > 0 ? '+' : '') + formatQuantity(n);
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
        productionDifferenceValue.textContent = formatDifference(difference);
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
        productionDifferenceValue.textContent = formatDifference(difference);
        productionDifference.classList.toggle('negative', difference < 0);
        productionRemainingValue.querySelector('output').textContent = '-2106';
        productionRemainingValue.classList.add('negative');
    }

    // Expander Kyoshin 635: data langsung dari layar utama GOT (B-1) di PLC, lewat PLC ROHIB (/plcrohib-app).
    // MODEL R10, PRODUCTION PLAN R20, ACTUAL R22, PLAN BY SUT R23, DIFFERENCE D20.
    // Baris REMAINING diganti LOSS TIME di tab ini. TOTAL NG = DEFECT R24 (tab lain tetap "Under Development").
    // Loss time dari timer R103 yang dicatat Plclogger per kejadian (tabel PlcKyoshinLossEvent):
    //   LOSS TIME       = akumulasi loss model yang sedang jalan (reset 0 saat ganti model / jam 07:00)  -> modelLossMin
    //   TOTAL LOSS TIME = akumulasi loss per shift hari produksi berjalan (Shift 1 07:00-15:45, Shift 2 15:45-23:15,
    //                     Shift 3 23:15-07:00); shift yang belum mulai "-"                               -> shiftLossMin
    // Tab lain: kembali ke nilai semula.
    var PLC_MAIN_URL = '/plcrohib-app/api/plc/main';
    var PLC_POLL_MS = 3000;
    var totalNgPlc = document.getElementById('productionTotalNgPlc');
    var totalNgValue = document.getElementById('productionTotalNgValue');
    var totalNgDev = document.getElementById('productionTotalNgDev');
    var shiftStopTimes = [1, 2, 3].map(function (no) {
        var el = document.getElementById('shiftStopTime' + no);
        el.dataset.original = el.textContent;
        return el;
    });

    function currentShiftNo() {
        var now = new Date();
        var minutes = now.getHours() * 60 + now.getMinutes();
        if (minutes >= 7 * 60 && minutes < 15 * 60 + 45) return 1;
        if (minutes >= 15 * 60 + 45 && minutes < 23 * 60 + 15) return 2;
        return 3;
    }

    function setShiftStopTimes(shiftLossMin) {
        shiftStopTimes.forEach(function (el, i) {
            var value = shiftLossMin ? shiftLossMin[i] : null;
            el.textContent = value === null || value === undefined ? '-' : formatQuantity(value);
        });
    }

    var remainingLabel = document.getElementById('productionRemainingLabel');

    // LOAD TIME shift yang sedang berjalan = menit sejak awal shift (detik 00) sampai sekarang,
    // dan bar biru timeline shift itu sepanjang load time. Shift lain: "-" dan bar kosong.
    var SHIFT_WINDOWS = [
        { startMinute: 7 * 60, lengthMinutes: 525 },        // Shift 1 07:00:00 - 15:45
        { startMinute: 15 * 60 + 45, lengthMinutes: 450 },  // Shift 2 15:45:00 - 23:15
        { startMinute: 23 * 60 + 15, lengthMinutes: 465 }   // Shift 3 23:15:00 - 07:00
    ];
    var shiftLoadTimes = [1, 2, 3].map(function (no) {
        var el = document.getElementById('shiftLoadTime' + no);
        el.dataset.original = el.textContent;
        return el;
    });
    var timelineTracks = [1, 2, 3].map(function (no) {
        var el = document.getElementById('timelineTrack' + no);
        el.dataset.originalHtml = el.innerHTML;
        return el;
    });

    function shiftStartDate(no) {
        var window_ = SHIFT_WINDOWS[no - 1];
        var now = new Date();
        var start = new Date(now.getFullYear(), now.getMonth(), now.getDate(),
            Math.floor(window_.startMinute / 60), window_.startMinute % 60, 0, 0);
        if (start > now) {
            start.setDate(start.getDate() - 1); // Shift 3 setelah tengah malam: mulai 23:15 kemarin
        }
        return start;
    }

    function shiftElapsedMinutes(no) {
        return Math.min(SHIFT_WINDOWS[no - 1].lengthMinutes, Math.max(0, (new Date() - shiftStartDate(no)) / 60000));
    }

    // GRAFIK PLAN VS ACTUAL (tab PLC): riwayat per menit dari tabel dbo.PlcKyoshinTrend lewat PLC ROHIB
    // (Plan = R23, Actual = R22). Titik di awal shift, setiap jam penuh, dan jam sekarang.
    var PLC_TREND_URL = '/plcrohib-app/api/plc/trend';
    var CHART_AREA = { x0: 56, x1: 894, yTop: 14, yBottom: 214 };
    var chartEls = {};
    ['chartYLabels', 'chartPlanFill', 'chartActualFill', 'chartPlanLine', 'chartActualLine',
        'chartPlanPoints', 'chartActualPoints', 'chartXLabels'].forEach(function (id) {
        var el = document.getElementById(id);
        el.dataset.original = el.tagName.toLowerCase() === 'path' ? el.getAttribute('d') : el.innerHTML;
        chartEls[id] = el;
    });
    var plcTrendSamples = [];
    var plcTrendLoadedAt = 0;

    function restoreChart() {
        Object.keys(chartEls).forEach(function (id) {
            var el = chartEls[id];
            if (el.tagName.toLowerCase() === 'path') {
                el.setAttribute('d', el.dataset.original);
            } else {
                el.innerHTML = el.dataset.original;
            }
        });
    }

    function niceChartMax(value) {
        if (value <= 0) {
            return 100;
        }
        var power = Math.pow(10, Math.floor(Math.log10(value)));
        var steps = [1, 2, 2.5, 5, 10];
        for (var i = 0; i < steps.length; i++) {
            if (steps[i] * power >= value) {
                return steps[i] * power;
            }
        }
        return 10 * power;
    }

    function timeLabel(date) {
        return String(date.getHours()).padStart(2, '0') + ':' + String(date.getMinutes()).padStart(2, '0');
    }

    function renderPlcChart() {
        var start = shiftStartDate(currentShiftNo());
        var now = new Date();
        var span = Math.max(60000, now - start);

        // Titik waktu: awal shift, tiap jam penuh setelahnya, dan sekarang
        var times = [start];
        var hour = new Date(start);
        hour.setMinutes(0, 0, 0);
        hour.setHours(hour.getHours() + 1);
        while (hour < now) {
            times.push(new Date(hour));
            hour.setHours(hour.getHours() + 1);
        }
        if (now - times[times.length - 1] >= 60000) {
            times.push(now);
        }

        // Nilai di tiap titik = sampel terakhir pada atau sebelum waktu itu (dalam shift ini)
        var points = [];
        times.forEach(function (t) {
            var sample = null;
            for (var i = 0; i < plcTrendSamples.length && plcTrendSamples[i].at <= t; i++) {
                sample = plcTrendSamples[i];
            }
            if (sample) {
                points.push({ t: t, plan: sample.plan, actual: sample.actual });
            }
        });

        var maxValue = niceChartMax(points.reduce(function (m, p) { return Math.max(m, p.plan, p.actual); }, 0));
        var xOf = function (t) { return CHART_AREA.x0 + (t - start) / span * (CHART_AREA.x1 - CHART_AREA.x0); };
        var yOf = function (v) { return CHART_AREA.yBottom - Math.max(0, v) / maxValue * (CHART_AREA.yBottom - CHART_AREA.yTop); };
        var fmt = function (n) { return n.toFixed(1); };

        chartEls.chartYLabels.innerHTML = '<text x="46" y="19">' + formatQuantity(maxValue) + '</text><text x="46" y="119">'
            + formatQuantity(maxValue / 2) + '</text><text x="46" y="219">0</text>';

        var xLabels = '';
        var lastLabelX = -100;
        times.forEach(function (t, i) {
            var x = xOf(t);
            if (x - lastLabelX < 40 && i !== times.length - 1) {
                return; // label terlalu rapat
            }
            if (x - lastLabelX < 40) {
                xLabels = xLabels.replace(/<text[^>]*>[^<]*<\/text>$/, ''); // label jam sekarang menggantikan label terakhir
            }
            xLabels += '<text x="' + fmt(x) + '" y="234">' + timeLabel(t) + '</text>';
            lastLabelX = x;
        });
        if (points.length === 0) {
            xLabels += '<text x="475" y="119" style="text-anchor:middle">Belum ada riwayat PLC untuk shift ini</text>';
        }
        chartEls.chartXLabels.innerHTML = xLabels;

        ['plan', 'actual'].forEach(function (key) {
            var cap = key === 'plan' ? 'Plan' : 'Actual';
            var line = points.map(function (p, i) { return (i ? 'L' : 'M') + fmt(xOf(p.t)) + ' ' + fmt(yOf(p[key])); }).join(' ');
            var fill = points.length
                ? 'M' + fmt(xOf(points[0].t)) + ' ' + CHART_AREA.yBottom + ' ' + line.replace(/^M/, 'L') + ' L'
                    + fmt(xOf(points[points.length - 1].t)) + ' ' + CHART_AREA.yBottom + ' Z'
                : '';
            chartEls['chart' + cap + 'Line'].setAttribute('d', line);
            chartEls['chart' + cap + 'Fill'].setAttribute('d', fill);
            chartEls['chart' + cap + 'Points'].innerHTML = points.map(function (p) {
                return '<circle cx="' + fmt(xOf(p.t)) + '" cy="' + fmt(yOf(p[key])) + '" r="4"><title>' + cap + ' '
                    + timeLabel(p.t) + ': ' + formatQuantity(p[key]) + '</title></circle>';
            }).join('');
        });
    }

    async function loadPlcTrend() {
        plcTrendLoadedAt = Date.now();
        try {
            var response = await fetch(PLC_TREND_URL, { cache: 'no-store' });
            if (!response.ok) {
                throw new Error('Trend request failed with status ' + response.status);
            }
            var data = await response.json();
            plcTrendSamples = (data.samples || []).map(function (s) {
                return { at: new Date(s.at), plan: Number(s.plan || 0), actual: Number(s.actual || 0) };
            });
        } catch (error) {
            console.error(error);
        }
        var activeTab = document.querySelector('.machine-tab.active');
        if (activeTab && activeTab.dataset.usesPlc === 'true') {
            renderPlcChart();
        }
    }

    // Jadwal istirahat (BreakTimeService Panamon) untuk tanggal produksi -> segmen hitam di timeline.
    var plcBreaks = [];
    var plcBreaksDate = null;

    function toMinuteOfDay(hhmmss) {
        var parts = String(hhmmss || '').split(':');
        return Number(parts[0] || 0) * 60 + Number(parts[1] || 0) + Number(parts[2] || 0) / 60;
    }

    async function loadPlcBreaks() {
        var date = datePicker.value;
        if (plcBreaksDate === date) {
            return;
        }
        plcBreaksDate = date;
        try {
            var query = new URLSearchParams({ handler: 'BreakTimes', productionDate: date });
            var response = await fetch(window.location.pathname + '?' + query.toString(), { cache: 'no-store' });
            if (!response.ok) {
                throw new Error('Break time request failed with status ' + response.status);
            }
            var list = await response.json();
            plcBreaks = list.map(function (b) {
                return { start: toMinuteOfDay(b.start), end: toMinuteOfDay(b.end), reason: b.reason || 'Break', label: b.start.slice(0, 5) + '-' + b.end.slice(0, 5) };
            });
        } catch (error) {
            console.error(error);
            plcBreaks = [];
            plcBreaksDate = null; // dicoba lagi pada pembaruan berikutnya
        }
        updatePlcShiftTimeline();
    }

    // Menit istirahat (jadwal istirahat) yang sudah lewat di shift yang sedang berjalan
    function elapsedBreakMinutes(no) {
        var window_ = SHIFT_WINDOWS[no - 1];
        var elapsed = shiftElapsedMinutes(no);
        return plcBreaks.reduce(function (sum, b) {
            var from = (b.start - window_.startMinute + 1440) % 1440;
            var to = Math.min(from + (b.end - b.start + 1440) % 1440, elapsed, window_.lengthMinutes);
            return from < window_.lengthMinutes && to > from ? sum + (to - from) : sum;
        }, 0);
    }

    // OEE tab PLC (Expander Kyoshin 635), rumus standar:
    //   Operating = (Load Time - Stop Time) / Load Time   Load Time = menit sejak awal shift - istirahat;
    //                                                      Stop Time = TOTAL LOSS TIME shift berjalan (akumulasi R103)
    //   Ability   = Actual / Plan                          R22 / R23 (Plan by SUT)
    //   Quality   = (Actual - Defect) / Actual             R22, R24
    //   OEE       = Operating x Ability x Quality
    // Tiap komponen dibatasi 0-100%.
    function calculatePlcOee(data) {
        var clampPercent = function (v) { return Math.max(0, Math.min(100, v)); };
        var running = currentShiftNo();
        var loadMinutes = Math.max(0, shiftElapsedMinutes(running) - elapsedBreakMinutes(running));
        var stopMinutes = Number((data.shiftLossMin && data.shiftLossMin[running - 1]) || 0);
        var actual = Number(data.actual || 0);
        var plan = Number(data.plan || 0);
        var defect = Number(data.defect || 0);

        var operating = loadMinutes > 0 ? clampPercent((loadMinutes - stopMinutes) / loadMinutes * 100) : 0;
        var ability = plan > 0 ? clampPercent(actual / plan * 100) : 0;
        var quality = actual > 0 ? clampPercent((actual - defect) / actual * 100) : 100;
        return {
            oee: Math.round(operating * ability * quality / 10000 * 10) / 10,
            operating: Math.round(operating * 10) / 10,
            ability: Math.round(ability * 10) / 10,
            quality: Math.round(quality * 10) / 10
        };
    }

    function segmentHtml(kind, left, width, title) {
        return '<i class="segment ' + kind + '-segment" style="--left:' + left.toFixed(4) + '%;--width:' + width.toFixed(4)
            + '%" title="' + title.replace(/"/g, '&quot;') + '"></i>';
    }

    function updatePlcShiftTimeline() {
        var running = currentShiftNo();
        [1, 2, 3].forEach(function (no) {
            var loadEl = shiftLoadTimes[no - 1];
            var track = timelineTracks[no - 1];
            if (no !== running) {
                loadEl.textContent = '-';
                track.innerHTML = '';
                return;
            }
            var window_ = SHIFT_WINDOWS[no - 1];
            var elapsed = shiftElapsedMinutes(no);
            loadEl.textContent = formatQuantity(Math.floor(elapsed));

            // Biru = load time (awal shift s/d sekarang); hitam = istirahat yang sudah lewat, ditimpa di atasnya
            var html = segmentHtml('run', 0, elapsed / window_.lengthMinutes * 100, 'Load time ' + Math.floor(elapsed) + ' menit');
            plcBreaks.forEach(function (b) {
                var from = (b.start - window_.startMinute + 1440) % 1440;    // menit sejak awal shift
                var length = (b.end - b.start + 1440) % 1440;
                if (from >= window_.lengthMinutes) {
                    return; // istirahat milik shift lain
                }
                var to = Math.min(from + length, elapsed, window_.lengthMinutes);
                if (to <= from) {
                    return; // belum terjadi
                }
                html += segmentHtml('break', from / window_.lengthMinutes * 100, (to - from) / window_.lengthMinutes * 100,
                    b.reason + ' ' + b.label);
            });
            track.innerHTML = html;
        });
    }

    function setPlcOnlyFields(visible) {
        remainingLabel.textContent = visible ? 'LOSS TIME' : 'REMAINING';
        totalNgPlc.hidden = !visible;
        totalNgDev.hidden = visible;
        if (visible) {
            updatePlcShiftTimeline();
            loadPlcBreaks();
            renderPlcChart();
            loadPlcTrend();
        } else {
            restoreChart();
            shiftStopTimes.forEach(function (el) { el.textContent = el.dataset.original; });
            shiftLoadTimes.forEach(function (el) { el.textContent = el.dataset.original; });
            timelineTracks.forEach(function (el) { el.innerHTML = el.dataset.originalHtml; });
        }
    }

    function showPlcOffline(reason) {
        updateOeeMetrics({ oee: 0, operating: 0, ability: 0, quality: 0 });
        productionOeeValue.textContent = '-';
        productionOeeLegend.textContent = 'OEE : -';
        productionAbilityLegend.textContent = 'Ability : -';
        productionOperatingLegend.textContent = 'Operating : -';
        productionQualityLegend.textContent = 'Quality : -';
        setShiftStopTimes(null);
        totalNgValue.textContent = '-';
        modelName.textContent = '-';
        modelName.title = reason;
        [productionPlanValue, planBySutValue, productionActualValue, productionDifferenceValue].forEach(function (el) {
            el.textContent = '-';
        });
        productionDifference.classList.remove('negative');
        productionRemainingValue.querySelector('output').textContent = '-';
        productionRemainingValue.classList.remove('negative');
    }

    function showPlcMetrics(data) {
        var productionPlan = Number(data.prodPlan || 0);
        var actual = Number(data.actual || 0);
        var difference = Number(data.difference || 0);
        var modelLoss = data.modelLossMin; // null = Plclogger belum punya akumulasi loss

        modelName.textContent = (data.model || '').trim() || '-';
        modelName.title = 'PLC ' + (data.source || '') + ' - terbaca ' + (data.readAt || '');
        productionPlanValue.textContent = formatQuantity(productionPlan);
        planBySutValue.textContent = formatQuantity(data.plan);
        productionActualValue.textContent = formatQuantity(actual);
        productionDifferenceValue.textContent = formatDifference(difference);
        productionDifference.classList.toggle('negative', difference < 0);
        productionRemainingValue.querySelector('output').textContent =
            modelLoss === null || modelLoss === undefined ? '-' : formatQuantity(modelLoss); // label: LOSS TIME
        productionRemainingValue.classList.remove('negative');
        setShiftStopTimes(data.shiftLossMin);
        totalNgValue.textContent = formatQuantity(data.defect);
        updateOeeMetrics(calculatePlcOee(data));
    }

    async function loadPlcMachineData(tab, requestNumber, silent) {
        if (!silent) {
            modelName.textContent = '...';
            productionPlanValue.textContent = '...';
            planBySutValue.textContent = '...';
            productionActualValue.textContent = '...';
            productionDifferenceValue.textContent = '...';
        }
        try {
            var response = await fetch(PLC_MAIN_URL, { cache: 'no-store' });
            if (!response.ok) {
                throw new Error('PLC request failed with status ' + response.status);
            }
            var data = await response.json();
            if (requestNumber !== machineDataRequest || !tab.classList.contains('active')) {
                return;
            }
            if (data.live) {
                showPlcMetrics(data);
            } else {
                showPlcOffline('PLC tidak terhubung (cek ' + (data.readAt || '-') + ')');
            }
        } catch (error) {
            if (requestNumber !== machineDataRequest || !tab.classList.contains('active')) {
                return;
            }
            console.error(error);
            showPlcOffline('PLC ROHIB tidak merespons');
        }
    }

    // Perbarui data PLC tiap 3 detik selama tab PLC sedang aktif
    window.setInterval(function () {
        var activeTab = document.querySelector('.machine-tab.active');
        if (activeTab && activeTab.dataset.usesPlc === 'true' && !document.hidden) {
            loadPlcBreaks();          // hanya mengambil ulang jika tanggal produksi berganti
            updatePlcShiftTimeline(); // load time & bar biru bertambah mengikuti jam
            if (Date.now() - plcTrendLoadedAt >= 60000) {
                loadPlcTrend();       // riwayat grafik dicatat per menit -> ambil ulang tiap menit
            }
            loadPlcMachineData(activeTab, machineDataRequest, true);
        }
    }, PLC_POLL_MS);

    async function loadMachineData(tab) {
        var requestNumber = ++machineDataRequest;
        var isEvaporator = tab.dataset.machine === 'Evaporator';
        if (!isEvaporator) {
            resetOeeMetrics();
        }

        setPlcOnlyFields(tab.dataset.usesPlc === 'true');
        if (tab.dataset.usesPlc === 'true') {
            await loadPlcMachineData(tab, requestNumber, false);
            return;
        }
        modelName.title = '';

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

    // WORKING TIME Shift 1 menurut hari produksi: Jumat 433 menit, hari lain 473 menit
    var FRIDAY = 5;
    document.getElementById('shiftWorkTime1').textContent = initialProductionDate.getDay() === FRIDAY ? '433' : '473';

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

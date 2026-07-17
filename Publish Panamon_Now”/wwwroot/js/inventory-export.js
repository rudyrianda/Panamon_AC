document.addEventListener("DOMContentLoaded", function () {
    var statusMessage = window.__invStatusMessage || '';
    var message = window.__invMessage || '';
    if (statusMessage === 'success') {
        Swal.fire({ icon: 'success', title: 'Success', text: message, confirmButtonText: 'OK' });
    } else if (statusMessage === 'error') {
        Swal.fire({ icon: 'error', title: 'Error', text: message, confirmButtonText: 'OK' });
    }

    const container = document.querySelector('.inv-table-container');
    const body = document.querySelector('.inv-body-wrap');
    const header = document.querySelector('.inv-header-wrap');
    const footer = document.querySelector('.inv-footer-wrap');

    // ── Snap tinggi area scroll body ke kelipatan pas tinggi 1 baris ──
    // Supaya nggak ada baris terakhir yang "kepotong setengah" pas nyampe
    // batas bawah, dan tabel Total nempel rapi tanpa keliatan ada gap.
    function snapBodyHeight() {
        if (!body) return;
        const firstRow = document.querySelector('#inventory-table-body tr');
        if (!firstRow) return;

        const rowHeight = firstRow.getBoundingClientRect().height;
        if (!rowHeight) return;

        // Lepas dulu batas tinggi biar bisa baca batas maksimal "alami" dari CSS
        body.style.maxHeight = '';
        body.style.height = '';
        const maxAllowed = body.getBoundingClientRect().height || body.clientHeight;
        if (!maxAllowed) return;

        const rows = Math.max(1, Math.floor(maxAllowed / rowHeight));
        const snappedHeight = Math.round(rows * rowHeight);

        body.style.maxHeight = snappedHeight + 'px';
        body.style.height = snappedHeight + 'px';
    }

    snapBodyHeight();

    let resizeTimeout;
    window.addEventListener('resize', function () {
        clearTimeout(resizeTimeout);
        resizeTimeout = setTimeout(snapBodyHeight, 150);
    });

    // footer = pemilik scrollbar horizontal asli (muncul di paling bawah).
    // header & body cuma ikut posisi scrollLeft footer.
    // Wheel/drag di area manapun (header/body/footer) tetap menggerakkan semuanya.
    if (container && body && footer) {
        let lockedAxis = null;   // 'x' | 'y' | null
        let axisTimeout = null;

        container.addEventListener('wheel', function (e) {
            // Tentukan axis dominan tiap kali mulai scroll baru
            if (lockedAxis === null) {
                lockedAxis = Math.abs(e.deltaX) > Math.abs(e.deltaY) ? 'x' : 'y';
            }

            if (lockedAxis === 'y') {
                // Scroll vertikal murni: cegah drift horizontal
                body.scrollTop += e.deltaY;
            } else {
                // Scroll horizontal: berlaku walau kursor sedang di atas header/body
                footer.scrollLeft += e.deltaX;
            }
            e.preventDefault();

            // Reset axis setelah user berhenti scroll sejenak (150ms)
            clearTimeout(axisTimeout);
            axisTimeout = setTimeout(() => { lockedAxis = null; }, 150);
        }, { passive: false });

        // Header & body mengikuti posisi scroll footer (sumber utama)
        footer.addEventListener('scroll', () => {
            header.scrollLeft = footer.scrollLeft;
            body.scrollLeft = footer.scrollLeft;
        });

        // Drag-to-scroll horizontal — bisa mulai drag dari header, body, ATAU footer
        let isDown = false;
        let startX;
        let scrollLeft;

        container.addEventListener('mousedown', (e) => {
            if (e.button !== 0 || e.target.closest('input, select, button, a')) return;
            isDown = true;
            container.classList.add('active-dragging');
            startX = e.pageX;
            scrollLeft = footer.scrollLeft;
        });

        document.addEventListener('mouseup', () => {
            isDown = false;
            container.classList.remove('active-dragging');
        });

        document.addEventListener('mousemove', (e) => {
            if (!isDown) return;
            e.preventDefault();
            const walk = (e.pageX - startX) * 1.5; // sensitivitas drag
            footer.scrollLeft = scrollLeft - walk;
        });
    }

    // Excel Export Handler (XLS Format with centered text and auto-calculated column widths)
    const exportBtn = document.getElementById('btn-export-csv');
    if (exportBtn) {
        exportBtn.addEventListener('click', function () {
            const headers = ['Model'];
            const dayCols = document.querySelectorAll('.inv-header-wrap thead th.col-day');
            dayCols.forEach(th => {
                headers.push(th.textContent.trim());
            });

            const bodyRows = document.querySelectorAll('#inventory-table-body tr');

            // Array to store maximum character length for each column to compute widths
            const colMaxLengths = Array(headers.length).fill(0);
            headers.forEach((h, colIdx) => {
                colMaxLengths[colIdx] = h.length;
            });

            const rowsData = [];
            bodyRows.forEach(tr => {
                const rowData = [];
                const modelTd = tr.querySelector('.col-model');
                const modelText = modelTd ? modelTd.textContent.trim() : '';
                rowData.push(modelText);
                if (modelText.length > colMaxLengths[0]) {
                    colMaxLengths[0] = modelText.length;
                }

                const dayTds = tr.querySelectorAll('.col-day');
                dayTds.forEach((td, dayIdx) => {
                    const valText = td.textContent.trim();
                    rowData.push(valText);
                    const colIdx = dayIdx + 1; // offset by 1 because of Model column
                    if (valText.length > colMaxLengths[colIdx]) {
                        colMaxLengths[colIdx] = valText.length;
                    }
                });
                rowsData.push(rowData);
            });

            // Calculate column widths based on max character length
            // Model: base length * 7 + 30 (min 200, max 300 px)
            const modelWidth = Math.min(Math.max(colMaxLengths[0] * 7 + 30, 200), 300);
            let colGroupHtml = `<col width="${modelWidth}" />`;

            // Day columns: max digits * 8 + 25 (min 50 px) to prevent ### format issues
            for (let colIdx = 1; colIdx < headers.length; colIdx++) {
                const maxLen = colMaxLengths[colIdx];
                const dayWidth = Math.max(maxLen * 8 + 25, 50);
                colGroupHtml += `<col width="${dayWidth}" />`;
            }

            // Build MSO conditional comment pieces without embedding a literal
            // "<!--" + tag sequence directly in this .js file's source text.
            const msoOpen = '<' + '!--[if gte mso 9]>';
            const msoEndIf = '<' + '![endif]--' + '>';

            let excelHtml = '';
            excelHtml += '<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel" xmlns="http://www.w3.org/TR/REC-html40">';
            excelHtml += '<head>';
            excelHtml += '<meta charset="utf-8" />';
            excelHtml += msoOpen;
            excelHtml += '<xml>';
            excelHtml += '<x:ExcelWorkbook>';
            excelHtml += '<x:ExcelWorksheets>';
            excelHtml += '<x:ExcelWorksheet>';
            excelHtml += '<x:Name>Inventory</x:Name>';
            excelHtml += '<x:WorksheetOptions>';
            excelHtml += '<x:DisplayGridlines/>';
            excelHtml += '</x:WorksheetOptions>';
            excelHtml += '</x:ExcelWorksheet>';
            excelHtml += '</x:ExcelWorksheets>';
            excelHtml += '</x:ExcelWorkbook>';
            excelHtml += '</xml>';
            excelHtml += msoEndIf;
            excelHtml += '<style>';
            excelHtml += 'table { border-collapse: collapse; }';
            excelHtml += 'th, td { border: 0.5pt solid #c0c0c0; text-align: center; font-family: Arial, sans-serif; font-size: 10pt; vertical-align: middle; }';
            excelHtml += 'th { background-color: #1e283c; color: #ffffff; font-weight: bold; }';
            excelHtml += '.col-model { text-align: center; }';
            excelHtml += '</style>';
            excelHtml += '</head>';
            excelHtml += '<body>';
            excelHtml += '<table>';
            excelHtml += `<colgroup>${colGroupHtml}</colgroup>`;
            excelHtml += '<thead><tr>';
            excelHtml += headers.map(h => `<th class="col-model">${h}</th>`).join('');
            excelHtml += '</tr></thead>';
            excelHtml += '<tbody>';

            rowsData.forEach(rowData => {
                excelHtml += '<tr>';
                excelHtml += `<td class="col-model">${rowData[0]}</td>`;
                for (let colIdx = 1; colIdx < rowData.length; colIdx++) {
                    excelHtml += `<td>${rowData[colIdx]}</td>`;
                }
                excelHtml += '</tr>';
            });

            excelHtml += '</tbody>';
            excelHtml += '</table>';
            excelHtml += '</body>';
            excelHtml += '</html>';

            const blob = new Blob([excelHtml], { type: 'application/vnd.ms-excel;charset=utf-8;' });
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.setAttribute('href', url);

            const activeMonthName = document.querySelector('.filter-content select[name="FilterBulan"] option:checked')?.textContent.trim() || 'Inventory';
            const activeYear = document.querySelector('.filter-content input[name="FilterTahun"]')?.value || '';
            link.setAttribute('download', `Inventory_${activeMonthName}_${activeYear}.xls`);
            link.style.visibility = 'hidden';
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
        });
    }

    const sel = document.querySelector('.filter-content select[name="FilterMachineLine"]');
    if (sel && sel.value) sel.classList.add('selected');
    if (sel) sel.addEventListener('change', function () {
        this.value ? this.classList.add('selected') : this.classList.remove('selected');
    });
});
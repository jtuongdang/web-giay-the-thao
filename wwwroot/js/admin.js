/**
 * SneakerZone Admin Backoffice Portal JavaScript
 * Handles Charts, PO dynamic line items, RMA helpers, and Modals
 */

document.addEventListener('DOMContentLoaded', function () {
    // 1. Initialize Dashboard Charts if canvas elements exist
    initDashboardCharts();

    // 2. Initialize PO Dynamic Line Items
    initPurchaseOrderForm();

    // 3. Initialize RMA Exchange / Refund form
    initRmaForm();

    // 4. Initialize Supplier Payment helper
    initSupplierPaymentHelper();

    // 5. Auto dismiss alerts after 5 seconds
    setTimeout(function () {
        const alerts = document.querySelectorAll('.alert-dismissible');
        alerts.forEach(function (alert) {
            const bsAlert = bootstrap.Alert.getOrCreateInstance(alert);
            bsAlert.close();
        });
    }, 5000);
});

// ==========================================
// 1. DASHBOARD CHARTS (Chart.js)
// ==========================================
function initDashboardCharts() {
    const revChartCanvas = document.getElementById('revenueProfitChart');
    const cashChartCanvas = document.getElementById('cashFlowChart');

    if (!revChartCanvas && !cashChartCanvas) return;

    fetch('/Admin/GetChartData')
        .then(response => {
            if (!response.ok) throw new Error('Không thể tải dữ liệu biểu đồ');
            return response.json();
        })
        .then(data => {
            // Revenue & Gross Profit Chart
            if (revChartCanvas && typeof Chart !== 'undefined') {
                new Chart(revChartCanvas, {
                    type: 'bar',
                    data: {
                        labels: data.labels,
                        datasets: [
                            {
                                label: 'Doanh thu thuần (VNĐ)',
                                data: data.revenue,
                                backgroundColor: 'rgba(59, 130, 246, 0.75)',
                                borderColor: '#2563eb',
                                borderWidth: 1.5,
                                borderRadius: 6
                            },
                            {
                                label: 'Lợi nhuận gộp (VNĐ)',
                                data: data.profit,
                                backgroundColor: 'rgba(16, 185, 129, 0.85)',
                                borderColor: '#059669',
                                borderWidth: 1.5,
                                borderRadius: 6
                            }
                        ]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: {
                            legend: {
                                position: 'top',
                                labels: {
                                    font: { family: "'Plus Jakarta Sans', sans-serif", weight: '600' }
                                }
                            },
                            tooltip: {
                                callbacks: {
                                    label: function (ctx) {
                                        return ctx.dataset.label + ': ' + new Intl.NumberFormat('vi-VN').format(ctx.raw) + ' đ';
                                    }
                                }
                            }
                        },
                        scales: {
                            y: {
                                beginAtZero: true,
                                ticks: {
                                    callback: function (val) {
                                        return (val / 1000000) + 'M';
                                    }
                                }
                            }
                        }
                    }
                });
            }

            // Cash Flow Chart (Inflow vs Outflow)
            if (cashChartCanvas && typeof Chart !== 'undefined') {
                new Chart(cashChartCanvas, {
                    type: 'line',
                    data: {
                        labels: data.labels,
                        datasets: [
                            {
                                label: 'Thực thu (Tiền vào)',
                                data: data.cashInflow,
                                borderColor: '#10b981',
                                backgroundColor: 'rgba(16, 185, 129, 0.12)',
                                fill: true,
                                tension: 0.35,
                                borderWidth: 2.5,
                                pointRadius: 3
                            },
                            {
                                label: 'Thực chi (Tiền ra)',
                                data: data.cashOutflow,
                                borderColor: '#ef4444',
                                backgroundColor: 'rgba(239, 68, 68, 0.1)',
                                fill: true,
                                tension: 0.35,
                                borderWidth: 2.5,
                                pointRadius: 3
                            }
                        ]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: {
                            legend: {
                                position: 'top',
                                labels: {
                                    font: { family: "'Plus Jakarta Sans', sans-serif", weight: '600' }
                                }
                            },
                            tooltip: {
                                callbacks: {
                                    label: function (ctx) {
                                        return ctx.dataset.label + ': ' + new Intl.NumberFormat('vi-VN').format(ctx.raw) + ' đ';
                                    }
                                }
                            }
                        },
                        scales: {
                            y: {
                                beginAtZero: true,
                                ticks: {
                                    callback: function (val) {
                                        return (val / 1000000) + 'M';
                                    }
                                }
                            }
                        }
                    }
                });
            }
        })
        .catch(err => {
            console.error('Error fetching chart data:', err);
        });
}

// ==========================================
// 2. PURCHASE ORDER DYNAMIC LINE ITEMS
// ==========================================
function initPurchaseOrderForm() {
    const tableBody = document.getElementById('poItemsTableBody');
    const btnAddRow = document.getElementById('btnAddPoItemRow');
    const totalAmountEl = document.getElementById('poTotalAmountDisplay');
    const inputPaidAmount = document.getElementById('poPaidAmountInput');
    const debtBalanceEl = document.getElementById('poDebtBalanceDisplay');

    if (!tableBody || !btnAddRow) return;

    let rowIndex = tableBody.querySelectorAll('tr').length;

    function recalculatePOTotals() {
        let total = 0;
        tableBody.querySelectorAll('tr').forEach(tr => {
            const qtyInput = tr.querySelector('.item-qty');
            const costInput = tr.querySelector('.item-cost');
            const rowTotalEl = tr.querySelector('.item-total');

            const qty = parseFloat(qtyInput ? qtyInput.value : 0) || 0;
            const cost = parseFloat(costInput ? costInput.value : 0) || 0;
            const rowTotal = qty * cost;

            if (rowTotalEl) {
                rowTotalEl.textContent = new Intl.NumberFormat('vi-VN').format(rowTotal) + ' đ';
            }
            total += rowTotal;
        });

        if (totalAmountEl) {
            totalAmountEl.textContent = new Intl.NumberFormat('vi-VN').format(total) + ' đ';
        }

        const paid = parseFloat(inputPaidAmount ? inputPaidAmount.value : 0) || 0;
        const debt = Math.max(0, total - paid);
        if (debtBalanceEl) {
            debtBalanceEl.textContent = new Intl.NumberFormat('vi-VN').format(debt) + ' đ';
        }
    }

    btnAddRow.addEventListener('click', function () {
        const variantOptions = window.poVariantOptionsHtml || '';
        const tr = document.createElement('tr');
        tr.innerHTML = `
            <td>
                <select name="Items[${rowIndex}].ProductVariantId" class="form-select item-variant-select" required>
                    <option value="">-- Chọn biến thể giày (Size / Màu) --</option>
                    ${variantOptions}
                </select>
            </td>
            <td style="width: 140px;">
                <input type="number" name="Items[${rowIndex}].Quantity" class="form-control item-qty" min="1" value="10" required />
            </td>
            <td style="width: 200px;">
                <input type="number" name="Items[${rowIndex}].UnitCost" class="form-control item-cost" min="0" step="1000" value="0" required />
            </td>
            <td class="text-end fw-bold item-total" style="width: 180px;">0 đ</td>
            <td class="text-center" style="width: 60px;">
                <button type="button" class="btn btn-sm btn-outline-danger btn-remove-row">
                    <i class="bi bi-trash"></i>
                </button>
            </td>
        `;

        tableBody.appendChild(tr);
        rowIndex++;
        attachRowEvents(tr);
        recalculatePOTotals();
    });

    function attachRowEvents(tr) {
        const select = tr.querySelector('.item-variant-select');
        const qty = tr.querySelector('.item-qty');
        const cost = tr.querySelector('.item-cost');
        const btnRemove = tr.querySelector('.btn-remove-row');

        if (select) {
            select.addEventListener('change', function () {
                const selectedOption = select.options[select.selectedIndex];
                const suggestedCost = selectedOption.getAttribute('data-cost');
                if (suggestedCost && cost) {
                    cost.value = suggestedCost;
                }
                recalculatePOTotals();
            });
        }

        if (qty) qty.addEventListener('input', recalculatePOTotals);
        if (cost) cost.addEventListener('input', recalculatePOTotals);
        if (btnRemove) {
            btnRemove.addEventListener('click', function () {
                if (tableBody.querySelectorAll('tr').length > 1) {
                    tr.remove();
                    recalculatePOTotals();
                } else {
                    alert('Phiếu nhập phải có ít nhất một mặt hàng.');
                }
            });
        }
    }

    // Attach to existing rows
    tableBody.querySelectorAll('tr').forEach(attachRowEvents);

    if (inputPaidAmount) {
        inputPaidAmount.addEventListener('input', recalculatePOTotals);
    }

    recalculatePOTotals();
}

// ==========================================
// 3. RMA (EXCHANGE / REFUND) FORM HELPER
// ==========================================
function initRmaForm() {
    const orderSelect = document.getElementById('rmaOrderSelect');
    const originalVariantSelect = document.getElementById('rmaOriginalVariantSelect');
    const rmaTypeSelect = document.getElementById('rmaTypeSelect');
    const exchangeVariantWrap = document.getElementById('rmaExchangeVariantWrap');
    const refundAmountWrap = document.getElementById('rmaRefundAmountWrap');
    const refundAmountInput = document.getElementById('rmaRefundAmountInput');

    if (!orderSelect || !originalVariantSelect) return;

    orderSelect.addEventListener('change', function () {
        const orderId = orderSelect.value;
        if (!orderId) {
            originalVariantSelect.innerHTML = '<option value="">-- Chọn đơn hàng trước --</option>';
            return;
        }

        originalVariantSelect.innerHTML = '<option value="">Đang tải sản phẩm trong đơn...</option>';

        fetch(`/Admin/GetOrderItemsForReturn?orderId=${orderId}`)
            .then(res => res.json())
            .then(items => {
                if (!items || items.length === 0) {
                    originalVariantSelect.innerHTML = '<option value="">Không có sản phẩm trong đơn này</option>';
                    return;
                }

                let html = '<option value="">-- Chọn sản phẩm trong đơn cần đổi/trả --</option>';
                items.forEach(item => {
                    html += `<option value="${item.productVariantId}" data-price="${item.unitPrice}" data-qty="${item.quantity}">
                        ${item.productName} (Size: ${item.size}, Màu: ${item.color}) - SL mua: ${item.quantity} - Giá: ${new Intl.NumberFormat('vi-VN').format(item.unitPrice)} đ
                    </option>`;
                });
                originalVariantSelect.innerHTML = html;
            })
            .catch(err => {
                console.error(err);
                originalVariantSelect.innerHTML = '<option value="">Lỗi tải dữ liệu sản phẩm</option>';
            });
    });

    if (rmaTypeSelect) {
        rmaTypeSelect.addEventListener('change', function () {
            const isExchange = rmaTypeSelect.value === 'EXCHANGE';
            if (exchangeVariantWrap) {
                exchangeVariantWrap.style.display = isExchange ? 'block' : 'none';
            }
            if (refundAmountWrap) {
                refundAmountWrap.style.display = isExchange ? 'none' : 'block';
            }
        });
    }

    if (originalVariantSelect && refundAmountInput) {
        originalVariantSelect.addEventListener('change', function () {
            const opt = originalVariantSelect.options[originalVariantSelect.selectedIndex];
            const price = opt ? opt.getAttribute('data-price') : 0;
            if (price) {
                refundAmountInput.value = price;
            }
        });
    }
}

// ==========================================
// 4. SUPPLIER DEBT PAYMENT HELPER
// ==========================================
function initSupplierPaymentHelper() {
    const supplierSelect = document.getElementById('paymentSupplierSelect');
    const poSelect = document.getElementById('paymentPoSelect');
    const amountInput = document.getElementById('paymentAmountInput');

    if (!supplierSelect || !poSelect) return;

    supplierSelect.addEventListener('change', function () {
        const supplierId = supplierSelect.value;
        if (!supplierId) {
            poSelect.innerHTML = '<option value="">-- Trả chung vào tổng nợ (không chỉ định PO) --</option>';
            return;
        }

        fetch(`/Admin/GetSupplierUnpaidPOs?supplierId=${supplierId}`)
            .then(res => res.json())
            .then(pos => {
                let html = '<option value="">-- Trả chung vào tổng nợ (không chỉ định PO) --</option>';
                if (pos && pos.length > 0) {
                    pos.forEach(po => {
                        html += `<option value="${po.id}" data-remaining="${po.remaining}">
                            ${po.poCode} - Tổng: ${new Intl.NumberFormat('vi-VN').format(po.totalAmount)} đ (Còn nợ: ${new Intl.NumberFormat('vi-VN').format(po.remaining)} đ)
                        </option>`;
                    });
                }
                poSelect.innerHTML = html;
            });
    });

    poSelect.addEventListener('change', function () {
        const opt = poSelect.options[poSelect.selectedIndex];
        const remaining = opt ? opt.getAttribute('data-remaining') : null;
        if (remaining && amountInput && (!amountInput.value || parseFloat(amountInput.value) === 0)) {
            amountInput.value = remaining;
        }
    });
}

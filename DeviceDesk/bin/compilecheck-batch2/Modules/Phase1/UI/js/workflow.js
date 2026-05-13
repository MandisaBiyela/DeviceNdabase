(() => {
    const API_BASE = `${location.origin}/api/phase1/receiving`;
    
    let currentStep = 1;
    let selectedOrder = null;
    let batchId = null;
    let scannedCount = 0;
    let expectedCount = 0;
    let orderLines = [];

    // Elements
    const orderSelect = document.getElementById('orderSelect');
    const orderDetailsBox = document.getElementById('orderDetailsBox');
    const orderDetailsContent = document.getElementById('orderDetailsContent');
    const proceedToVerificationBtn = document.getElementById('proceedToVerificationBtn');
    const invoiceUploadArea = document.getElementById('invoiceUploadArea');
    const invoiceFileInput = document.getElementById('invoiceFileInput');
    const selectedFilesLabel = document.getElementById('selectedFilesLabel');
    const scanInput = document.getElementById('scanInput');
    const scannedCountDisplay = document.getElementById('scannedCount');
    const completeCountBtn = document.getElementById('completeCountBtn');
    const varianceCheckbox = document.getElementById('varianceCheckbox');
    const proceedToGRVBtn = document.getElementById('proceedToGRVBtn');
    const acceptVarianceBtn = document.getElementById('acceptVarianceBtn');
    const startNewProcessBtn = document.getElementById('startNewProcessBtn');
    const downloadGRVBtn = document.getElementById('downloadGRVBtn');

    // Initialize
    init();

    async function init() {
        await loadOrders();
        setupEventListeners();
        showStep(1);
    }

    function setupEventListeners() {
        orderSelect.addEventListener('change', handleOrderSelection);
        proceedToVerificationBtn.addEventListener('click', () => proceedToStep2());
        if (invoiceUploadArea && invoiceFileInput) {
            invoiceUploadArea.addEventListener('click', () => invoiceFileInput.click());
            invoiceUploadArea.addEventListener('dragover', e => { e.preventDefault(); invoiceUploadArea.classList.add('dragover'); });
            invoiceUploadArea.addEventListener('dragleave', () => invoiceUploadArea.classList.remove('dragover'));
            invoiceUploadArea.addEventListener('drop', e => { e.preventDefault(); invoiceFileInput.files = e.dataTransfer.files; updateSelectedFilesLabel(); invoiceUploadArea.classList.remove('dragover'); });
            invoiceFileInput.addEventListener('change', updateSelectedFilesLabel);
        }
        scanInput.addEventListener('keypress', handleScan);
        completeCountBtn.addEventListener('click', completeCount);
        proceedToGRVBtn.addEventListener('click', () => proceedToStep4());
        acceptVarianceBtn.addEventListener('click', () => proceedToStep4());
        startNewProcessBtn.addEventListener('click', resetWorkflow);
        downloadGRVBtn.addEventListener('click', downloadGRV);
    }

    function updateSelectedFilesLabel() {
        const names = Array.from(invoiceFileInput.files || []).map(f => f.name);
        selectedFilesLabel.textContent = names.length ? names.join(', ') : '';
    }

    async function loadOrders() {
        try {
            // Fetch orders from Phase 0 NEW stock uploads
            const res = await fetch(`${location.origin}/api/phase0/new/orders`);
            if (!res.ok) throw new Error('Failed to load orders');
            const orders = await res.json();
            
            orderSelect.innerHTML = '<option value="">-- Select Order --</option>';
            orders.forEach(order => {
                const opt = document.createElement('option');
                opt.value = order.orderId;
                opt.textContent = `${order.orderNumber} - ${order.supplierName} (${order.totalDevices} devices)`;
                opt.dataset.order = JSON.stringify(order);
                orderSelect.appendChild(opt);
            });
        } catch (err) {
            console.error('Error loading orders:', err);
            alert('Failed to load orders. Please refresh the page.');
        }
    }

    function handleOrderSelection() {
        const selectedOption = orderSelect.options[orderSelect.selectedIndex];
        if (!selectedOption.value) {
            orderDetailsBox.style.display = 'none';
            proceedToVerificationBtn.disabled = true;
            return;
        }

        selectedOrder = JSON.parse(selectedOption.dataset.order);
        orderLines = selectedOrder.devices || [];
        expectedCount = selectedOrder.totalDevices || 0;

        // Display order details
        orderDetailsContent.innerHTML = `
            <p><strong>Order Number:</strong> ${selectedOrder.orderNumber}</p>
            <p><strong>Supplier:</strong> ${selectedOrder.supplierName}</p>
            <p><strong>Total Devices:</strong> ${expectedCount}</p>
            <p><strong>File:</strong> ${selectedOrder.fileName || 'N/A'}</p>
            <p><strong>Created:</strong> ${new Date(selectedOrder.createdAt).toLocaleDateString()}</p>
        `;
        orderDetailsBox.style.display = 'block';
        proceedToVerificationBtn.disabled = false;
    }

    async function proceedToStep2() {
        try {
            // Create receiving batch
            const payload = {
                sourceType: 1, // New Stock
                orderId: selectedOrder.orderId,
                collectionSlipId: null,
                schoolId: null,
                receivedBy: 'System User',
                notes: 'Created via workflow'
            };

            const res = await fetch(`${API_BASE}/batches`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            if (!res.ok) throw new Error('Failed to create batch');
            const data = await res.json();
            batchId = data.receivingBatchId;

            // Populate blind copy
            document.getElementById('blindInvoiceRef').textContent = selectedOrder.orderNumber;
            const blindCopyItems = document.getElementById('blindCopyItems');
            blindCopyItems.innerHTML = '';
            
            orderLines.forEach(line => {
                const item = document.createElement('div');
                item.className = 'blind-copy-item';
                item.innerHTML = `
                    <div>
                        <strong>${line.brand} ${line.model}</strong>
                    </div>
                    <div class="text-muted small">[Qty: Hidden]</div>
                `;
                blindCopyItems.appendChild(item);
            });

            showStep(2);
        } catch (err) {
            console.error('Error:', err);
            alert('Failed to create batch: ' + err.message);
        }
    }

    function handleScan(e) {
        if (e.key === 'Enter') {
            e.preventDefault();
            const value = scanInput.value.trim();
            if (value) {
                scannedCount++;
                scannedCountDisplay.textContent = scannedCount;
                scanInput.value = '';
                scanInput.focus();
            }
        }
    }

    function completeCount() {
        // Check if variance checkbox is checked
        if (varianceCheckbox.checked) {
            // Simulate variance by reducing count
            scannedCount = Math.max(0, expectedCount - 2);
            scannedCountDisplay.textContent = scannedCount;
        } else {
            // Match expected count
            scannedCount = expectedCount;
            scannedCountDisplay.textContent = scannedCount;
        }

        setTimeout(() => {
            proceedToStep3();
        }, 500);
    }

    function proceedToStep3() {
        const tbody = document.getElementById('reconciliationTableBody');
        tbody.innerHTML = '';

        let hasVariance = false;

        orderLines.forEach(line => {
            const invoiceQty = line.quantityOrdered;
            const countedQty = Math.floor(scannedCount / orderLines.length); // Distribute count
            const variance = countedQty - invoiceQty;

            if (variance !== 0) hasVariance = true;

            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${line.brand} ${line.model}</td>
                <td class="text-center">${invoiceQty}</td>
                <td class="text-center">${countedQty}</td>
                <td class="text-center">
                    <span class="variance-badge ${variance === 0 ? 'variance-match' : 'variance-mismatch'}">
                        ${variance === 0 ? '0' : (variance > 0 ? '+' + variance : variance)}
                    </span>
                </td>
            `;
            tbody.appendChild(row);
        });

        if (hasVariance) {
            document.getElementById('matchResult').style.display = 'none';
            document.getElementById('varianceResult').style.display = 'block';
        } else {
            document.getElementById('matchResult').style.display = 'block';
            document.getElementById('varianceResult').style.display = 'none';
        }

        showStep(3);
    }

    async function proceedToStep4() {
        try {
            // Generate GRV
            const grvNumber = `GRV-${new Date().getFullYear()}-${String(Math.floor(Math.random() * 1000)).padStart(3, '0')}`;
            document.getElementById('grvNumber').textContent = grvNumber;
            
            showStep(4);
        } catch (err) {
            console.error('Error:', err);
            alert('Failed to generate GRV: ' + err.message);
        }
    }

    function downloadGRV() {
        if (batchId) {
            window.open(`${API_BASE}/batches/${batchId}/blind-copy`, '_blank');
        }
    }

    function resetWorkflow() {
        scannedCount = 0;
        batchId = null;
        selectedOrder = null;
        orderSelect.value = '';
        orderDetailsBox.style.display = 'none';
        proceedToVerificationBtn.disabled = true;
        scannedCountDisplay.textContent = '0';
        varianceCheckbox.checked = false;
        showStep(1);
    }

    function showStep(step) {
        currentStep = step;
        
        // Hide all content
        document.querySelectorAll('[id^="step"][id$="-content"]').forEach(el => {
            el.style.display = 'none';
        });
        
        // Show current step content
        document.getElementById(`step${step}-content`).style.display = 'block';
        
        // Update step indicators
        for (let i = 1; i <= 4; i++) {
            const indicator = document.getElementById(`step${i}-indicator`);
            indicator.classList.remove('active', 'completed');
            
            if (i < step) {
                indicator.classList.add('completed');
            } else if (i === step) {
                indicator.classList.add('active');
            }
        }

        // Focus scan input when on step 2
        if (step === 2) {
            setTimeout(() => scanInput.focus(), 100);
        }
    }
})();

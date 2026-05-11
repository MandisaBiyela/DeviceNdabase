(() => {
    const API_BASE = `${location.origin}/api/phase1/newstock`;
    let currentBatch = null;
    let scannedDevices = [];

    // Load pending batches on page load
    loadPendingBatches();

    // Scan button
    document.getElementById('scanBtn').addEventListener('click', scanDevice);

    // Serial input - scan on Enter
    document.getElementById('serialInput').addEventListener('keypress', (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            scanDevice();
        }
    });

    // Confirm batch button
    document.getElementById('confirmBatchBtn').addEventListener('click', confirmBatch);

    async function loadPendingBatches() {
        try {
            const response = await fetch(`${API_BASE}/pending`);
            if (!response.ok) throw new Error('Failed to load pending batches');

            const batches = await response.json();
            renderPendingBatches(batches);
        } catch (error) {
            console.error('Error loading pending batches:', error);
            document.getElementById('pendingBatchesContainer').innerHTML = `
                <div class="col-12">
                    <div class="alert alert-danger">
                        Error loading batches: ${error.message}
                    </div>
                </div>
            `;
        }
    }

    function renderPendingBatches(batches) {
        const container = document.getElementById('pendingBatchesContainer');

        if (batches.length === 0) {
            container.innerHTML = `
                <div class="col-12">
                    <div class="alert alert-info">
                        <i class="bi bi-info-circle"></i> No pending batches found.
                        All batches have been scanned or there are no batches created in Phase 0.
                    </div>
                </div>
            `;
            return;
        }

        container.innerHTML = batches.map(batch => `
            <div class="col-md-6 col-lg-4 mb-3">
                <div class="card h-100">
                    <div class="card-body">
                        <h5 class="card-title">${batch.batchNumber}</h5>
                        ${batch.invoiceNumber ? `<h6 class="text-primary">Order: ${batch.invoiceNumber}</h6>` : ''}
                        <p class="card-text">
                            <strong>Supplier:</strong> ${batch.supplierName || 'N/A'}<br>
                            ${batch.invoiceNumber ? `<strong>Order Number:</strong> ${batch.invoiceNumber}<br>` : ''}
                            <strong>Expected:</strong> <span class="badge bg-primary">${batch.totalQuantityExpected}</span><br>
                            <strong>Scanned:</strong> <span class="badge bg-info">${batch.totalQuantityScanned}</span><br>
                            <strong>Status:</strong> ${getStatusBadge(batch.status)}
                        </p>
                        <button class="btn btn-primary w-100" onclick="startScanning('${batch.batchId}')">
                            <i class="bi bi-upc-scan"></i> Start Scanning
                        </button>
                    </div>
                    <div class="card-footer text-muted">
                        <small>Created: ${new Date(batch.createdAt).toLocaleDateString()}</small>
                    </div>
                </div>
            </div>
        `).join('');
    }

    window.startScanning = async function(batchId) {
        try {
            const response = await fetch(`${API_BASE}/batches/${batchId}`);
            if (!response.ok) throw new Error('Failed to load batch details');

            currentBatch = await response.json();
            scannedDevices = currentBatch.scannedDevices || [];

            // Switch views
            document.getElementById('pendingBatchesView').style.display = 'none';
            document.getElementById('scanningView').style.display = 'block';

            // Render batch details
            renderScanningView();

            // Focus on serial input
            document.getElementById('serialInput').focus();
        } catch (error) {
            console.error('Error loading batch:', error);
            showAlert('Error: ' + error.message, 'danger');
        }
    };

    function renderScanningView() {
        // Update header
        const batchTitle = currentBatch.invoiceNumber 
            ? `${currentBatch.batchNumber} - Order ${currentBatch.invoiceNumber}`
            : currentBatch.batchNumber;
        document.getElementById('batchNumberDisplay').textContent = batchTitle;
        document.getElementById('supplierDisplay').textContent = 
            `${currentBatch.supplierName || 'N/A'}`;

        // Update progress
        updateProgress();

        // Render blind copy
        renderBlindCopy();

        // Render scanned devices
        renderScannedDevices();
    }

    function renderBlindCopy() {
        const container = document.getElementById('blindCopyContainer');
        
        if (!currentBatch.items || currentBatch.items.length === 0) {
            container.innerHTML = '<p class="text-muted">No items</p>';
            return;
        }

        container.innerHTML = currentBatch.items.map(item => `
            <div class="blind-copy-item">
                <div class="d-flex justify-content-between align-items-start">
                    <div>
                        <strong>${item.brand || 'Unknown'} ${item.model || ''}</strong><br>
                        <small class="text-muted">${item.deviceType || 'Device'}</small><br>
                        ${item.description ? `<small>${item.description}</small>` : ''}
                    </div>
                    <div class="text-end">
                        <span class="badge bg-primary">${item.quantityExpected}</span>
                    </div>
                </div>
            </div>
        `).join('');
    }

    function renderScannedDevices() {
        const container = document.getElementById('scannedDevicesContainer');
        document.getElementById('scannedCount').textContent = scannedDevices.length;

        if (scannedDevices.length === 0) {
            container.innerHTML = '<p class="text-muted text-center">No devices scanned yet</p>';
            return;
        }

        container.innerHTML = scannedDevices
            .sort((a, b) => new Date(b.scannedAt) - new Date(a.scannedAt))
            .map(device => `
                <div class="scanned-device ${device.isDuplicate ? 'duplicate' : ''}">
                    <div class="d-flex justify-content-between align-items-center">
                        <div>
                            <strong>${device.serialNumber}</strong>
                            ${device.brand || device.model ? `<br><small class="text-muted">${device.brand || ''} ${device.model || ''}</small>` : ''}
                            <br><small class="text-muted">${new Date(device.scannedAt).toLocaleString()}</small>
                        </div>
                        <button class="btn btn-sm btn-outline-danger" onclick="deleteScan('${device.scanId}')">
                            <i class="bi bi-trash"></i>
                        </button>
                    </div>
                </div>
            `).join('');
    }

    async function scanDevice() {
        const serialInput = document.getElementById('serialInput');
        const serialNumber = serialInput.value.trim();

        if (!serialNumber) {
            showAlert('Please enter a serial number', 'warning');
            return;
        }

        const scannedBy = getCookie('user_email') || 'officer@example.com';

        try {
            const response = await fetch(`${API_BASE}/batches/${currentBatch.batchId}/scan`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    serialNumber: serialNumber,
                    imei: null,
                    brand: null,
                    model: null,
                    scannedBy: scannedBy
                })
            });

            const result = await response.json();

            if (!result.success) {
                showAlert(result.message, 'danger');
                serialInput.value = '';
                serialInput.focus();
                return;
            }

            // Add to scanned devices
            scannedDevices.push({
                scanId: Date.now(), // Temporary ID
                serialNumber: serialNumber,
                scannedAt: new Date().toISOString(),
                scannedBy: scannedBy,
                isDuplicate: false
            });

            // Update batch status
            currentBatch.totalQuantityScanned = result.totalScanned;
            currentBatch.status = result.status;

            // Update UI
            updateProgress();
            renderScannedDevices();

            // Clear input
            serialInput.value = '';
            serialInput.focus();

            // Show success feedback
            showAlert(result.message, 'success');

            // Show confirm button if ready
            if (result.status === 2) { // ReadyToConfirm
                document.getElementById('confirmBatchBtn').style.display = 'block';
            }
        } catch (error) {
            console.error('Error scanning device:', error);
            showAlert('Error: ' + error.message, 'danger');
        }
    }

    window.deleteScan = async function(scanId) {
        if (!confirm('Are you sure you want to delete this scan?')) return;

        try {
            const response = await fetch(`${API_BASE}/scans/${scanId}`, {
                method: 'DELETE'
            });

            if (!response.ok) throw new Error('Failed to delete scan');

            // Remove from local array
            scannedDevices = scannedDevices.filter(d => d.scanId !== scanId);
            currentBatch.totalQuantityScanned--;

            // Update UI
            updateProgress();
            renderScannedDevices();

            showAlert('Scan deleted successfully', 'success');
        } catch (error) {
            console.error('Error deleting scan:', error);
            showAlert('Error: ' + error.message, 'danger');
        }
    };

    async function confirmBatch() {
        if (!confirm('Are you sure you want to confirm this batch and generate GRV?')) return;

        const confirmedBy = getCookie('user_email') || 'officer@example.com';

        try {
            const response = await fetch(`${API_BASE}/batches/${currentBatch.batchId}/confirm`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    confirmedBy: confirmedBy,
                    notes: null
                })
            });

            if (!response.ok) throw new Error('Failed to confirm batch');

            const result = await response.json();

            showAlert(`Success! GRV ${result.grvNumber} generated for ${result.totalDevices} devices.`, 'success');

            // Wait a moment then go back to pending batches
            setTimeout(() => {
                backToPendingBatches();
            }, 2000);
        } catch (error) {
            console.error('Error confirming batch:', error);
            showAlert('Error: ' + error.message, 'danger');
        }
    }

    function updateProgress() {
        const scanned = currentBatch.totalQuantityScanned;
        const expected = currentBatch.totalQuantityExpected;
        const percentage = expected > 0 ? (scanned / expected) * 100 : 0;

        document.getElementById('progressDisplay').textContent = `${scanned}/${expected}`;
        document.getElementById('progressBar').style.width = `${percentage}%`;

        // Update progress bar color based on status
        const progressBar = document.getElementById('progressBar');
        progressBar.className = 'progress-bar';
        
        if (scanned === expected) {
            progressBar.classList.add('bg-success');
        } else if (scanned > expected) {
            progressBar.classList.add('bg-danger');
        } else {
            progressBar.classList.add('bg-info');
        }
    }

    window.backToPendingBatches = function() {
        document.getElementById('scanningView').style.display = 'none';
        document.getElementById('pendingBatchesView').style.display = 'block';
        currentBatch = null;
        scannedDevices = [];
        loadPendingBatches();
    };

    function getStatusBadge(status) {
        const statusMap = {
            0: { text: 'Pending Scan', class: 'status-pending' },
            1: { text: 'Scanning', class: 'status-scanning' },
            2: { text: 'Ready', class: 'status-ready' },
            3: { text: 'Mismatch', class: 'status-mismatch' },
            4: { text: 'Completed', class: 'status-completed' }
        };

        const statusInfo = statusMap[status] || { text: 'Unknown', class: 'status-pending' };
        return `<span class="status-badge ${statusInfo.class}">${statusInfo.text}</span>`;
    }

    function showAlert(message, type) {
        const alertHtml = `
            <div class="alert alert-${type} alert-dismissible fade show" role="alert">
                ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        `;
        document.getElementById('alertContainer').innerHTML = alertHtml;

        // Auto-dismiss after 3 seconds
        setTimeout(() => {
            const alert = document.querySelector('.alert');
            if (alert) {
                bootstrap.Alert.getInstance(alert)?.close();
            }
        }, 3000);
    }

    function getCookie(name) {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
        return null;
    }
})();

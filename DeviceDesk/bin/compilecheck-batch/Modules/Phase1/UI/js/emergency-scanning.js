(() => {
    const API_BASE = `${location.origin}/api/phase1/receiving`;
    
    let batchId = null;
    let loanId = null;
    let batchData = null;
    let loanData = null;
    let scannedDevices = [];

    // Get parameters from URL
    const urlParams = new URLSearchParams(window.location.search);
    batchId = urlParams.get('batchId');
    loanId = urlParams.get('loanId');

    if (!batchId) {
        alert('No batch ID provided. Redirecting to dashboard.');
        window.location.href = '/phase1/dashboard.html';
        return;
    }

    // Elements
    const deviceScanInput = document.getElementById('deviceScanInput');
    const scanBtn = document.getElementById('scanBtn');
    const scannedCountDisplay = document.getElementById('scannedCount');
    const deviceList = document.getElementById('deviceList');
    const emergencyDetails = document.getElementById('emergencyDetails');
    const loanDetails = document.getElementById('loanDetails');
    const completeScanBtn = document.getElementById('completeScanBtn');

    // Initialize
    init();

    async function init() {
        await loadBatchData();
        await loadLoanData();
        setupEventListeners();
        deviceScanInput.focus();
    }

    function setupEventListeners() {
        // Scan input - Enter key
        deviceScanInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                scanDevice();
            }
        });

        // Scan button
        scanBtn.addEventListener('click', scanDevice);

        // Complete scanning
        completeScanBtn.addEventListener('click', completeScanning);
    }

    async function loadBatchData() {
        try {
            const response = await fetch(`${API_BASE}/batches/${batchId}`);
            if (!response.ok) throw new Error('Failed to load batch data');
            
            batchData = await response.json();
            displayEmergencyDetails();
        } catch (error) {
            console.error('Error loading batch data:', error);
            showAlert('danger', 'Failed to load batch data: ' + error.message);
        }
    }

    async function loadLoanData() {
        if (!loanId) return;
        
        try {
            const response = await fetch(`${API_BASE}/emergency/loan-assignment/${loanId}`);
            if (!response.ok) throw new Error('Failed to load loan data');
            
            loanData = await response.json();
            displayLoanDetails();
        } catch (error) {
            console.error('Error loading loan data:', error);
            showAlert('warning', 'Failed to load loan assignment data: ' + error.message);
        }
    }

    function displayEmergencyDetails() {
        emergencyDetails.innerHTML = `
            <p class="mb-1"><strong>Batch ID:</strong> ${batchData.receivingBatchId}</p>
            <p class="mb-1"><strong>School:</strong> ${batchData.collectionSlip?.schoolName || 'N/A'}</p>
            <p class="mb-1"><strong>Priority:</strong> <span class="badge bg-danger">EMERGENCY</span></p>
            <p class="mb-0"><strong>Created:</strong> ${new Date(batchData.createdAt).toLocaleString()}</p>
        `;
    }

    function displayLoanDetails() {
        if (!loanData) {
            loanDetails.innerHTML = `
                <div class="alert alert-warning mb-0">
                    <i class="bi bi-exclamation-triangle"></i> 
                    Loan assignment data not available.
                </div>
            `;
            return;
        }

        loanDetails.innerHTML = `
            <p class="mb-1"><strong>Loan Unit:</strong> ${loanData.loanUnit?.brand} ${loanData.loanUnit?.model}</p>
            <p class="mb-1"><strong>Serial:</strong> ${loanData.loanUnit?.serialNumber}</p>
            <p class="mb-1"><strong>Assigned To:</strong> ${loanData.replacementUser}</p>
            <p class="mb-1"><strong>Reason:</strong> ${loanData.emergencyReason}</p>
            <p class="mb-0"><strong>Expected Return:</strong> ${new Date(loanData.expectedReturnDate).toLocaleDateString()}</p>
        `;
    }

    async function scanDevice() {
        const serialNumber = deviceScanInput.value.trim();
        
        if (!serialNumber) {
            showAlert('warning', 'Please enter a device serial number or barcode.');
            deviceScanInput.focus();
            return;
        }

        // Check for duplicates
        if (scannedDevices.some(d => d.serialNumber === serialNumber)) {
            showAlert('warning', 'This device has already been scanned in this batch.');
            deviceScanInput.value = '';
            deviceScanInput.focus();
            return;
        }

        try {
            // Scan device for Emergency R&R
            const response = await fetch(`${API_BASE}/emergency/scan-device`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    receivingBatchId: batchId,
                    loanAssignmentId: loanId,
                    serialNumber: serialNumber,
                    scannedBy: 'Receiving Officer'
                })
            });

            const result = await response.json();

            if (response.ok) {
                // Device validation successful
                const deviceData = {
                    serialNumber: serialNumber,
                    deviceInfo: `${result.brand || 'Unknown'} ${result.model || 'Device'}`,
                    loanUnitLink: result.loanUnitSerial || 'Not Linked',
                    status: 'Emergency R&R Received',
                    scannedAt: new Date().toLocaleString(),
                    validationResult: 'success',
                    issues: result.issues || []
                };

                scannedDevices.push(deviceData);
                updateDeviceList();
                updateScannedCount();
                
                showAlert('success', `Faulty device ${serialNumber} successfully received and linked to loan unit`);
            } else {
                // Device validation failed but still add to list for review
                const deviceData = {
                    serialNumber: serialNumber,
                    deviceInfo: 'Validation Failed',
                    loanUnitLink: 'Link Failed',
                    status: result.status || 'Validation Error',
                    scannedAt: new Date().toLocaleString(),
                    validationResult: 'failed',
                    issues: result.issues || [result.message || 'Unknown error']
                };

                scannedDevices.push(deviceData);
                updateDeviceList();
                updateScannedCount();
                
                showAlert('danger', result.message || 'Device validation failed. Added to list for review.');
            }

            // Clear input and refocus
            deviceScanInput.value = '';
            deviceScanInput.focus();

        } catch (error) {
            console.error('Error scanning device:', error);
            showAlert('danger', 'Error scanning device: ' + error.message);
        }
    }

    function updateDeviceList() {
        if (scannedDevices.length === 0) {
            deviceList.innerHTML = `
                <tr>
                    <td colspan="7" class="text-center text-muted py-4">
                        No faulty devices scanned yet. Scan the device that was replaced by the loan unit.
                    </td>
                </tr>
            `;
            return;
        }

        deviceList.innerHTML = scannedDevices.map((device, index) => {
            let statusClass = 'status-emergency';
            let rowClass = '';
            
            if (device.validationResult === 'failed') {
                statusClass = 'status-error';
                rowClass = 'table-danger';
            } else if (device.validationResult === 'success') {
                statusClass = 'status-validated';
            }

            return `
                <tr class="${rowClass}">
                    <td>${index + 1}</td>
                    <td><code>${device.serialNumber}</code></td>
                    <td>${device.deviceInfo}</td>
                    <td>
                        <small class="text-muted">
                            ${device.loanUnitLink !== 'Not Linked' ? 
                                `<i class="bi bi-link text-success"></i> ${device.loanUnitLink}` : 
                                `<i class="bi bi-x-circle text-danger"></i> Not Linked`
                            }
                        </small>
                    </td>
                    <td>
                        <span class="device-status ${statusClass}">
                            ${device.status}
                        </span>
                    </td>
                    <td><small>${device.scannedAt}</small></td>
                    <td>
                        <button class="btn btn-sm btn-outline-danger" onclick="removeDevice(${index})" title="Remove device">
                            <i class="bi bi-trash"></i>
                        </button>
                        ${device.issues.length > 0 ? `<button class="btn btn-sm btn-outline-warning" onclick="showIssues(${index})" title="View issues"><i class="bi bi-exclamation-triangle"></i></button>` : ''}
                    </td>
                </tr>
            `;
        }).join('');
    }

    function updateScannedCount() {
        scannedCountDisplay.textContent = scannedDevices.length;
    }

    async function completeScanning() {
        if (scannedDevices.length === 0) {
            showAlert('warning', 'No devices have been scanned. Please scan the faulty device before completing.');
            return;
        }

        const failedDevices = scannedDevices.filter(d => d.validationResult === 'failed');
        if (failedDevices.length > 0) {
            const confirmMsg = `${failedDevices.length} device(s) have validation issues. Continue to verification anyway?`;
            if (!confirm(confirmMsg)) {
                return;
            }
        }

        if (confirm(`Complete emergency scanning with ${scannedDevices.length} device(s) and proceed to verification?`)) {
            try {
                const response = await fetch(`${API_BASE}/emergency/complete-scanning`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        receivingBatchId: batchId,
                        loanAssignmentId: loanId,
                        scannedDevices: scannedDevices,
                        actualCount: scannedDevices.length,
                        completedBy: 'Receiving Officer'
                    })
                });

                if (response.ok) {
                    showAlert('success', 'Emergency scanning completed successfully! Proceeding to verification...');
                    
                    // Redirect to Emergency verification page (Step 3)
                    setTimeout(() => {
                        window.location.href = `/phase1/emergency-verification.html?batchId=${batchId}&loanId=${loanId}`;
                    }, 2000);
                } else {
                    const error = await response.json();
                    showAlert('danger', 'Failed to complete scanning: ' + (error.message || 'Unknown error'));
                }
            } catch (error) {
                console.error('Error completing scanning:', error);
                showAlert('danger', 'Error completing scanning: ' + error.message);
            }
        }
    }

    function removeDevice(index) {
        if (confirm('Remove this device from the scanned list?')) {
            scannedDevices.splice(index, 1);
            updateDeviceList();
            updateScannedCount();
            showAlert('info', 'Device removed from list.');
        }
    }

    function showIssues(index) {
        const device = scannedDevices[index];
        const issuesList = device.issues.join('\n• ');
        alert(`Issues with device ${device.serialNumber}:\n\n• ${issuesList}`);
    }

    function showAlert(type, message) {
        const alertDiv = document.createElement('div');
        alertDiv.className = `alert alert-${type} alert-dismissible fade show`;
        alertDiv.innerHTML = `
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;
        
        document.querySelector('.container').insertBefore(alertDiv, document.querySelector('.container').firstChild);
        
        setTimeout(() => alertDiv.remove(), 5000);
    }

    // Expose functions globally
    window.removeDevice = removeDevice;
    window.showIssues = showIssues;
})();

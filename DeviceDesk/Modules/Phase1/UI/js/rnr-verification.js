(() => {
    const RECEIVING_API = `${location.origin}/api/phase1/receiving`;
    const RNR_API = `${location.origin}/api/phase1/rnr`;
    const RECON_API = `${location.origin}/api/phase1/reconciliation`;
    
    let batchId = null;
    let batchData = null;
    let verificationData = null;

    // Get batch ID from URL
    const urlParams = new URLSearchParams(window.location.search);
    batchId = urlParams.get('batchId');

    if (!batchId || batchId === '00000000-0000-0000-0000-000000000000') {
        window.toast && toast('No batch ID provided. Redirecting to dashboard.', 'danger');
        window.location.href = '/phase1/dashboard.html';
        return;
    }

    // Initialize
    init();

    async function init() {
        await loadBatchData();
        await performVerification();
        setupEventListeners();
    }

    function setupEventListeners() {
        document.getElementById('proceedToGRVBtn')?.addEventListener('click', proceedToGRV);
        document.getElementById('proceedWithIssuesBtn')?.addEventListener('click', proceedToGRV);
        document.getElementById('requestRecountBtn')?.addEventListener('click', requestRecount);
        document.getElementById('supervisorApprovalBtn')?.addEventListener('click', requestSupervisorApproval);
        document.getElementById('resolveVarianceBtn')?.addEventListener('click', resolveVariance);
        document.getElementById('reviewIssuesBtn')?.addEventListener('click', reviewIssues);
    }

    async function loadBatchData() {
        try {
            batchData = await getJson(`${RECEIVING_API}/batches/${batchId}`);
            displayBatchSummary();
        } catch (error) {
            console.error('Error loading batch data:', error);
            showAlert('danger', 'Failed to load batch data: ' + error.message);
            const summaryEl = document.getElementById('batchSummary');
            if (summaryEl) {
                summaryEl.innerHTML = '<div class="text-danger">Could not load batch summary.</div>';
            }
        }
    }

    function displayBatchSummary() {
        const summary = document.getElementById('batchSummary');
        summary.innerHTML = `
            <div class="row">
                <div class="col-md-4">
                    <h6>Batch Information</h6>
                    <p><strong>Batch ID:</strong> ${batchData.receivingBatchId}</p>
                    <p><strong>Type:</strong> ${batchData.sourceType === 2 ? 'RnR Normal' : 'RnR Emergency'}</p>
                    <p><strong>Status:</strong> <span class="badge bg-warning">${batchData.status}</span></p>
                </div>
                <div class="col-md-4">
                    <h6>Collection Slip</h6>
                    <p><strong>Slip Number:</strong> ${batchData.collectionSlip?.slipNumber || 'N/A'}</p>
                    <p><strong>School:</strong> ${batchData.collectionSlip?.schoolName || 'N/A'}</p>
                    <p><strong>EMIS Code:</strong> ${batchData.collectionSlip?.emisCode || 'N/A'}</p>
                </div>
                <div class="col-md-4">
                    <h6>Scanning Results</h6>
                    <p><strong>Devices Scanned:</strong> ${batchData.devicesScanned ?? batchData.actualCount ?? 0}</p>
                    <p><strong>Scanned By:</strong> ${batchData.scanningOfficer || batchData.receivedBy || 'N/A'}</p>
                    <p><strong>Completed:</strong> ${batchData.scanningCompletedAt ? new Date(batchData.scanningCompletedAt).toLocaleString() : 'N/A'}</p>
                </div>
            </div>
        `;
    }

    async function performVerification() {
        try {
            const summary = await getJson(`${RNR_API}/batches/${batchId}/summary`);
            const scans = await getJson(`${RNR_API}/batches/${batchId}/scans`);

            const hasScans = Array.isArray(scans) && scans.length > 0;

            // Build verificationData expected by renderer
            const checks = [];
            checks.push({
                title: 'All expected devices present',
                description: 'No missing devices compared to slip',
                status: summary.missing === 0 && hasScans ? 'pass' : 'fail',
                details: summary.missing > 0 ? `${summary.missing} missing` : (hasScans ? '' : 'No scans recorded')
            });
            checks.push({
                title: 'No unexpected devices',
                description: 'All scanned devices were on the slip',
                status: summary.unexpected === 0 && hasScans ? 'pass' : 'fail',
                details: summary.unexpected > 0 ? `${summary.unexpected} unexpected` : (hasScans ? '' : 'No scans recorded')
            });

            verificationData = {
                verificationChecks: checks,
                scannedDevices: scans.map(s => ({
                    serialNumber: s.serial,
                    deviceInfo: s.deviceInfo,
                    schoolMatch: s.schoolMatch,
                    status: s.status,
                    issues: [],
                })),
                hasVariances: (summary.missing || 0) > 0 || (summary.unexpected || 0) > 0,
                hasIssues: !hasScans || (summary.missing || 0) > 0 || (summary.unexpected || 0) > 0,
            };

            displayVerificationResults();
        } catch (error) {
            console.error('Error performing verification:', error);
            showAlert('danger', 'Failed to perform verification: ' + error.message);
        }
    }

    function displayVerificationResults() {
        displayVerificationChecklist();
        displayDevicesReview();
        displayVerificationActions();
    }

    function displayVerificationChecklist() {
        const checklist = document.getElementById('verificationChecklist');
        const checks = verificationData.verificationChecks || [];
        
        checklist.innerHTML = checks.map(check => {
            let itemClass = 'verification-item ';
            let icon = '';
            
            if (check.status === 'pass') {
                itemClass += 'success';
                icon = '<i class="bi bi-check-circle-fill text-success"></i>';
            } else if (check.status === 'warning') {
                itemClass += 'warning';
                icon = '<i class="bi bi-exclamation-triangle-fill text-warning"></i>';
            } else {
                itemClass += 'danger';
                icon = '<i class="bi bi-x-circle-fill text-danger"></i>';
            }
            
            return `
                <div class="${itemClass}">
                    <div class="d-flex align-items-center">
                        <div class="me-3">${icon}</div>
                        <div class="flex-grow-1">
                            <h6 class="mb-1">${check.title}</h6>
                            <p class="mb-0 small">${check.description}</p>
                            ${check.details ? `<div class="mt-2 small text-muted">${check.details}</div>` : ''}
                        </div>
                    </div>
                </div>
            `;
        }).join('');
    }

    function displayDevicesReview() {
        const devicesTable = document.getElementById('devicesReview');
        const devices = verificationData.scannedDevices || [];
        
        if (devices.length === 0) {
            devicesTable.innerHTML = `
                <tr>
                    <td colspan="6" class="text-center text-muted py-4">
                        No devices found in verification data.
                    </td>
                </tr>
            `;
            return;
        }

        devicesTable.innerHTML = devices.map((device, index) => {
            let rowClass = '';
            let issuesDisplay = '';
            
            if (device.issues && device.issues.length > 0) {
                rowClass = device.validationResult === 'failed' ? 'table-danger' : 'table-warning';
                issuesDisplay = `
                    <button class="btn btn-sm btn-outline-warning" onclick="showDeviceIssues(${index})">
                        <i class="bi bi-exclamation-triangle"></i> ${device.issues.length}
                    </button>
                `;
            } else {
                issuesDisplay = '<span class="text-success"><i class="bi bi-check-circle"></i> None</span>';
            }

            return `
                <tr class="${rowClass}">
                    <td>${index + 1}</td>
                    <td><code>${device.serialNumber}</code></td>
                    <td>${device.deviceInfo || 'Unknown Device'}</td>
                    <td>
                        <span class="badge ${device.schoolMatch === 'Match' ? 'bg-success' : device.schoolMatch === 'Mismatch' ? 'bg-warning' : 'bg-secondary'}">
                            ${device.schoolMatch || 'Unknown'}
                        </span>
                    </td>
                    <td>${device.status || 'Unknown'}</td>
                    <td>${issuesDisplay}</td>
                </tr>
            `;
        }).join('');
    }

    function displayVerificationActions() {
        const hasIssues = verificationData.hasIssues || false;
        const hasVariances = verificationData.hasVariances || false;
        
        if (hasVariances) {
            document.getElementById('varianceSection').style.display = 'block';
            displayVarianceDetails();
        }
        
        if (hasIssues) {
            document.getElementById('verificationIssues').style.display = 'block';
            document.getElementById('verificationSuccess').style.display = 'none';
        } else {
            document.getElementById('verificationSuccess').style.display = 'block';
            document.getElementById('verificationIssues').style.display = 'none';
        }
    }

    function displayVarianceDetails() {
        const varianceDetails = document.getElementById('varianceDetails');
        const variances = verificationData.variances || [];
        
        varianceDetails.innerHTML = `
            <div class="alert alert-warning">
                <h6>Variances Detected:</h6>
                <ul class="mb-0">
                    ${variances.map(v => `<li>${v.description}</li>`).join('')}
                </ul>
            </div>
        `;
    }

    function requestRecount() {
        if (confirm('Request a recount? This will return to the scanning phase.')) {
            window.location.href = `/phase1/rnr-scanning.html?batchId=${batchId}`;
        }
    }

    function requestSupervisorApproval() {
        alert('Supervisor approval workflow would be implemented here.\n\nThis would:\n• Send notification to supervisor\n• Require supervisor login and approval\n• Document approval in audit trail');
    }

    async function resolveVariance() {
        const notes = document.getElementById('resolutionNotes').value.trim();
        if (!notes) {
            alert('Please provide resolution notes before resolving the variance.');
            return;
        }

        try {
            const response = await fetch(`${API_BASE}/rnr/resolve-variance`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    receivingBatchId: batchId,
                    resolutionNotes: notes,
                    resolvedBy: 'Receiving Clerk'
                })
            });

            if (response.ok) {
                showAlert('success', 'Variance resolved successfully!');
                document.getElementById('varianceSection').style.display = 'none';
                document.getElementById('verificationSuccess').style.display = 'block';
                document.getElementById('verificationIssues').style.display = 'none';
            } else {
                const error = await response.json();
                showAlert('danger', 'Failed to resolve variance: ' + (error.message || 'Unknown error'));
            }
        } catch (error) {
            console.error('Error resolving variance:', error);
            showAlert('danger', 'Error resolving variance: ' + error.message);
        }
    }

    function reviewIssues() {
        const devices = verificationData.scannedDevices || [];
        const issueDevices = devices.filter(d => d.issues && d.issues.length > 0);
        
        let issuesList = 'Devices with Issues:\n\n';
        issueDevices.forEach((device, index) => {
            issuesList += `${index + 1}. ${device.serialNumber}\n`;
            device.issues.forEach(issue => {
                issuesList += `   • ${issue}\n`;
            });
            issuesList += '\n';
        });
        
        alert(issuesList);
    }

    async function proceedToGRV() {
        if (!batchId || batchId === '00000000-0000-0000-0000-000000000000') {
            showAlert('danger', 'Error generating GRV: Missing batchId');
            return;
        }
        const hasIssues = verificationData.hasIssues || false;
        
        if (hasIssues) {
            const confirmMsg = 'There are unresolved issues. Proceed to GRV generation anyway?\n\nThis will:\n• Generate GRV with issue notes\n• Move devices to TechOps Queue\n• Mark issues for technician review';
            if (!confirm(confirmMsg)) {
                return;
            }
        }

        try {
            showAlert('info', 'Verifying batch and generating GRV...');

            // Step 1: Verify the batch first (sets status to Verified)
            await getJson(`${RNR_API}/batches/${batchId}/verify`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    verifiedBy: batchData?.scanningOfficer || batchData?.receivedBy || 'Receiving Clerk',
                    notes: hasIssues ? 'Verified with issues - proceeding to GRV' : 'Verified successfully'
                })
            });

            // Step 2: Generate GRV
            const grv = await getJson(`${RECON_API}/generate-grv/${batchId}`, {
                method: 'POST'
            });

            // Redirect to GRV completion page (Step 4)
            const grvId = grv.grvId;
            const processed = grv.totalQuantity ?? 0;
            window.location.href = `/phase1/rnr-complete.html?batchId=${batchId}&grvId=${grvId}&grvNumber=${encodeURIComponent(grv.grvNumber)}&processed=${processed}`;
        } catch (error) {
            console.error('Error generating GRV:', error);
            showAlert('danger', 'Error generating GRV: ' + error.message);
        }
    }

    function showDeviceIssues(deviceIndex) {
        const device = verificationData.scannedDevices[deviceIndex];
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

    // Expose function globally
    window.showDeviceIssues = showDeviceIssues;
})();

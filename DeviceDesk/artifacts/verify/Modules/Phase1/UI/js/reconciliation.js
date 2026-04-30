(() => {
    const API_BASE = `${location.origin}/api/phase1/receiving`;
    const RECON_API = `${location.origin}/api/phase1/reconciliation`;
    
    let batchId = null;
    let batchData = null;

    // Get batch ID from URL
    const urlParams = new URLSearchParams(window.location.search);
    batchId = urlParams.get('batchId');

    if (!batchId) {
        alert('No batch ID provided. Redirecting to dashboard.');
        window.location.href = '/phase1/dashboard.html';
        return;
    }

    // Initialize
    init();

    async function init() {
        await loadBatchData();
        await performReconciliation();
    }

    async function loadBatchData() {
        try {
            const response = await fetch(`${API_BASE}/batches/${batchId}`);
            if (!response.ok) throw new Error('Failed to load batch data');
            
            batchData = await response.json();
            displayBatchInfo();
        } catch (error) {
            console.error('Error loading batch data:', error);
            document.getElementById('batchInfo').innerHTML = `
                <div class="alert alert-danger">
                    <i class="bi bi-exclamation-triangle"></i> 
                    Failed to load batch data: ${error.message}
                </div>
            `;
        }
    }

    function displayBatchInfo() {
        const batchInfo = document.getElementById('batchInfo');
        batchInfo.innerHTML = `
            <div class="row">
                <div class="col-md-6">
                    <h6>Batch Information</h6>
                    <p><strong>Batch ID:</strong> ${batchData.receivingBatchId}</p>
                    <p><strong>Source Type:</strong> ${batchData.sourceType === 1 ? 'New Stock' : 'RnR'}</p>
                    <p><strong>Status:</strong> <span class="badge bg-primary">${batchData.status}</span></p>
                </div>
                <div class="col-md-6">
                    <h6>Order Details</h6>
                    <p><strong>Order Number:</strong> ${batchData.order?.orderNumber || 'N/A'}</p>
                    <p><strong>Supplier:</strong> ${batchData.order?.supplierName || 'N/A'}</p>
                    <p><strong>Expected Items:</strong> ${batchData.expectedCount || 0}</p>
                </div>
            </div>
        `;
    }

    async function performReconciliation() {
        try {
            const response = await fetch(`${RECON_API}/submit-count`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ 
                    receivingBatchId: batchId,
                    submittedBy: 'Receiving Clerk'
                })
            });

            if (!response.ok) throw new Error('Failed to perform reconciliation');
            
            const reconciliationData = await response.json();
            displayReconciliationResults(reconciliationData);
        } catch (error) {
            console.error('Error performing reconciliation:', error);
            showAlert('danger', 'Failed to perform reconciliation: ' + error.message);
        }
    }

    function displayReconciliationResults(data) {
        const table = document.getElementById('reconciliationTable');
        table.innerHTML = '';

        // Group items by brand/model for display
        const itemGroups = {};
        
        if (batchData.order?.lines) {
            batchData.order.lines.forEach(line => {
                const key = `${line.brand} ${line.model}`;
                if (!itemGroups[key]) {
                    itemGroups[key] = {
                        name: key,
                        invoiceQty: 0,
                        countedQty: 0
                    };
                }
                itemGroups[key].invoiceQty += line.quantityOrdered;
            });
        }

        // Calculate counted quantities (distribute evenly for demo)
        const totalCounted = data.actualCount || batchData.actualCount || 0;
        const itemCount = Object.keys(itemGroups).length;
        const avgCountedPerItem = itemCount > 0 ? Math.floor(totalCounted / itemCount) : 0;

        let hasVariance = false;

        Object.values(itemGroups).forEach(item => {
            item.countedQty = avgCountedPerItem;
            const variance = item.countedQty - item.invoiceQty;
            
            if (variance !== 0) hasVariance = true;

            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${item.name}</td>
                <td class="text-center">${item.invoiceQty}</td>
                <td class="text-center">${item.countedQty}</td>
                <td class="text-center">
                    <span class="variance-badge ${variance === 0 ? 'variance-match' : 'variance-mismatch'}">
                        ${variance === 0 ? '0' : (variance > 0 ? '+' + variance : variance)}
                    </span>
                </td>
            `;
            table.appendChild(row);
        });

        // Show appropriate result section
        if (hasVariance || data.hasVariance) {
            document.getElementById('matchResult').style.display = 'none';
            document.getElementById('varianceResult').style.display = 'block';
            setupVarianceHandlers();
        } else {
            document.getElementById('matchResult').style.display = 'block';
            document.getElementById('varianceResult').style.display = 'none';
            setupMatchHandlers();
        }
    }

    function setupMatchHandlers() {
        document.getElementById('proceedToGRVBtn').addEventListener('click', async () => {
            await proceedToGRV();
        });
    }

    function setupVarianceHandlers() {
        document.getElementById('recountBtn').addEventListener('click', () => {
            if (confirm('Request a recount? This will return to the scanning phase.')) {
                window.location.href = `/phase1/rnr-scanning.html?batchId=${batchId}`;
            }
        });

        document.getElementById('supervisorApprovalBtn').addEventListener('click', () => {
            alert('Supervisor approval workflow would be implemented here.');
        });

        document.getElementById('acceptVarianceBtn').addEventListener('click', async () => {
            const reason = document.getElementById('varianceReason').value.trim();
            if (!reason) {
                alert('Please provide a reason for the variance before accepting.');
                return;
            }

            if (confirm('Accept variance and proceed to GRV generation?')) {
                await resolveVariance(reason);
            }
        });
    }

    async function resolveVariance(reason) {
        try {
            const response = await fetch(`${RECON_API}/resolve-variance`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    receivingBatchId: batchId,
                    varianceReason: reason,
                    varianceResolution: 'Accepted',
                    resolvedBy: 'Receiving Clerk'
                })
            });

            if (!response.ok) throw new Error('Failed to resolve variance');
            
            showAlert('success', 'Variance resolved successfully! Proceeding to GRV generation...');
            
            setTimeout(() => {
                proceedToGRV();
            }, 2000);
        } catch (error) {
            console.error('Error resolving variance:', error);
            showAlert('danger', 'Failed to resolve variance: ' + error.message);
        }
    }

    async function proceedToGRV() {
        try {
            const response = await fetch(`${RECON_API}/generate-grv/${batchId}`, {
                method: 'POST'
            });

            if (!response.ok) throw new Error('Failed to generate GRV');

            const grvData = await response.json();
            const grvId = grvData.grvId;
            const processed = grvData.totalQuantity ?? 0;

            // Redirect to unified RnR completion page with GRV details
            window.location.href = `/phase1/rnr-complete.html?batchId=${batchId}&grvId=${grvId}&grvNumber=${encodeURIComponent(grvData.grvNumber)}&processed=${processed}`;
        } catch (error) {
            console.error('Error generating GRV:', error);
            showAlert('danger', 'Failed to generate GRV: ' + error.message);
        }
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
})();

(() => {
    const API_BASE = `${location.origin}/api/phase1`;
    const urlParams = new URLSearchParams(window.location.search);
    const batchId = urlParams.get('id');

    if (!batchId) {
        alert('No batch ID provided');
        window.location.href = '/phase1/dashboard.html';
        return;
    }

    let currentModels = [];
    let activeModel = null;
    let scannedSerials = [];
    let currentStep = 'model-selection'; // 'model-selection', 'scanning', 'variance'

    // Initialize when DOM is ready
    function init() {
        loadBatchInfo();
        loadModels();

        // Event Listeners - check if elements exist
        const scanBtn = document.getElementById('scanBtn');
        const serialInput = document.getElementById('serialNumber');
        const closeModelBtn = document.getElementById('closeModelBtn');
        const backToModelsBtn = document.getElementById('backToModelsBtn');
        const backToModelsFromVarianceBtn = document.getElementById('backToModelsFromVarianceBtn');
        const generateGrvBtn = document.getElementById('generateGrvBtn');

        if (scanBtn) scanBtn.addEventListener('click', scanSerial);
        if (serialInput) serialInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') scanSerial();
        });
        if (closeModelBtn) closeModelBtn.addEventListener('click', closeActiveModel);
        if (backToModelsBtn) backToModelsBtn.addEventListener('click', () => showStep('model-selection'));
        if (backToModelsFromVarianceBtn) backToModelsFromVarianceBtn.addEventListener('click', () => showStep('model-selection'));
        if (generateGrvBtn) generateGrvBtn.addEventListener('click', generateGRV);
    }

    // Wait for DOM to be ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    async function loadBatchInfo() {
        try {
            const res = await fetch(`${API_BASE}/receiving/batches/${batchId}`);
            const batch = await res.json();
            
            document.getElementById('orderNumber').textContent = batch.invoiceNumber || batch.orderNumber || 'N/A';
        } catch (err) {
            console.error('Error loading batch info:', err);
        }
    }

    async function loadModels() {
        try {
            const res = await fetch(`${API_BASE}/model-scanning/orders/${batchId}/models`);
            if (!res.ok) throw new Error('Failed to load models');
            
            const data = await res.json();
            currentModels = data.models || data; // Handle both wrapped and unwrapped responses
            renderModelList();
        } catch (err) {
            showAlert('Error loading models: ' + err.message, 'danger');
            document.getElementById('modelList').innerHTML = `
                <div class="col-12 text-center text-danger py-5">
                    <i class="bi bi-exclamation-triangle" style="font-size: 3rem;"></i>
                    <p class="mt-3">Error loading models. Please try refreshing the page.</p>
                    <p class="text-muted">${err.message}</p>
                </div>
            `;
        }
    }

    function renderModelList() {
        const container = document.getElementById('modelList');
        
        if (currentModels.length === 0) {
            container.innerHTML = `
                <div class="col-12 text-center text-muted py-5">
                    <i class="bi bi-inbox" style="font-size: 3rem;"></i>
                    <p class="mt-3">No models found for this order.</p>
                </div>
            `;
            return;
        }

        container.innerHTML = currentModels.map(model => {
            const progress = model.expectedQty > 0 ? Math.round((model.countedQty / model.expectedQty) * 100) : 0;
            const variance = model.expectedQty - model.countedQty;
            const statusClass = model.status === 'Closed' ? 'status-closed' : 
                               model.countedQty > 0 ? 'status-scanning' : 'status-open';
            const cardClass = model.status === 'Closed' ? 'model-card completed' : 'model-card';
            
            return `
                <div class="col-md-6">
                    <div class="${cardClass}" onclick="window.selectModel('${model.modelId}')" 
                         ${model.status === 'Closed' ? 'style="pointer-events: none;"' : ''}>
                        <div class="d-flex justify-content-between align-items-start mb-2">
                            <h5 class="mb-0">${model.modelName}</h5>
                            <span class="model-status ${statusClass}">${model.status}</span>
                        </div>
                        <div class="row mt-3">
                            <div class="col-6">
                                <small class="text-muted">Expected</small>
                                <div class="fs-4 fw-bold">${model.expectedQty}</div>
                            </div>
                            <div class="col-6">
                                <small class="text-muted">Counted</small>
                                <div class="fs-4 fw-bold ${variance === 0 && model.status === 'Closed' ? 'text-success' : variance < 0 ? 'text-danger' : ''}">${model.countedQty}</div>
                            </div>
                        </div>
                        <div class="progress mt-3" style="height: 8px;">
                            <div class="progress-bar ${variance === 0 && model.status === 'Closed' ? 'bg-success' : 'bg-primary'}" 
                                 style="width: ${progress}%"></div>
                        </div>
                        ${model.status !== 'Closed' ? 
                            `<button class="btn btn-sm btn-primary mt-3 w-100">
                                <i class="bi bi-upc-scan"></i> ${model.countedQty > 0 ? 'Continue Scanning' : 'Start Scanning'}
                            </button>` : 
                            `<div class="text-center mt-3 text-success">
                                <i class="bi bi-check-circle-fill"></i> Completed
                            </div>`
                        }
                    </div>
                </div>
            `;
        }).join('');
    }

    async function selectModel(modelId) {
        const model = currentModels.find(m => m.modelId === modelId);
        if (!model || model.status === 'Closed') return;

        activeModel = model;
        
        // Load scanned serials for this model
        await loadScannedSerials(modelId);
        
        // Update UI
        document.getElementById('activeModelName').textContent = model.modelName;
        document.getElementById('activeModelExpected').textContent = model.expectedQty;
        document.getElementById('activeModelCounted').textContent = model.countedQty;
        
        showStep('scanning');
        document.getElementById('serialNumber').focus();
    }

    async function loadScannedSerials(modelId) {
        try {
            // Get all scanned serials for this model from database
            const res = await fetch(`${API_BASE}/model-scanning/orders/${batchId}/models/${modelId}/serials`);
            if (res.ok) {
                scannedSerials = await res.json();
                renderSerialList();
            } else {
                scannedSerials = [];
                renderSerialList();
            }
        } catch (err) {
            console.error('Error loading scanned serials:', err);
            scannedSerials = [];
            renderSerialList();
        }
    }

    function renderSerialList() {
        const tbody = document.getElementById('serialList');
        
        if (scannedSerials.length === 0) {
            tbody.innerHTML = '<tr><td colspan="3" class="text-center text-muted py-4">No serials scanned yet</td></tr>';
            return;
        }

        tbody.innerHTML = scannedSerials.map((item, idx) => `
            <tr>
                <td>${idx + 1}</td>
                <td><strong>${item.deviceSerial}</strong></td>
                <td>${new Date(item.timestamp).toLocaleTimeString()}</td>
            </tr>
        `).join('');
    }

    async function scanSerial() {
        const serialNumber = document.getElementById('serialNumber').value.trim();

        if (!serialNumber) {
            showAlert('Please enter a serial number', 'warning');
            return;
        }

        if (!activeModel) {
            showAlert('No active model selected', 'danger');
            return;
        }

        try {
            const res = await fetch(`${API_BASE}/model-scanning/orders/${batchId}/models/${activeModel.modelId}/scan`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ serial: serialNumber })
            });

            const result = await res.json();

            if (res.ok && result.success) {
                showAlert(result.message || 'Serial scanned successfully!', 'success');
                
                // Update counted qty
                activeModel.countedQty++;
                document.getElementById('activeModelCounted').textContent = activeModel.countedQty;
                
                // Add to scanned list
                scannedSerials.push({
                    deviceSerial: serialNumber,
                    timestamp: new Date().toISOString()
                });
                renderSerialList();
                
                // Clear input
                document.getElementById('serialNumber').value = '';
                document.getElementById('serialNumber').focus();
                
                // Check if we've reached expected quantity
                if (activeModel.countedQty >= activeModel.expectedQty) {
                    showAlert(`✅ Reached expected quantity (${activeModel.expectedQty}). You can now close this model.`, 'info');
                }
            } else {
                showAlert(result.message || 'Failed to scan serial', 'danger');
            }
        } catch (err) {
            showAlert('Error: ' + err.message, 'danger');
        }
    }

    async function closeActiveModel() {
        if (!activeModel) return;

        if (activeModel.countedQty === 0) {
            showAlert('Cannot close model with no scanned serials', 'warning');
            return;
        }

        if (activeModel.countedQty < activeModel.expectedQty) {
            const shortage = activeModel.expectedQty - activeModel.countedQty;
            if (!confirm(`⚠️ Shortage detected: ${shortage} devices missing.\n\nExpected: ${activeModel.expectedQty}\nScanned: ${activeModel.countedQty}\n\nAre you sure you want to close this model?`)) {
                return;
            }
        }

        try {
            const res = await fetch(`${API_BASE}/model-scanning/models/${activeModel.modelId}/close`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });

            const result = await res.json();

            if (res.ok && result.success) {
                showAlert('Model closed successfully!', 'success');
                
                // Update model status
                activeModel.status = 'Closed';
                activeModel = null;
                
                // Reload models and go back to selection
                await loadModels();
                showStep('model-selection');
                
                // Check if all models are closed
                const allClosed = currentModels.every(m => m.status === 'Closed');
                if (allClosed) {
                    setTimeout(() => {
                        if (confirm('All models have been scanned and closed. View variance report?')) {
                            showVarianceReport();
                        }
                    }, 1000);
                }
            } else {
                showAlert(result.message || 'Failed to close model', 'danger');
            }
        } catch (err) {
            showAlert('Error: ' + err.message, 'danger');
        }
    }

    async function showVarianceReport() {
        try {
            const res = await fetch(`${API_BASE}/model-scanning/orders/${batchId}/variance`);
            if (!res.ok) throw new Error('Failed to load variance report');
            
            const data = await res.json();
            const variance = data.success ? data : data; // Handle wrapped response
            
            // Calculate totals
            const totalExpected = variance.models.reduce((sum, m) => sum + m.expectedQty, 0);
            const totalCounted = variance.models.reduce((sum, m) => sum + m.countedQty, 0);
            const totalVariance = totalExpected - totalCounted;
            
            // Render variance report
            const reportHtml = `
                <div class="row">
                    <div class="col-md-12">
                        <div class="card mb-4">
                            <div class="card-header bg-primary text-white">
                                <h5 class="mb-0"><i class="bi bi-graph-up"></i> Order Summary</h5>
                            </div>
                            <div class="card-body">
                                <div class="row text-center">
                                    <div class="col-md-3">
                                        <h3 class="text-primary">${totalExpected}</h3>
                                        <p class="text-muted">Expected</p>
                                    </div>
                                    <div class="col-md-3">
                                        <h3 class="text-info">${totalCounted}</h3>
                                        <p class="text-muted">Counted</p>
                                    </div>
                                    <div class="col-md-3">
                                        <h3 class="${totalVariance === 0 ? 'text-success' : 'text-danger'}">${totalVariance}</h3>
                                        <p class="text-muted">Variance</p>
                                    </div>
                                    <div class="col-md-3">
                                        <h3>
                                            ${variance.allModelsClosed ? 
                                                '<i class="bi bi-check-circle-fill text-success"></i>' : 
                                                '<i class="bi bi-hourglass-split text-warning"></i>'}
                                        </h3>
                                        <p class="text-muted">Status</p>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <div class="row">
                    <div class="col-md-12">
                        <div class="card">
                            <div class="card-header bg-white">
                                <h5 class="mb-0"><i class="bi bi-list-check"></i> Model Breakdown</h5>
                            </div>
                            <div class="card-body p-0">
                                <table class="table table-hover mb-0">
                                    <thead class="table-light">
                                        <tr>
                                            <th>Model Name</th>
                                            <th class="text-center">Expected</th>
                                            <th class="text-center">Counted</th>
                                            <th class="text-center">Variance</th>
                                            <th class="text-center">Status</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        ${variance.models.map(m => `
                                            <tr>
                                                <td><strong>${m.modelName}</strong></td>
                                                <td class="text-center">${m.expectedQty}</td>
                                                <td class="text-center">${m.countedQty}</td>
                                                <td class="text-center">
                                                    <span class="badge ${m.variance === 0 ? 'bg-success' : 'bg-danger'}">
                                                        ${m.variance}
                                                    </span>
                                                </td>
                                                <td class="text-center">
                                                    ${m.variance === 0 ? 
                                                        '<i class="bi bi-check-circle-fill text-success"></i>' : 
                                                        '<i class="bi bi-exclamation-triangle-fill text-danger"></i>'}
                                                </td>
                                            </tr>
                                        `).join('')}
                                    </tbody>
                                </table>
                            </div>
                        </div>
                    </div>
                </div>

                ${variance.canGenerateGRV ? `
                    <div class="row mt-4">
                        <div class="col-md-12">
                            <div class="alert alert-success text-center">
                                <h5><i class="bi bi-check-circle"></i> Ready for GRV Generation</h5>
                                <p class="mb-0">All models match expected quantities. You can now generate the Goods Received Voucher.</p>
                            </div>
                        </div>
                    </div>
                ` : `
                    <div class="row mt-4">
                        <div class="col-md-12">
                            <div class="alert alert-warning text-center">
                                <h5><i class="bi bi-exclamation-triangle"></i> Variance Detected</h5>
                                <p class="mb-0">Some models have variances. Please review before proceeding.</p>
                            </div>
                        </div>
                    </div>
                `}
            `;
            
            document.getElementById('varianceReport').innerHTML = reportHtml;
            
            // Show/hide GRV button
            if (variance.canGenerateGRV) {
                document.getElementById('generateGrvBtn').style.display = 'inline-block';
            } else {
                document.getElementById('generateGrvBtn').style.display = 'none';
            }
            
            showStep('variance');
        } catch (err) {
            showAlert('Error loading variance report: ' + err.message, 'danger');
        }
    }

    async function generateGRV() {
        try {
            showAlert('Generating GRV document...', 'info');
            
            // Call GRV generation API (you'll need to implement this endpoint)
            const res = await fetch(`${API_BASE}/model-scanning/orders/${batchId}/grv`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });

            if (res.ok) {
                const blob = await res.blob();
                const url = window.URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = `GRV-${batchId}.pdf`;
                document.body.appendChild(a);
                a.click();
                window.URL.revokeObjectURL(url);
                document.body.removeChild(a);
                
                showAlert('GRV generated successfully!', 'success');
            } else {
                showAlert('Failed to generate GRV', 'danger');
            }
        } catch (err) {
            showAlert('Error generating GRV: ' + err.message, 'danger');
        }
    }

    function showStep(step) {
        // Hide all steps
        document.querySelectorAll('.wizard-step').forEach(el => {
            el.classList.remove('active');
        });
        
        // Show selected step
        document.getElementById(`step-${step}`).classList.add('active');
        currentStep = step;
        
        // Reload data based on step
        if (step === 'model-selection') {
            loadModels();
        } else if (step === 'variance') {
            showVarianceReport();
        }
    }

    function showAlert(message, type) {
        const container = document.getElementById('alertContainer');
        const alert = document.createElement('div');
        alert.className = `alert alert-${type} alert-scan alert-dismissible fade show`;
        alert.innerHTML = `
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;
        container.appendChild(alert);
        
        setTimeout(() => alert.remove(), 5000);
    }

    // Expose functions globally
    window.selectModel = selectModel;
})();

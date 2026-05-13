(() => {
    console.log('=== MODEL SCANNING PAGE LOADED ===');
    
    const urlParams = new URLSearchParams(window.location.search);
    const batchId = urlParams.get('id');
    
    console.log('Raw batchId from URL:', batchId, 'type:', typeof batchId);
    
    // NUCLEAR GUARD: Validate batchId BEFORE any other code runs
    const guidRegex = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
    
    if (!batchId || batchId === 'null' || batchId === 'undefined' || !guidRegex.test(batchId)) {
        const errorMsg = 'Invalid or missing batch/order ID in the URL. Please start Model-Driven Scanning from a valid Receiving batch.';
        alert(errorMsg);
        console.error('Aborting model-scanning: invalid batchId in URL:', batchId);
        console.error('batchId === "null":', batchId === 'null');
        console.error('batchId === null:', batchId === null);
        console.error('!batchId:', !batchId);
        throw new Error('Aborting model-scanning: invalid batchId in URL: ' + batchId);
    }
    
    // From here on, batchId is guaranteed to be a valid GUID
    console.log('Using batchId for model scanning:', batchId);
    
    const API_BASE = `${location.origin}/api/phase1/model-scanning`;

    let currentBatch = null;
    let models = [];
    let activeModel = null;
    let scannedSerials = [];

    function escapeHtmlMs(s) {
        const d = document.createElement('div');
        d.textContent = s == null ? '' : String(s);
        return d.innerHTML;
    }

    // Wait for DOM to be ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    function init() {
        console.log('Initializing...');
        
        // batchId is already validated above, so we can safely use it
        console.log('Loading models for batch:', batchId);
        loadBatchInfo();
        loadModels();

        // Event listeners
        document.getElementById('serialInput').addEventListener('keypress', handleSerialScan);
        document.getElementById('closeModelBtn').addEventListener('click', closeActiveModel);
        document.getElementById('generateGRVBtn').addEventListener('click', generateGRV);
    }

    async function loadBatchInfo() {
        try {
            const response = await fetch(`${location.origin}/api/phase0/newstock/batches/${batchId}`);
            if (!response.ok) throw new Error('Failed to load batch info');

            currentBatch = await response.json();
            document.getElementById('batchNumber').textContent = currentBatch.batchNumber;
            document.getElementById('orderSlip').textContent = currentBatch.invoiceNumber || 'N/A';

            // Check if GRV already exists
            if (currentBatch.grvNumber || currentBatch.GRVNumber) {
                console.log('Batch already has GRV:', currentBatch.grvNumber || currentBatch.GRVNumber);
                checkAndShowGRV();
            }
        } catch (error) {
            console.error('Error loading batch:', error);
            showAlert('Error loading batch information', 'danger');
        }
    }

    async function loadModels() {
        try {
            console.log('Loading models for batch:', batchId);
            const response = await fetch(`${API_BASE}/orders/${batchId}/models`);
            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.error || 'Failed to load models');
            }

            const data = await response.json();
            models = data.models || [];
            console.log('Models loaded:', models);
            renderModelCards();
        } catch (error) {
            console.error('Error loading models:', error);
            document.getElementById('modelsGrid').innerHTML = `
                <div class="col-12 text-center">
                    <div class="alert alert-danger">
                        <i class="bi bi-exclamation-triangle"></i> ${error.message}
                    </div>
                    <a href="/phase0/new-stock-batch.html" class="btn btn-primary">Back to Batches</a>
                </div>
            `;
        }
    }

    function renderModelCards() {
        console.log('renderModelCards called with', models.length, 'models');
        if (models.length === 0) {
            document.getElementById('modelsGrid').innerHTML = `
                <div class="col-12 text-center">
                    <div class="alert alert-warning">
                        <i class="bi bi-exclamation-circle"></i> No models found for this batch.
                    </div>
                </div>
            `;
            return;
        }

        // Check if all models are closed
        const allClosed = models.every(m => m.status === 'Closed');
        if (allClosed) {
            showVarianceReport();
            return;
        }

        document.getElementById('modelsGrid').innerHTML = models.map(model => {
            const isClosed = model.status === 'Closed';
            // API returns ModelID (uppercase), not modelId
            const modelID = model.modelID || model.ModelID;

            const breakdown = Array.isArray(model.schoolBreakdown) ? model.schoolBreakdown : [];
            const breakdownHtml = breakdown.length
                ? `
                    <div class="mt-3 small">
                        <div class="text-muted text-uppercase fw-semibold mb-1" style="letter-spacing:.04em;">School breakdown</div>
                        <ul class="list-unstyled mb-0">
                            ${breakdown
                                .map(b => `<li><span class="fw-semibold">${escapeHtmlMs(b.schoolName)}</span>: ${b.qtyOrdered}</li>`)
                                .join('')}
                        </ul>
                    </div>`
                : '';

            return `
                <div class="col-md-4">
                    <div class="model-card ${isClosed ? 'completed' : ''} card h-100"
                         style="${isClosed ? '' : 'cursor: pointer;'}">
                        <div class="card-body">
                            <h5 class="card-title">
                                ${escapeHtmlMs(model.modelName)}
                                ${isClosed ? '<i class="bi bi-check-circle-fill text-success float-end"></i>' : ''}
                            </h5>
                            <div class="row text-center mt-3">
                                <div class="col-6">
                                    <h6 class="text-muted mb-1">Expected</h6>
                                    <h2 class="text-primary">${model.expectedQty}</h2>
                                </div>
                                <div class="col-6">
                                    <h6 class="text-muted mb-1">Scanned</h6>
                                    <h2 class="text-info">${model.countedQty}</h2>
                                </div>
                            </div>
                            ${breakdownHtml}
                            <div class="mt-4 text-center">
                                ${isClosed ?
                                    '<span class="badge bg-secondary"><i class="bi bi-lock"></i> Closed</span>' :
                                    `<button class="btn btn-success btn-lg w-100" data-model-id="${modelID}"><i class="bi bi-upc-scan"></i> Click to Scan</button>`
                                }
                            </div>
                        </div>
                    </div>
                </div>
            `;
        }).join('');
        
        // Attach click handlers after rendering
        console.log('Attaching click handlers to buttons...');
        const buttons = document.querySelectorAll('button[data-model-id]');
        console.log('Found ' + buttons.length + ' buttons');
        
        buttons.forEach(button => {
            console.log('Attaching to button:', button.getAttribute('data-model-id'));
            button.addEventListener('click', function() {
                const modelId = this.getAttribute('data-model-id');
                console.log('Button clicked! ModelID:', modelId);
                selectModel(modelId);
            });
        });
    }

    function selectModel(modelId) {
        console.log('selectModel called with:', modelId);
        // API returns ModelID (uppercase), handle both cases
        activeModel = models.find(m => (m.modelID || m.ModelID) === modelId || (m.modelId || m.modelid) === modelId);
        if (!activeModel) {
            console.error('Model not found for ID:', modelId);
            console.log('Available models:', models);
            return;
        }

        // Update UI - hide expected quantity for blind scanning
        document.getElementById('activeModelName').textContent = activeModel.modelName;
        document.getElementById('activeScanned').textContent = activeModel.countedQty;

        // Switch sections
        document.getElementById('modelSelectionSection').style.display = 'none';
        document.getElementById('scanningSection').style.display = 'block';

        // Load scanned serials for this model
        loadScannedSerials();

        // Focus serial input
        setTimeout(() => {
            document.getElementById('serialInput').focus();
        }, 100);
    };
    
    // Helper function to get ModelID (API returns ModelID in PascalCase)
    function getModelID(model) {
        return model.ModelID || model.modelID || model.modelId;
    }

    async function loadScannedSerials() {
        try {
            const modelID = getModelID(activeModel);
            const response = await fetch(`${API_BASE}/orders/${batchId}/models/${modelID}/serials`);
            if (!response.ok) throw new Error('Failed to load serials');

            const data = await response.json();
            scannedSerials = data.serials || [];
            renderSerialsList();
        } catch (error) {
            console.error('Error loading serials:', error);
        }
    }

    function renderSerialsList() {
        const container = document.getElementById('serialsList');
        document.getElementById('serialCount').textContent = scannedSerials.length;

        if (scannedSerials.length === 0) {
            container.innerHTML = '<p class="text-muted col-12">No serials scanned yet...</p>';
            return;
        }

        container.innerHTML = scannedSerials.map((serial, index) => `
            <div class="col-md-4">
                <div class="scanned-serial-item">
                    <div class="d-flex justify-content-between align-items-center">
                        <div>
                            <strong>#${index + 1}</strong>
                            <div class="font-monospace">${serial.deviceSerial}</div>
                            <small class="text-muted">${new Date(serial.timestamp).toLocaleTimeString()}</small>
                        </div>
                        <i class="bi bi-check-circle-fill text-success"></i>
                    </div>
                </div>
            </div>
        `).join('');
    }

    async function handleSerialScan(event) {
        if (event.key !== 'Enter') return;

        const input = event.target;
        const serial = input.value.trim();

        if (!serial) return;

        // Clear input
        input.value = '';

        // Show scanning feedback
        showScanFeedback('Scanning...', 'info');

        try {
            const modelID = getModelID(activeModel);
            const response = await fetch(`${API_BASE}/orders/${batchId}/models/${modelID}/scan`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ serial: serial })
            });

            const result = await response.json();

            if (!response.ok || !result.success) {
                throw new Error(result.error || 'Scan failed');
            }

            // Success!
            showScanFeedback(`✓ Scanned: ${serial}`, 'success');
            
            // Update model data
            activeModel.countedQty++;
            document.getElementById('activeScanned').textContent = activeModel.countedQty;

            // Add to scanned serials list
            scannedSerials.push({
                deviceSerial: serial,
                timestamp: new Date().toISOString()
            });
            renderSerialsList();

            // Play success sound (optional)
            playBeep();

        } catch (error) {
            console.error('Scan error:', error);
            showScanFeedback(`✗ Error: ${error.message}`, 'danger');
            playErrorBeep();
        }

        // Refocus input
        setTimeout(() => input.focus(), 100);
    }

    async function closeActiveModel() {
        if (!activeModel) return;

        // Check if expected quantity is met
        if (activeModel.countedQty < activeModel.expectedQty) {
            const shortage = activeModel.expectedQty - activeModel.countedQty;
            if (!confirm(`Warning: You scanned ${activeModel.countedQty} but expected ${activeModel.expectedQty}. Shortage of ${shortage}. Close anyway?`)) {
                return;
            }
        }

        try {
            const modelID = getModelID(activeModel);
            const response = await fetch(`${API_BASE}/models/${modelID}/close`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });

            const result = await response.json();

            if (!response.ok || !result.success) {
                throw new Error(result.error || 'Failed to close model');
            }

            // Update model status
            activeModel.status = 'Closed';
            activeModel = null;
            scannedSerials = [];

            // Reload models
            await loadModels();

            // Switch back to model selection
            document.getElementById('scanningSection').style.display = 'none';
            document.getElementById('modelSelectionSection').style.display = 'block';

            // Clear input
            document.getElementById('serialInput').value = '';
            document.getElementById('scanFeedback').innerHTML = '';

        } catch (error) {
            console.error('Error closing model:', error);
            alert('Error closing model: ' + error.message);
        }
    }

    async function showVarianceReport() {
        try {
            const response = await fetch(`${API_BASE}/orders/${batchId}/variance`);
            if (!response.ok) throw new Error('Failed to load variance report');

            const variance = await response.json();

            // Update summary
            document.getElementById('varTotalExpected').textContent = variance.totalExpected;
            document.getElementById('varTotalScanned').textContent = variance.totalCounted;
            document.getElementById('varTotalVariance').textContent = variance.totalVariance;

            // Render per-model breakdown
            const tbody = document.getElementById('varianceTableBody');
            tbody.innerHTML = variance.models.map(model => {
                let rowClass = 'variance-ok';
                let statusIcon = '<i class="bi bi-check-circle-fill text-success"></i>';

                if (model.variance < 0) {
                    rowClass = 'variance-shortage';
                    statusIcon = '<i class="bi bi-dash-circle-fill text-danger"></i> Shortage';
                } else if (model.variance > 0) {
                    rowClass = 'variance-overage';
                    statusIcon = '<i class="bi bi-plus-circle-fill text-warning"></i> Overage';
                } else {
                    statusIcon = '<i class="bi bi-check-circle-fill text-success"></i> Match';
                }

                return `
                    <tr class="${rowClass}">
                        <td><strong>${model.modelName}</strong></td>
                        <td>${model.expectedQty}</td>
                        <td>${model.countedQty}</td>
                        <td class="fw-bold">${model.variance > 0 ? '+' + model.variance : model.variance}</td>
                        <td>${statusIcon}</td>
                    </tr>
                `;
            }).join('');

            // Show/hide GRV button
            if (variance.canGenerateGRV && variance.allModelsClosed) {
                document.getElementById('generateGRVBtn').style.display = 'inline-block';
                document.getElementById('varianceWarning').style.display = 'none';
            } else {
                document.getElementById('generateGRVBtn').style.display = 'none';
                document.getElementById('varianceWarning').style.display = 'block';
            }

            // Show variance section
            document.getElementById('modelSelectionSection').style.display = 'none';
            document.getElementById('scanningSection').style.display = 'none';
            document.getElementById('varianceSection').style.display = 'block';

        } catch (error) {
            console.error('Error loading variance:', error);
            alert('Error loading variance report: ' + error.message);
        }
    }

    async function generateGRV() {
        if (!confirm('Generate GRV document for this batch?')) return;

        try {
            const response = await fetch(`${API_BASE}/orders/${batchId}/generate-grv`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.error || 'Failed to generate GRV');
            }

            const result = await response.json();
            
            alert(`✓ GRV Generated Successfully!\n\nGRV Number: ${result.grvNumber}`);
            
            // Show View GRV button instead of Generate button
            const grvButton = document.getElementById('generateGRVBtn');
            if (grvButton) {
                grvButton.outerHTML = `
                    <button onclick="viewGRV()" class="btn btn-success btn-lg" style="background: #059669;">
                        <i class="bi bi-eye"></i> View GRV Document
                    </button>
                    <button onclick="window.location.href='/phase1/receiving-create.html'" class="btn btn-secondary btn-lg ms-2">
                        <i class="bi bi-arrow-left"></i> Back to Batch List
                    </button>
                `;
            }
        } catch (error) {
            console.error('Error generating GRV:', error);
            alert('Error generating GRV: ' + error.message);
        }
    }

    // Expose generateGRV globally for onclick handler
    window.generateGRV = generateGRV;

    function viewGRV() {
        window.location.href = `/phase1/grv-document.html?id=${batchId}`;
    }

    // Expose viewGRV globally for onclick handlers
    window.viewGRV = viewGRV;

    async function checkAndShowGRV() {
        // Wait a bit for variance section to render
        setTimeout(() => {
            const grvButton = document.getElementById('generateGRVBtn');
            if (grvButton && grvButton.style.display !== 'none') {
                grvButton.outerHTML = `
                    <button onclick="viewGRV()" class="btn btn-success btn-lg" style="background: #059669;">
                        <i class="bi bi-eye"></i> View GRV Document
                    </button>
                    <button onclick="window.location.href='/phase1/receiving-create.html'" class="btn btn-secondary btn-lg ms-2">
                        <i class="bi bi-arrow-left"></i> Back to Batch List
                    </button>
                `;
            }
        }, 500);
    }

    function showScanFeedback(message, type) {
        const feedback = document.getElementById('scanFeedback');
        feedback.innerHTML = `
            <div class="alert alert-${type} alert-dismissible fade show" role="alert">
                ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        `;

        // Auto-dismiss success messages
        if (type === 'success') {
            setTimeout(() => {
                feedback.innerHTML = '';
            }, 2000);
        }
    }

    function playBeep() {
        // Simple beep using Web Audio API
        try {
            const audioContext = new (window.AudioContext || window.webkitAudioContext)();
            const oscillator = audioContext.createOscillator();
            const gainNode = audioContext.createGain();

            oscillator.connect(gainNode);
            gainNode.connect(audioContext.destination);

            oscillator.frequency.value = 800;
            oscillator.type = 'sine';

            gainNode.gain.setValueAtTime(0.3, audioContext.currentTime);
            gainNode.gain.exponentialRampToValueAtTime(0.01, audioContext.currentTime + 0.1);

            oscillator.start(audioContext.currentTime);
            oscillator.stop(audioContext.currentTime + 0.1);
        } catch (e) {
            // Audio not supported, ignore
        }
    }

    function playErrorBeep() {
        try {
            const audioContext = new (window.AudioContext || window.webkitAudioContext)();
            const oscillator = audioContext.createOscillator();
            const gainNode = audioContext.createGain();

            oscillator.connect(gainNode);
            gainNode.connect(audioContext.destination);

            oscillator.frequency.value = 200;
            oscillator.type = 'sawtooth';

            gainNode.gain.setValueAtTime(0.3, audioContext.currentTime);
            gainNode.gain.exponentialRampToValueAtTime(0.01, audioContext.currentTime + 0.3);

            oscillator.start(audioContext.currentTime);
            oscillator.stop(audioContext.currentTime + 0.3);
        } catch (e) {
            // Audio not supported, ignore
        }
    }

    function showAlert(message, type) {
        // Simple alert implementation
        alert(message);
    }
})();

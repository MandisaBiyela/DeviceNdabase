// GRV Document Viewer
const API_BASE = '/api/phase1/model-scanning';

// Get batch ID from URL
const urlParams = new URLSearchParams(window.location.search);
const batchId = urlParams.get('id');

if (!batchId) {
    showError('No batch ID provided');
} else {
    loadGRVData();
}

async function loadGRVData() {
    try {
        const response = await fetch(`${API_BASE}/orders/${batchId}/grv`);
        
        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'Failed to load GRV');
        }

        const data = await response.json();
        displayGRV(data);
    } catch (error) {
        console.error('Error loading GRV:', error);
        showError(error.message || 'Failed to load GRV document');
    }
}

function displayGRV(data) {
    // Hide loading
    document.getElementById('loading').style.display = 'none';
    document.getElementById('grv-content').style.display = 'block';

    // Document details
    document.getElementById('grv-number').textContent = data.grvNumber || data.GRVNumber;
    document.getElementById('batch-number').textContent = data.batchNumber || data.BatchNumber;
    document.getElementById('created-date').textContent = formatDate(data.createdDate || data.CreatedDate);
    document.getElementById('confirmed-date').textContent = formatDate(data.confirmedDate || data.ConfirmedDate);

    // Supplier info
    document.getElementById('supplier-name').textContent = data.supplierName || data.SupplierName || 'N/A';
    document.getElementById('invoice-number').textContent = data.invoiceNumber || data.InvoiceNumber || 'N/A';
    document.getElementById('confirmed-by').textContent = data.confirmedBy || data.ConfirmedBy || 'System';

    // Total quantity
    document.getElementById('total-quantity').textContent = data.totalQuantity || data.TotalQuantity || 0;

    // Models
    const models = data.models || data.Models || [];
    renderModels(models);

    // Generated time
    document.getElementById('generated-time').textContent = `Generated: ${new Date().toLocaleString()}`;
}

function renderModels(models) {
    const container = document.getElementById('models-container');
    container.innerHTML = '';

    models.forEach(model => {
        const modelName = model.modelName || model.ModelName;
        const expectedQty = model.expectedQty || model.ExpectedQty;
        const countedQty = model.countedQty || model.CountedQty;
        const variance = model.variance || model.Variance;
        const serials = model.serials || model.Serials || [];

        const card = document.createElement('div');
        card.className = 'model-card';
        
        card.innerHTML = `
            <div class="model-header">
                <div class="model-name">${modelName}</div>
                <div class="model-stats">
                    <div class="stat">
                        <div class="stat-label">Expected</div>
                        <div class="stat-value">${expectedQty}</div>
                    </div>
                    <div class="stat">
                        <div class="stat-label">Received</div>
                        <div class="stat-value match">${countedQty}</div>
                    </div>
                    <div class="stat">
                        <div class="stat-label">Variance</div>
                        <div class="stat-value">${variance}</div>
                    </div>
                </div>
            </div>
            <div>
                <strong>Serial Numbers (${serials.length}):</strong>
                <div class="serials-list">
                    ${serials.map(serial => `<div class="serial-item">${serial}</div>`).join('')}
                </div>
            </div>
        `;

        container.appendChild(card);
    });
}

function formatDate(dateString) {
    if (!dateString) return 'N/A';
    const date = new Date(dateString);
    return date.toLocaleDateString('en-GB', {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });
}

function showError(message) {
    document.getElementById('loading').style.display = 'none';
    const errorDiv = document.getElementById('error');
    errorDiv.innerHTML = `
        <div class="grv-document">
            <div class="error-message">
                <h3>❌ Error Loading GRV</h3>
                <p>${message}</p>
                <button class="btn-back" onclick="goBack()" style="margin-top: 15px;">← Go Back</button>
            </div>
        </div>
    `;
    errorDiv.style.display = 'block';
}

function goBack() {
    // Validate batchId before redirecting
    if (!batchId || batchId === 'null' || batchId === 'undefined') {
        console.error('Invalid batchId in grv-document.js goBack():', batchId);
        alert('Cannot go back: Invalid batch ID. Please navigate from the receiving batch list.');
        window.location.href = '/phase1/receiving-dashboard.html';
        return;
    }
    
    const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    if (!guidPattern.test(batchId)) {
        console.error('Invalid GUID format in grv-document.js goBack():', batchId);
        alert('Cannot go back: Invalid batch ID format. Please navigate from the receiving batch list.');
        window.location.href = '/phase1/receiving-dashboard.html';
        return;
    }
    
    console.log('Going back to model-scanning.html with batchId:', batchId);
    window.location.href = `/phase1/model-scanning.html?id=${encodeURIComponent(batchId)}`;
}

function downloadPDF() {
    if (!batchId) {
        alert('No batch ID available for download');
        return;
    }
    
    // Open the blind copy PDF in a new tab (which will trigger download)
    window.open(`/api/phase1/receiving/batches/${batchId}/blind-copy`, '_blank');
}

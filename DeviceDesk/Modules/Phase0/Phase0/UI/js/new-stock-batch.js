(() => {
    const API_BASE = `${location.origin}/api/phase0/newstock`;
    let items = [];

    // Load batches on page load
    loadBatches();

    // Add item button
    document.getElementById('addItemBtn').addEventListener('click', addItem);

    // Save batch button
    document.getElementById('saveBatchBtn').addEventListener('click', saveBatch);

    // Add first item by default
    addItem();

    function addItem() {
        const itemId = Date.now();
        const itemHtml = `
            <div class="item-row" data-item-id="${itemId}">
                <div class="row">
                    <div class="col-md-3 mb-2">
                        <label class="form-label">Brand</label>
                        <input type="text" class="form-control form-control-sm item-brand" placeholder="e.g., HP">
                    </div>
                    <div class="col-md-3 mb-2">
                        <label class="form-label">Model</label>
                        <input type="text" class="form-control form-control-sm item-model" placeholder="e.g., EliteBook 840">
                    </div>
                    <div class="col-md-2 mb-2">
                        <label class="form-label">Type</label>
                        <select class="form-select form-select-sm item-type">
                            <option value="Laptop">Laptop</option>
                            <option value="Desktop">Desktop</option>
                            <option value="Tablet">Tablet</option>
                            <option value="Chromebook">Chromebook</option>
                            <option value="Other">Other</option>
                        </select>
                    </div>
                    <div class="col-md-2 mb-2">
                        <label class="form-label">Quantity</label>
                        <input type="number" class="form-control form-control-sm item-quantity" min="1" value="1">
                    </div>
                    <div class="col-md-2 mb-2">
                        <label class="form-label">&nbsp;</label>
                        <button type="button" class="btn btn-sm btn-danger w-100 remove-item-btn" data-item-id="${itemId}">
                            <i class="bi bi-trash"></i> Remove
                        </button>
                    </div>
                </div>
                <div class="row">
                    <div class="col-12">
                        <label class="form-label">Description (optional)</label>
                        <input type="text" class="form-control form-control-sm item-description" placeholder="e.g., 14-inch business laptop">
                    </div>
                </div>
            </div>
        `;

        document.getElementById('itemsContainer').insertAdjacentHTML('beforeend', itemHtml);

        // Add remove handler
        document.querySelector(`[data-item-id="${itemId}"] .remove-item-btn`).addEventListener('click', function() {
            document.querySelector(`[data-item-id="${itemId}"]`).remove();
        });
    }

    async function saveBatch() {
        const supplierName = document.getElementById('supplierName').value.trim();
        const invoiceNumber = document.getElementById('invoiceNumber').value.trim();
        const expectedDeliveryDate = document.getElementById('expectedDeliveryDate').value;

        // Collect items
        const itemRows = document.querySelectorAll('.item-row');
        const items = [];

        itemRows.forEach(row => {
            const brand = row.querySelector('.item-brand').value.trim();
            const model = row.querySelector('.item-model').value.trim();
            const deviceType = row.querySelector('.item-type').value;
            const quantity = parseInt(row.querySelector('.item-quantity').value);
            const description = row.querySelector('.item-description').value.trim();

            if (quantity > 0) {
                items.push({
                    brand: brand || null,
                    model: model || null,
                    deviceType: deviceType,
                    description: description || null,
                    quantityExpected: quantity
                });
            }
        });

        // Validation
        if (items.length === 0) {
            showAlert('Please add at least one item with quantity > 0', 'danger');
            return;
        }

        // Get current user email (from cookie or session)
        const createdBy = getCookie('user_email') || 'clerk@example.com';

        const payload = {
            supplierName: supplierName || null,
            invoiceNumber: invoiceNumber || null,
            expectedDeliveryDate: expectedDeliveryDate || null,
            items: items,
            createdBy: createdBy
        };

        try {
            const response = await fetch(`${API_BASE}/batches`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.error || 'Failed to create batch');
            }

            const result = await response.json();
            showAlert(`Batch ${result.batchNumber} created successfully!`, 'success');

            // Close modal and reload
            bootstrap.Modal.getInstance(document.getElementById('createBatchModal')).hide();
            document.getElementById('createBatchForm').reset();
            document.getElementById('itemsContainer').innerHTML = '';
            addItem(); // Add one default item
            loadBatches();
        } catch (error) {
            console.error('Error creating batch:', error);
            showAlert('Error: ' + error.message, 'danger');
        }
    }

    async function loadBatches() {
        try {
            const response = await fetch(`${API_BASE}/batches`);
            if (!response.ok) throw new Error('Failed to load batches');

            const batches = await response.json();
            renderBatches(batches);
        } catch (error) {
            console.error('Error loading batches:', error);
            document.getElementById('batchesTableBody').innerHTML = `
                <tr>
                    <td colspan="9" class="text-center text-danger">
                        Error loading batches: ${error.message}
                    </td>
                </tr>
            `;
        }
    }

    function renderBatches(batches) {
        const tbody = document.getElementById('batchesTableBody');

        if (batches.length === 0) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="9" class="text-center text-muted">
                        No batches found. Click "Create Batch" to get started.
                    </td>
                </tr>
            `;
            return;
        }

        tbody.innerHTML = batches.map(batch => `
            <tr>
                <td><strong>${batch.batchNumber}</strong></td>
                <td>${batch.supplierName || '-'}</td>
                <td>${batch.invoiceNumber || '-'}</td>
                <td><span class="badge bg-primary">${batch.totalQuantityExpected}</span></td>
                <td><span class="badge bg-info">${batch.totalQuantityScanned}</span></td>
                <td>${getStatusBadge(batch.status)}</td>
                <td>${new Date(batch.createdAt).toLocaleString()}</td>
                <td>${batch.grvNumber || '-'}</td>
                <td>
                    <button class="btn btn-sm btn-outline-primary me-1" onclick="viewBatch('${batch.batchId}')">
                        <i class="bi bi-eye"></i> View
                    </button>
                    ${batch.status === 0 || batch.status === 1 ? `
                        <a href="/phase0/model-scanning.html?id=${batch.batchId}" class="btn btn-sm btn-success">
                            <i class="bi bi-upc-scan"></i> Scan
                        </a>
                    ` : ''}
                </td>
            </tr>
        `).join('');
    }

    function getStatusBadge(status) {
        const statusMap = {
            0: { text: 'Pending Scan', class: 'status-pending' },
            1: { text: 'Scanning', class: 'status-scanning' },
            2: { text: 'Ready to Confirm', class: 'status-ready' },
            3: { text: 'Mismatch', class: 'status-mismatch' },
            4: { text: 'Completed', class: 'status-completed' },
            5: { text: 'Cancelled', class: 'status-mismatch' }
        };

        const statusInfo = statusMap[status] || { text: 'Unknown', class: 'status-pending' };
        return `<span class="status-badge ${statusInfo.class}">${statusInfo.text}</span>`;
    }

    window.viewBatch = async function(batchId) {
        try {
            const response = await fetch(`${API_BASE}/batches/${batchId}`);
            if (!response.ok) throw new Error('Failed to load batch details');

            const batch = await response.json();
            renderBatchDetails(batch);

            const modal = new bootstrap.Modal(document.getElementById('viewBatchModal'));
            modal.show();
        } catch (error) {
            console.error('Error loading batch details:', error);
            showAlert('Error: ' + error.message, 'danger');
        }
    };

    function renderBatchDetails(batch) {
        const content = document.getElementById('batchDetailsContent');
        content.innerHTML = `
            <div class="row mb-3">
                <div class="col-md-6">
                    <h6>Batch Number</h6>
                    <p class="lead">${batch.batchNumber}</p>
                </div>
                <div class="col-md-6">
                    <h6>Status</h6>
                    <p>${getStatusBadge(batch.status)}</p>
                </div>
            </div>

            <div class="row mb-3">
                <div class="col-md-6">
                    <h6>Supplier</h6>
                    <p>${batch.supplierName || '-'}</p>
                </div>
                <div class="col-md-6">
                    <h6>Invoice Number</h6>
                    <p>${batch.invoiceNumber || '-'}</p>
                </div>
            </div>

            <div class="row mb-3">
                <div class="col-md-4">
                    <h6>Expected Quantity</h6>
                    <p><span class="badge bg-primary fs-6">${batch.totalQuantityExpected}</span></p>
                </div>
                <div class="col-md-4">
                    <h6>Scanned Quantity</h6>
                    <p><span class="badge bg-info fs-6">${batch.totalQuantityScanned}</span></p>
                </div>
                <div class="col-md-4">
                    <h6>GRV Number</h6>
                    <p>${batch.grvNumber || '-'}</p>
                </div>
            </div>

            <hr>
            <h6>Item Lines</h6>
            <div class="table-responsive">
                <table class="table table-sm">
                    <thead>
                        <tr>
                            <th>Brand</th>
                            <th>Model</th>
                            <th>Type</th>
                            <th>Description</th>
                            <th>Expected</th>
                            <th>Scanned</th>
                        </tr>
                    </thead>
                    <tbody>
                        ${batch.items.map(item => `
                            <tr>
                                <td>${item.brand || '-'}</td>
                                <td>${item.model || '-'}</td>
                                <td>${item.deviceType || '-'}</td>
                                <td>${item.description || '-'}</td>
                                <td><span class="badge bg-primary">${item.quantityExpected}</span></td>
                                <td><span class="badge bg-info">${item.quantityScanned}</span></td>
                            </tr>
                        `).join('')}
                    </tbody>
                </table>
            </div>

            ${batch.notes ? `
                <hr>
                <h6>Notes</h6>
                <p>${batch.notes}</p>
            ` : ''}

            <hr>
            <div class="row">
                <div class="col-md-6">
                    <small class="text-muted">Created by: ${batch.createdBy}</small><br>
                    <small class="text-muted">Created at: ${new Date(batch.createdAt).toLocaleString()}</small>
                </div>
                <div class="col-md-6 text-end">
                    ${batch.confirmedBy ? `
                        <small class="text-muted">Confirmed by: ${batch.confirmedBy}</small><br>
                        <small class="text-muted">Confirmed at: ${new Date(batch.confirmedAt).toLocaleString()}</small>
                    ` : ''}
                </div>
            </div>
        `;
    }

    function showAlert(message, type) {
        const alertHtml = `
            <div class="alert alert-${type} alert-dismissible fade show" role="alert">
                ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        `;
        document.getElementById('alertContainer').innerHTML = alertHtml;

        // Auto-dismiss after 5 seconds
        setTimeout(() => {
            const alert = document.querySelector('.alert');
            if (alert) {
                bootstrap.Alert.getInstance(alert)?.close();
            }
        }, 5000);
    }

    function getCookie(name) {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
        return null;
    }
})();

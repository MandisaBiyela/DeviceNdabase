(() => {
    const API_BASE = `${location.origin}/api/phase1/receiving`;
    
    let allBatches = [];
    let filteredBatches = [];
    let currentPage = 1;
    let pageSize = 10;

    // Elements
    const batchesTable = document.getElementById('batchesTable');
    const sourceFilter = document.getElementById('sourceFilter');
    const statusFilter = document.getElementById('statusFilter');
    const dateFrom = document.getElementById('dateFrom');
    const dateTo = document.getElementById('dateTo');
    const filterBtn = document.getElementById('filterBtn');
    const refreshBtn = document.getElementById('refreshBtn');
    const exportBtn = document.getElementById('exportBtn');
    
    // Stats elements
    const totalBatches = document.getElementById('totalBatches');
    const totalInvoices = document.getElementById('totalInvoices');
    const totalSlips = document.getElementById('totalSlips');
    const totalDevices = document.getElementById('totalDevices');

    // Initialize
    init();

    async function init() {
        await loadAllData();
        setupEventListeners();
        applyFilters();
    }

    function setupEventListeners() {
        filterBtn.addEventListener('click', applyFilters);
        refreshBtn.addEventListener('click', () => {
            loadAllData();
            applyFilters();
        });
        exportBtn.addEventListener('click', exportData);
        
        // Auto-filter on select change
        sourceFilter.addEventListener('change', applyFilters);
        statusFilter.addEventListener('change', applyFilters);
    }

    async function loadAllData() {
        try {
            // Fetch real data from API
            const response = await fetch(`${API_BASE}/list`);
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            
            const data = await response.json();
            allBatches = data || [];
            
            updateStatistics();
        } catch (error) {
            console.error('Error loading data:', error);
            showError('Failed to load batch data. ' + error.message);
            allBatches = [];
            updateStatistics();
        }
    }

    function updateStatistics() {
        const stats = {
            totalBatches: allBatches.length,
            totalInvoices: allBatches.filter(b => b.documentInfo.type === 'Invoice').length,
            totalSlips: allBatches.filter(b => b.documentInfo.type === 'Collection Slip' || b.documentInfo.type === 'Emergency Slip').length,
            totalDevices: allBatches.reduce((sum, b) => sum + (b.actualCount || 0), 0)
        };

        totalBatches.textContent = stats.totalBatches;
        totalInvoices.textContent = stats.totalInvoices;
        totalSlips.textContent = stats.totalSlips;
        totalDevices.textContent = stats.totalDevices;
    }

    function applyFilters() {
        filteredBatches = allBatches.filter(batch => {
            // Source type filter
            if (sourceFilter.value && batch.sourceType.toString() !== sourceFilter.value) {
                return false;
            }

            // Status filter
            if (statusFilter.value && batch.status !== statusFilter.value) {
                return false;
            }

            // Date range filter
            if (dateFrom.value) {
                const batchDate = new Date(batch.createdAt);
                const fromDate = new Date(dateFrom.value);
                if (batchDate < fromDate) return false;
            }

            if (dateTo.value) {
                const batchDate = new Date(batch.createdAt);
                const toDate = new Date(dateTo.value);
                toDate.setHours(23, 59, 59, 999); // End of day
                if (batchDate > toDate) return false;
            }

            return true;
        });

        currentPage = 1;
        displayBatches();
    }

    function displayBatches() {
        const startIndex = (currentPage - 1) * pageSize;
        const endIndex = startIndex + pageSize;
        const pageData = filteredBatches.slice(startIndex, endIndex);

        if (pageData.length === 0) {
            batchesTable.innerHTML = `
                <tr>
                    <td colspan="9" class="text-center py-5 text-muted">
                        <i class="bi bi-inbox" style="font-size: 3rem;"></i>
                        <p class="mt-3">No batches found matching your criteria</p>
                    </td>
                </tr>
            `;
            return;
        }

        batchesTable.innerHTML = pageData.map(batch => `
            <tr>
                <td>
                    <strong>${batch.batchId}</strong>
                    <br><small class="text-muted">by ${batch.createdBy}</small>
                </td>
                <td>
                    <span class="source-badge ${getSourceClass(batch.sourceType)}">
                        ${batch.sourceTypeName}
                    </span>
                </td>
                <td>
                    <div>
                        <strong>${batch.documentInfo.type}: ${batch.documentInfo.number}</strong>
                        ${batch.documentInfo.supplier ? `<br><small class="text-muted">Supplier: ${batch.documentInfo.supplier}</small>` : ''}
                        ${batch.documentInfo.school ? `<br><small class="text-muted">School: ${batch.documentInfo.school}</small>` : ''}
                        ${batch.documentInfo.amount ? `<br><small class="text-success">${batch.documentInfo.amount}</small>` : ''}
                        ${batch.documentInfo.loanUnit ? `<br><small class="text-warning">Loan: ${batch.documentInfo.loanUnit}</small>` : ''}
                        <br><small class="text-muted">Uploaded: ${formatDateTime(batch.documentInfo.uploadedAt)}</small>
                    </div>
                </td>
                <td>
                    <strong>${batch.schoolSupplier}</strong>
                    ${batch.documentInfo.emisCode ? `<br><small class="text-muted">EMIS: ${batch.documentInfo.emisCode}</small>` : ''}
                </td>
                <td>
                    <span class="status-badge ${getStatusClass(batch.status)}">
                        ${formatStatus(batch.status)}
                    </span>
                </td>
                <td>
                    <div class="text-center">
                        <strong>${batch.actualCount}/${batch.deviceCount}</strong>
                        <br><small class="text-muted">scanned/expected</small>
                    </div>
                </td>
                <td>
                    <div>
                        ${formatDateTime(batch.createdAt)}
                        <br><small class="text-muted">${formatTimeAgo(batch.createdAt)}</small>
                    </div>
                </td>
                <td>
                    <div>
                        ${formatDateTime(batch.lastUpdated)}
                        <br><small class="text-muted">${formatTimeAgo(batch.lastUpdated)}</small>
                    </div>
                </td>
                <td>
                    <div class="d-flex flex-column gap-1">
                        <button class="btn btn-outline-primary btn-action" onclick="viewBatch('${batch.batchId}')" title="View Details">
                            <i class="bi bi-eye"></i> View
                        </button>
                        ${batch.status !== 'Completed' ? `
                            <button class="btn btn-outline-success btn-action" onclick="continueBatch('${batch.batchId}')" title="Continue Process">
                                <i class="bi bi-play"></i> Continue
                            </button>
                        ` : ''}
                        <button class="btn btn-outline-secondary btn-action" onclick="downloadDocs('${batch.batchId}')" title="Download Documents">
                            <i class="bi bi-download"></i> Docs
                        </button>
                    </div>
                </td>
            </tr>
        `).join('');

        updatePaginationInfo();
    }

    function getSourceClass(sourceType) {
        switch(sourceType) {
            case 1: return 'source-new-stock';
            case 2: return 'source-rnr-normal';
            case 3: return 'source-rnr-emergency';
            default: return 'source-new-stock';
        }
    }

    function getStatusClass(status) {
        switch(status) {
            case 'Draft': return 'status-draft';
            case 'ScanningInProgress': return 'status-scanning';
            case 'PendingVerification': return 'status-verification';
            case 'Completed': return 'status-completed';
            default: return 'status-draft';
        }
    }

    function formatStatus(status) {
        switch(status) {
            case 'ScanningInProgress': return 'Scanning';
            case 'PendingVerification': return 'Verification';
            default: return status;
        }
    }

    function formatDateTime(dateString) {
        return new Date(dateString).toLocaleString();
    }

    function formatTimeAgo(dateString) {
        const now = new Date();
        const date = new Date(dateString);
        const diffMs = now - date;
        const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
        const diffDays = Math.floor(diffHours / 24);

        if (diffDays > 0) return `${diffDays} day${diffDays > 1 ? 's' : ''} ago`;
        if (diffHours > 0) return `${diffHours} hour${diffHours > 1 ? 's' : ''} ago`;
        return 'Just now';
    }

    function updatePaginationInfo() {
        const startIndex = (currentPage - 1) * pageSize + 1;
        const endIndex = Math.min(currentPage * pageSize, filteredBatches.length);
        
        document.getElementById('showingFrom').textContent = startIndex;
        document.getElementById('showingTo').textContent = endIndex;
        document.getElementById('totalRecords').textContent = filteredBatches.length;
    }

    function showError(message) {
        batchesTable.innerHTML = `
            <tr>
                <td colspan="9" class="text-center py-5 text-danger">
                    <i class="bi bi-exclamation-triangle" style="font-size: 3rem;"></i>
                    <p class="mt-3">${message}</p>
                </td>
            </tr>
        `;
    }

    function exportData() {
        // Mock export functionality
        alert('Export functionality would generate CSV/Excel file with all batch data including:\n\n• Batch details\n• Document information\n• Timestamps\n• Device counts\n• Status history\n• User actions');
    }

    // Global functions for button actions
    window.viewBatch = function(batchId) {
        const batch = allBatches.find(b => b.batchId === batchId);
        if (batch) {
            alert(`Batch Details: ${batchId}\n\nSource: ${batch.sourceTypeName}\nDocument: ${batch.documentInfo.type} ${batch.documentInfo.number}\nStatus: ${batch.status}\nDevices: ${batch.actualCount}/${batch.deviceCount}\nNotes: ${batch.notes}`);
        }
    };

    window.continueBatch = function(batchId) {
        const batch = allBatches.find(b => b.batchId === batchId);
        if (batch) {
            // Redirect to appropriate workflow step based on status and source type
            let redirectUrl = '/phase1/receiving-create.html';
            
            if (batch.status === 'ScanningInProgress') {
                if (batch.sourceType === 3) {
                    redirectUrl = `/phase1/emergency-scanning.html?batchId=${batchId}`;
                } else {
                    redirectUrl = `/phase1/rnr-scanning.html?batchId=${batchId}`;
                }
            } else if (batch.status === 'PendingVerification') {
                if (batch.sourceType === 1) {
                    redirectUrl = `/phase1/reconciliation.html?batchId=${batchId}`;
                } else {
                    redirectUrl = `/phase1/rnr-verification.html?batchId=${batchId}`;
                }
            }
            
            window.location.href = redirectUrl;
        }
    };

    window.downloadDocs = function(batchId) {
        alert(`Download documents for ${batchId}:\n\n• Original invoice/slip\n• Blind copy/emergency slip\n• GRV document (if completed)\n• Scanning reports\n• Audit trail`);
    };
})();

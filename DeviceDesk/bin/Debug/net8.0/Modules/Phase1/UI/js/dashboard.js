(() => {
    const API_BASE = `${location.origin}/api/phase1`;

    // Load dashboard data on page load
    document.addEventListener('DOMContentLoaded', () => {
        loadDashboardStats();
        loadRecentActivity();
    });

    async function loadDashboardStats() {
        try {
            const response = await fetch(`${API_BASE}/receiving/dashboard/stats`);
            if (response.ok) {
                const contentType = response.headers.get('content-type');
                if (!contentType || !contentType.includes('application/json')) {
                    const text = await response.text();
                    console.error('Expected JSON but got:', contentType, text.substring(0, 200));
                    throw new Error(`Server returned ${contentType} instead of JSON`);
                }
                const stats = await response.json();
                updateStats({
                    totalBatches: stats.totalBatches || 0,
                    completedBatches: stats.completedBatches || 0,
                    inProgressBatches: stats.inProgressBatches || 0,
                    totalDevices: stats.totalDevices || 0,
                    newStockCount: stats.newStockCount || 0,
                    rnrNormalCount: stats.rnrNormalCount || 0,
                    rnrEmergencyCount: stats.rnrEmergencyCount || 0
                });
            } else {
                const errorText = await response.text().catch(() => 'Unknown error');
                console.error('Failed to load dashboard stats:', response.status, response.statusText, errorText.substring(0, 200));
                // Show zeros on error
                updateStats({
                    totalBatches: 0,
                    completedBatches: 0,
                    inProgressBatches: 0,
                    totalDevices: 0,
                    newStockCount: 0,
                    rnrNormalCount: 0,
                    rnrEmergencyCount: 0
                });
            }
        } catch (err) {
            console.error('Error loading dashboard stats:', err);
            // Show zeros on error
            updateStats({
                totalBatches: 0,
                completedBatches: 0,
                inProgressBatches: 0,
                totalDevices: 0,
                newStockCount: 0,
                rnrNormalCount: 0,
                rnrEmergencyCount: 0
            });
        }
    }

    function updateStats(stats) {
        document.getElementById('totalBatches').textContent = stats.totalBatches;
        document.getElementById('completedBatches').textContent = stats.completedBatches;
        document.getElementById('inProgressBatches').textContent = stats.inProgressBatches;
        document.getElementById('totalDevices').textContent = stats.totalDevices;
        
        // Update source breakdown
        document.getElementById('newStockCount').textContent = stats.newStockCount;
        document.getElementById('rnrNormalCount').textContent = stats.rnrNormalCount;
        document.getElementById('rnrEmergencyCount').textContent = stats.rnrEmergencyCount;
        
        const total = stats.newStockCount + stats.rnrNormalCount + stats.rnrEmergencyCount;
        if (total > 0) {
            document.getElementById('newStockBar').style.width = `${(stats.newStockCount / total) * 100}%`;
            document.getElementById('rnrNormalBar').style.width = `${(stats.rnrNormalCount / total) * 100}%`;
            document.getElementById('rnrEmergencyBar').style.width = `${(stats.rnrEmergencyCount / total) * 100}%`;
        }
    }

    async function loadRecentActivity() {
        const activityContainer = document.getElementById('recentActivity');
        if (!activityContainer) {
            console.warn('Recent activity container not found');
            return;
        }

        try {
            const response = await fetch(`${API_BASE}/receiving/dashboard/recent`);
            if (response.ok) {
                const contentType = response.headers.get('content-type');
                if (!contentType || !contentType.includes('application/json')) {
                    const text = await response.text();
                    console.error('Expected JSON but got:', contentType, text.substring(0, 200));
                    throw new Error(`Server returned ${contentType} instead of JSON`);
                }
                const batches = await response.json();
                
                if (!batches || batches.length === 0) {
                    activityContainer.innerHTML = `
                        <div class="text-center py-5 text-muted">
                            <i class="bi bi-inbox" style="font-size: 3rem;"></i>
                            <p class="mt-3">No recent activity yet</p>
                            <p class="small">Create your first receiving batch to see activity here</p>
                        </div>
                    `;
                    return;
                }

                // Render batches
                activityContainer.innerHTML = batches.map(batch => {
                    const date = new Date(batch.createdAt);
                    const statusBadgeClass = batch.status === 'Completed' ? 'bg-success' 
                        : batch.status === 'Cancelled' ? 'bg-secondary'
                        : 'bg-warning';
                    
                    return `
                        <div class="list-group-item">
                            <div class="d-flex w-100 justify-content-between align-items-start">
                                <div class="flex-grow-1">
                                    <h6 class="mb-1">
                                        ${batch.sourceTypeName || batch.sourceType}
                                        ${batch.documentNumber ? ` - ${batch.documentNumber}` : ''}
                                    </h6>
                                    <p class="mb-1">
                                        <strong>School/Supplier:</strong> ${batch.schoolName || 'N/A'}<br>
                                        <strong>Devices:</strong> ${batch.deviceCount || 0}
                                    </p>
                                    <small class="text-muted">
                                        ${date.toLocaleDateString()} ${date.toLocaleTimeString()}
                                    </small>
                                </div>
                                <div class="ms-3">
                                    <span class="badge ${statusBadgeClass}">${batch.statusName || batch.status}</span>
                                </div>
                            </div>
                        </div>
                    `;
                }).join('');
            } else {
                const errorText = await response.text().catch(() => 'Unknown error');
                console.error('Failed to load recent activity:', response.status, response.statusText, errorText.substring(0, 200));
                activityContainer.innerHTML = `
                    <div class="text-center py-5 text-muted">
                        <i class="bi bi-exclamation-triangle" style="font-size: 3rem;"></i>
                        <p class="mt-3">Failed to load recent activity</p>
                        <p class="small">Status: ${response.status} ${response.statusText}</p>
                        <p class="small">Please refresh the page or check console for details</p>
                    </div>
                `;
            }
        } catch (err) {
            console.error('Error loading recent activity:', err);
            const errorMsg = err.message || 'Unknown error';
            activityContainer.innerHTML = `
                <div class="text-center py-5 text-muted">
                    <i class="bi bi-exclamation-triangle" style="font-size: 3rem;"></i>
                    <p class="mt-3">Error loading recent activity</p>
                    <p class="small">${errorMsg}</p>
                    <p class="small">Check browser console for details</p>
                </div>
            `;
        }
    }

    function showToast(title, message, type) {
        // Create toast element
        const toastHtml = `
            <div class="toast align-items-center text-white bg-${type} border-0" role="alert" aria-live="assertive" aria-atomic="true">
                <div class="d-flex">
                    <div class="toast-body">
                        <strong>${title}</strong><br>${message}
                    </div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
                </div>
            </div>
        `;

        // Create container if it doesn't exist
        let container = document.getElementById('toastContainer');
        if (!container) {
            container = document.createElement('div');
            container.id = 'toastContainer';
            container.className = 'toast-container position-fixed top-0 end-0 p-3';
            container.style.zIndex = '9999';
            document.body.appendChild(container);
        }

        // Add toast
        container.insertAdjacentHTML('beforeend', toastHtml);
        const toastElement = container.lastElementChild;
        const toast = new bootstrap.Toast(toastElement, { delay: 4000 });
        toast.show();

        // Remove after hidden
        toastElement.addEventListener('hidden.bs.toast', () => {
            toastElement.remove();
        });
    }
})();

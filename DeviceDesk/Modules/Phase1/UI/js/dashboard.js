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
                        <div class="empty-state">
                            <div class="empty-state-icon">
                                <i class="bi bi-inbox"></i>
                            </div>
                            <h5>No Recent Activity</h5>
                            <p>Create your first receiving batch to see activity here</p>
                        </div>
                    `;
                    return;
                }

                // Render batches with enhanced design
                activityContainer.innerHTML = batches.map(batch => {
                    const date = new Date(batch.createdAt);
                    
                    // Determine icon and styling based on source type
                    let iconClass = 'new-stock';
                    let icon = 'bi-box-seam';
                    let typeName = batch.sourceTypeName || batch.sourceType;
                    
                    if (typeName.toLowerCase().includes('rnr emergency') || typeName.toLowerCase().includes('emergency')) {
                        iconClass = 'rnr-emergency';
                        icon = 'bi-lightning-fill';
                    } else if (typeName.toLowerCase().includes('rnr') || typeName.toLowerCase().includes('normal')) {
                        iconClass = 'rnr-normal';
                        icon = 'bi-arrow-repeat';
                    }
                    
                    // Determine status badge
                    let statusClass = 'badge-pending';
                    let statusText = batch.statusName || batch.status || 'Unknown';
                    
                    if (statusText.toLowerCase().includes('complet')) {
                        statusClass = 'badge-completed';
                    } else if (statusText.toLowerCase().includes('progress') || statusText.toLowerCase().includes('scanning') || statusText.toLowerCase().includes('verif')) {
                        statusClass = 'badge-in-progress';
                    } else if (statusText.toLowerCase().includes('cancel')) {
                        statusClass = 'badge-cancelled';
                    }
                    
                    // Format document number
                    const documentNumber = batch.documentNumber || batch.batchNumber || 'N/A';
                    
                    return `
                        <div class="activity-item">
                            <div class="activity-header">
                                <div class="activity-type">
                                    <div class="activity-type-icon ${iconClass}">
                                        <i class="bi ${icon}"></i>
                                    </div>
                                    <div>
                                        <div style="font-weight: 600; color: #2c3e50;">${typeName}</div>
                                        <div style="font-size: 0.85rem; color: #6c757d;">${documentNumber}</div>
                                    </div>
                                </div>
                                <span class="activity-badge ${statusClass}">${statusText}</span>
                            </div>
                            
                            <div class="activity-details">
                                <div class="activity-detail-item">
                                    <div class="activity-detail-label">School/Supplier</div>
                                    <div class="activity-detail-value">${batch.schoolName || 'N/A'}</div>
                                </div>
                                <div class="activity-detail-item">
                                    <div class="activity-detail-label">Devices</div>
                                    <div class="activity-detail-value">
                                        <i class="bi bi-box text-primary me-1"></i>${batch.deviceCount || 0}
                                    </div>
                                </div>
                            </div>
                            
                            <div class="activity-timestamp">
                                <i class="bi bi-clock"></i>
                                <span>${formatDateTime(date)}</span>
                            </div>
                        </div>
                    `;
                }).join('');
            } else {
                const errorText = await response.text().catch(() => 'Unknown error');
                console.error('Failed to load recent activity:', response.status, response.statusText, errorText.substring(0, 200));
                activityContainer.innerHTML = `
                    <div class="empty-state">
                        <div class="empty-state-icon">
                            <i class="bi bi-exclamation-triangle text-warning"></i>
                        </div>
                        <h5>Failed to Load Activity</h5>
                        <p class="small">Status: ${response.status} ${response.statusText}</p>
                        <p class="small">Please refresh the page or check console for details</p>
                    </div>
                `;
            }
        } catch (err) {
            console.error('Error loading recent activity:', err);
            const errorMsg = err.message || 'Unknown error';
            activityContainer.innerHTML = `
                <div class="empty-state">
                    <div class="empty-state-icon">
                        <i class="bi bi-exclamation-triangle text-danger"></i>
                    </div>
                    <h5>Error Loading Activity</h5>
                    <p class="small">${errorMsg}</p>
                    <p class="small">Check browser console for details</p>
                </div>
            `;
        }
    }

    function formatDateTime(date) {
        const now = new Date();
        const diffMs = now - date;
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMs / 3600000);
        const diffDays = Math.floor(diffMs / 86400000);

        if (diffMins < 1) {
            return 'Just now';
        } else if (diffMins < 60) {
            return `${diffMins} minute${diffMins > 1 ? 's' : ''} ago`;
        } else if (diffHours < 24) {
            return `${diffHours} hour${diffHours > 1 ? 's' : ''} ago`;
        } else if (diffDays < 7) {
            return `${diffDays} day${diffDays > 1 ? 's' : ''} ago`;
        } else {
            return date.toLocaleDateString('en-US', { 
                month: 'short', 
                day: 'numeric', 
                year: 'numeric',
                hour: '2-digit',
                minute: '2-digit'
            });
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

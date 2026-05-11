(() => {
    const API_BASE = `${location.origin}/api/phase1/receiving`;
    
    // Get batch ID from URL parameters
    const urlParams = new URLSearchParams(window.location.search);
    const batchId = urlParams.get('batchId');
    
    if (!batchId) {
        alert('No batch ID provided. Redirecting to dashboard.');
        window.location.href = '/phase1/dashboard.html';
        return;
    }

    // Elements
    const uploadZone = document.getElementById('uploadZone');
    const fileInput = document.getElementById('fileInput');
    const spreadsheetInput = document.getElementById('spreadsheetInput');
    const spreadsheetResult = document.getElementById('spreadsheetResult');
    const docTypeSelect = document.getElementById('docType');
    const uploadProgress = document.getElementById('uploadProgress');
    const filesList = document.getElementById('filesList');
    const continueBtn = document.getElementById('continueBtn');
    const skipBtn = document.getElementById('skipBtn');

    let uploadedDocuments = [];
    let spreadsheetData = null;
    let isUploading = false;

    // Initialize page
    init();

    async function init() {
        await loadBatchInfo();
        await loadExistingDocuments();
        setupEventListeners();
    }

    async function loadBatchInfo() {
        try {
            const res = await fetch(`${API_BASE}/batches/${batchId}`);
            if (!res.ok) throw new Error('Failed to load batch info');
            
            const batch = await res.json();
            
            document.getElementById('batchId').textContent = batch.receivingBatchId;
            document.getElementById('sourceType').textContent = batch.sourceTypeName || 'Unknown';
            document.getElementById('createdAt').textContent = new Date(batch.createdAt).toLocaleString();
            document.getElementById('status').textContent = batch.statusName || 'Active';
            
        } catch (err) {
            console.error('Error loading batch info:', err);
            showAlert('Failed to load batch information', 'danger');
        }
    }

    async function loadExistingDocuments() {
        try {
            const res = await fetch(`${API_BASE}/batches/${batchId}/documents`);
            if (!res.ok) throw new Error('Failed to load documents');
            
            uploadedDocuments = await res.json();
            displayUploadedFiles();
            
        } catch (err) {
            console.error('Error loading documents:', err);
            // Don't show error for empty document list
        }
    }

    function setupEventListeners() {
        // File input change
        fileInput.addEventListener('change', handleFileSelect);
        
        // Spreadsheet input change
        spreadsheetInput.addEventListener('change', handleSpreadsheetSelect);
        
        // Drag and drop
        uploadZone.addEventListener('click', () => fileInput.click());
        uploadZone.addEventListener('dragover', handleDragOver);
        uploadZone.addEventListener('dragleave', handleDragLeave);
        uploadZone.addEventListener('drop', handleDrop);
        
        // Navigation buttons
        continueBtn.addEventListener('click', () => {
            window.location.href = `/phase1/scanning.html?batchId=${batchId}`;
        });
        
        skipBtn.addEventListener('click', () => {
            if (confirm('Skip document upload? You can add documents later from the batch details page.')) {
                window.location.href = `/phase1/scanning.html?batchId=${batchId}`;
            }
        });
    }

    function handleFileSelect(e) {
        const files = Array.from(e.target.files);
        if (files.length > 0) {
            uploadFiles(files);
        }
    }

    function handleDragOver(e) {
        e.preventDefault();
        uploadZone.classList.add('dragover');
    }

    function handleDragLeave(e) {
        e.preventDefault();
        uploadZone.classList.remove('dragover');
    }

    function handleDrop(e) {
        e.preventDefault();
        uploadZone.classList.remove('dragover');
        
        const files = Array.from(e.dataTransfer.files);
        if (files.length > 0) {
            uploadFiles(files);
        }
    }

    async function uploadFiles(files) {
        if (isUploading) {
            showAlert('Upload in progress, please wait...', 'warning');
            return;
        }

        isUploading = true;
        const docType = docTypeSelect.value;
        
        try {
            showUploadProgress(0);
            
            for (let i = 0; i < files.length; i++) {
                const file = files[i];
                const progress = ((i + 1) / files.length) * 100;
                
                await uploadSingleFile(file, docType);
                showUploadProgress(progress);
            }
            
            showAlert(`Successfully uploaded ${files.length} file(s)!`, 'success');
            await loadExistingDocuments(); // Refresh the list
            
        } catch (err) {
            console.error('Upload error:', err);
            showAlert('Upload failed: ' + err.message, 'danger');
        } finally {
            isUploading = false;
            setTimeout(() => {
                uploadProgress.style.display = 'none';
            }, 2000);
        }
    }

    async function uploadSingleFile(file, docType) {
        const formData = new FormData();
        formData.append('file', file);

        const res = await fetch(`${API_BASE}/batches/${batchId}/documents?docType=${docType}`, {
            method: 'POST',
            body: formData
        });

        const data = await res.json();
        
        if (!res.ok) {
            throw new Error(data.error || `Failed to upload ${file.name}`);
        }

        return data;
    }

    function showUploadProgress(percent) {
        uploadProgress.style.display = 'block';
        const progressBar = uploadProgress.querySelector('.progress-bar');
        progressBar.style.width = percent + '%';
        progressBar.textContent = Math.round(percent) + '%';
    }

    function displayUploadedFiles() {
        if (uploadedDocuments.length === 0) {
            filesList.innerHTML = '<p class="text-muted">No documents uploaded yet.</p>';
            return;
        }

        const html = uploadedDocuments.map(doc => `
            <div class="file-item d-flex justify-content-between align-items-center">
                <div class="d-flex align-items-center">
                    <i class="bi bi-file-earmark-text fs-4 me-3 text-primary"></i>
                    <div>
                        <h6 class="mb-1">${doc.fileName}</h6>
                        <small class="text-muted">
                            <span class="badge bg-secondary me-2">${doc.docType}</span>
                            ${formatFileSize(doc.fileSizeBytes)} • 
                            ${new Date(doc.uploadedAt).toLocaleString()}
                        </small>
                    </div>
                </div>
                <div class="btn-group">
                    <button type="button" class="btn btn-sm btn-outline-primary" 
                            onclick="downloadDocument(${doc.documentId})" title="Download">
                        <i class="bi bi-download"></i>
                    </button>
                    <button type="button" class="btn btn-sm btn-outline-danger" 
                            onclick="deleteDocument(${doc.documentId})" title="Delete">
                        <i class="bi bi-trash"></i>
                    </button>
                </div>
            </div>
        `).join('');

        filesList.innerHTML = html;
    }

    function formatFileSize(bytes) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    }

    // Global functions for button clicks
    window.downloadDocument = function(documentId) {
        window.open(`${API_BASE}/documents/${documentId}/download`, '_blank');
    };

    window.deleteDocument = async function(documentId) {
        if (!confirm('Are you sure you want to delete this document?')) return;
        
        try {
            const res = await fetch(`${API_BASE}/documents/${documentId}`, {
                method: 'DELETE'
            });
            
            if (!res.ok) {
                const data = await res.json();
                throw new Error(data.error || 'Failed to delete document');
            }
            
            showAlert('Document deleted successfully', 'success');
            await loadExistingDocuments(); // Refresh the list
            
        } catch (err) {
            console.error('Delete error:', err);
            showAlert('Failed to delete document: ' + err.message, 'danger');
        }
    };

    async function handleSpreadsheetSelect(e) {
        const file = e.target.files[0];
        if (!file) return;

        await uploadSpreadsheet(file);
    }

    async function uploadSpreadsheet(file) {
        if (isUploading) {
            showAlert('Upload in progress, please wait...', 'warning');
            return;
        }

        isUploading = true;
        
        try {
            showAlert('Parsing spreadsheet...', 'info');
            
            const formData = new FormData();
            formData.append('file', file);

            const res = await fetch(`${API_BASE}/batches/${batchId}/spreadsheet`, {
                method: 'POST',
                body: formData
            });

            const data = await res.json();
            
            if (!res.ok) {
                throw new Error(data.error || `Failed to upload ${file.name}`);
            }

            spreadsheetData = data;
            displaySpreadsheetResult(data);
            showAlert(`Spreadsheet parsed successfully! ${data.validRows} devices found.`, 'success');
            
            // Refresh document list
            await loadExistingDocuments();
            
        } catch (err) {
            console.error('Spreadsheet upload error:', err);
            showAlert('Spreadsheet upload failed: ' + err.message, 'danger');
        } finally {
            isUploading = false;
            spreadsheetInput.value = ''; // Reset input
        }
    }

    function displaySpreadsheetResult(data) {
        spreadsheetResult.style.display = 'block';
        
        let html = `
            <div class="card border-success">
                <div class="card-body">
                    <h6 class="card-title text-success">
                        <i class="bi bi-check-circle"></i> Spreadsheet Parsed Successfully
                    </h6>
                    <div class="row">
                        <div class="col-md-4">
                            <p class="mb-1"><strong>Total Rows:</strong> ${data.totalRows}</p>
                        </div>
                        <div class="col-md-4">
                            <p class="mb-1"><strong>Valid Devices:</strong> ${data.validRows}</p>
                        </div>
                        <div class="col-md-4">
                            <p class="mb-1"><strong>Errors:</strong> ${data.errors.length}</p>
                        </div>
                    </div>
        `;
        
        if (data.errors && data.errors.length > 0) {
            html += `
                <div class="alert alert-warning mt-3 mb-0">
                    <strong>Warnings:</strong>
                    <ul class="mb-0 mt-2">
                        ${data.errors.slice(0, 5).map(err => `<li>${err}</li>`).join('')}
                        ${data.errors.length > 5 ? `<li><em>...and ${data.errors.length - 5} more</em></li>` : ''}
                    </ul>
                </div>
            `;
        }
        
        html += `
                    <div class="mt-3">
                        <button type="button" class="btn btn-sm btn-outline-primary" onclick="viewSpreadsheetDevices()">
                            <i class="bi bi-list"></i> View Devices (${data.validRows})
                        </button>
                    </div>
                </div>
            </div>
        `;
        
        spreadsheetResult.innerHTML = html;
    }

    window.viewSpreadsheetDevices = function() {
        if (!spreadsheetData || !spreadsheetData.devices) {
            showAlert('No spreadsheet data available', 'warning');
            return;
        }

        const devices = spreadsheetData.devices;
        const modalHtml = `
            <div class="modal fade" id="devicesModal" tabindex="-1">
                <div class="modal-dialog modal-lg modal-dialog-scrollable">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title">Spreadsheet Devices (${devices.length})</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <div class="table-responsive">
                                <table class="table table-sm table-striped">
                                    <thead>
                                        <tr>
                                            <th>#</th>
                                            <th>Serial</th>
                                            <th>Brand</th>
                                            <th>Model</th>
                                            <th>Description</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        ${devices.map((d, i) => `
                                            <tr>
                                                <td>${i + 1}</td>
                                                <td><strong>${d.serial}</strong></td>
                                                <td>${d.brand || '-'}</td>
                                                <td>${d.model || '-'}</td>
                                                <td>${d.description || '-'}</td>
                                            </tr>
                                        `).join('')}
                                    </tbody>
                                </table>
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        // Remove existing modal if any
        const existingModal = document.getElementById('devicesModal');
        if (existingModal) existingModal.remove();

        // Add modal to body
        document.body.insertAdjacentHTML('beforeend', modalHtml);

        // Show modal
        const modal = new bootstrap.Modal(document.getElementById('devicesModal'));
        modal.show();
    };

    function showAlert(message, type) {
        // Create alert element if it doesn't exist
        let alertContainer = document.getElementById('alertContainer');
        if (!alertContainer) {
            alertContainer = document.createElement('div');
            alertContainer.id = 'alertContainer';
            alertContainer.className = 'position-fixed top-0 end-0 p-3';
            alertContainer.style.zIndex = '1050';
            document.body.appendChild(alertContainer);
        }

        const alertId = 'alert-' + Date.now();
        const alertHtml = `
            <div id="${alertId}" class="alert alert-${type} alert-dismissible fade show" role="alert">
                ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        `;
        
        alertContainer.insertAdjacentHTML('beforeend', alertHtml);
        
        // Auto-remove after 5 seconds
        setTimeout(() => {
            const alertElement = document.getElementById(alertId);
            if (alertElement) {
                alertElement.remove();
            }
        }, 5000);
    }
})();

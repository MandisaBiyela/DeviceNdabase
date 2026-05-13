(() => {
    const API_BASE = `${location.origin}/api/phase1/receiving`;
    
    let selectedSourceType = null;
    let selectedOrderId = null;
    let selectedSlipId = null;
    let orders = [];
    let slips = [];

    // Elements
    const step1 = document.getElementById('step1');
    const step2 = document.getElementById('step2');
    const step3 = document.getElementById('step3');
    const sourceLabel = document.getElementById('sourceLabel');
    const orderSelection = document.getElementById('orderSelection');
    const slipSelection = document.getElementById('slipSelection');
    const orderIdSelect = document.getElementById('orderId');
    const collectionSlipIdSelect = document.getElementById('collectionSlipId');
    const orderDetails = document.getElementById('orderDetails');
    const slipDetails = document.getElementById('slipDetails');
    const alertMsg = document.getElementById('alertMsg');
    const workflowProgress = document.getElementById('workflowProgress');
    
    let currentBatchId = null;

    // Handle hash navigation for New Stock. Accept "#newstock" or
    // "#newstock?batchId=<guid>" (document-ingest deep link).
    const rawHash = window.location.hash || '';
    if (rawHash.startsWith('#newstock')) {
        selectedSourceType = 1;
        const hashQuery = rawHash.includes('?') ? rawHash.substring(rawHash.indexOf('?')) : '';
        if (hashQuery) {
            const hp = new URLSearchParams(hashQuery);
            const b = hp.get('batchId');
            if (b) {
                // Re-write the location so loadOrders() pre-selects this batch
                const newSearch = location.search
                    ? location.search + '&batchId=' + encodeURIComponent(b)
                    : '?batchId=' + encodeURIComponent(b);
                history.replaceState(null, '', location.pathname + newSearch + '#newstock');
            }
        }
        showStep2();
    }
    
    // Step 1: Source Type Selection (legacy support)
    document.querySelectorAll('.select-source').forEach(btn => {
        btn.addEventListener('click', (e) => {
            const card = e.target.closest('.source-card');
            selectedSourceType = parseInt(card.dataset.source);
            showStep2();
        });
    });

    async function showStep2() {
        step1.style.display = 'none';
        step2.style.display = 'block';

        if (selectedSourceType === 1) {
            // New Stock - Show workflow progress
            workflowProgress.style.display = 'flex';
            updateWorkflowProgress(1); // Step 1: Invoice Upload
            updateWorkflowLabels('new-stock');
            
            sourceLabel.textContent = 'Order/Invoice';
            orderSelection.style.display = 'block';
            slipSelection.style.display = 'none';
            // Set required attribute
            orderIdSelect.setAttribute('required', 'required');
            collectionSlipIdSelect.removeAttribute('required');
            await loadOrders();
        } else {
            // RnR - Show workflow progress for R&R
            workflowProgress.style.display = 'flex';
            updateWorkflowProgress(1); // Step 1: Batch Setup
            updateWorkflowLabels('rnr');
            
            sourceLabel.textContent = 'Collection Slip';
            orderSelection.style.display = 'none';
            slipSelection.style.display = 'block';
            // Set required attribute
            collectionSlipIdSelect.setAttribute('required', 'required');
            orderIdSelect.removeAttribute('required');
            await loadCollectionSlips(selectedSourceType);
        }
    }

    function updateWorkflowProgress(currentStep) {
        // Reset all steps
        for (let i = 1; i <= 4; i++) {
            const stepEl = document.getElementById(`ws${i}`);
            stepEl.classList.remove('active', 'completed');
            
            if (i < currentStep) {
                stepEl.classList.add('completed');
            } else if (i === currentStep) {
                stepEl.classList.add('active');
            }
        }
    }

    function updateWorkflowLabels(workflowType) {
        if (workflowType === 'new-stock') {
            document.querySelector('#ws1 .workflow-label').textContent = 'Invoice Upload';
            document.querySelector('#ws2 .workflow-label').textContent = 'Verification';
            document.querySelector('#ws3 .workflow-label').textContent = 'Reconciliation';
            document.querySelector('#ws4 .workflow-label').textContent = 'GRV Generation';
        } else if (workflowType === 'rnr') {
            if (selectedSourceType === 3) {
                // Emergency R&R
                document.querySelector('#ws1 .workflow-label').textContent = 'Emergency + Loan';
                document.querySelector('#ws2 .workflow-label').textContent = 'Device Scanning';
                document.querySelector('#ws3 .workflow-label').textContent = 'Verification';
                document.querySelector('#ws4 .workflow-label').textContent = 'Emergency GRV';
            } else {
                // Normal R&R
                document.querySelector('#ws1 .workflow-label').textContent = 'Batch Setup';
                document.querySelector('#ws2 .workflow-label').textContent = 'Device Scanning';
                document.querySelector('#ws3 .workflow-label').textContent = 'Verification';
                document.querySelector('#ws4 .workflow-label').textContent = 'GRV & Handover';
            }
        }
    }

    async function loadOrders() {
        try {
            const res = await fetch(`${API_BASE}/orders`);
            if (!res.ok) throw new Error('Failed to load orders');
            orders = await res.json();

            console.log('[Phase 1] Loaded orders from Phase 0:', orders);

            orderIdSelect.innerHTML = '<option value="">-- Select Order --</option>';
            orders.forEach(o => {
                const opt = document.createElement('option');
                opt.value = o.orderId;
                opt.textContent = formatOrderOption(o);
                orderIdSelect.appendChild(opt);
            });

            // Pre-select from ?batchId= query param when navigating here from
            // document-ingest or the order detail page.
            const params = new URLSearchParams(location.search);
            const preselect = params.get('batchId') || params.get('id');
            if (preselect && orders.some(o => o.orderId === preselect)) {
                orderIdSelect.value = preselect;
                orderIdSelect.dispatchEvent(new Event('change'));
            }

            if (orders.length === 0) {
                showAlert('No approved orders found in Phase 0. Please create and approve an order first.', 'warning');
            }
        } catch (err) {
            console.error('[Phase 1] Error loading orders:', err);
            showAlert('Error loading orders: ' + err.message, 'danger');
        }
    }

    function formatOrderOption(o) {
        // Prefer the procurement-order surface: "{PONumber} — {Project} ({FY}) — {n} devices"
        const po = o.poNumber || o.orderNumber;
        const project = o.projectName ? ` — ${o.projectName}` : '';
        const fy = o.financialYear ? ` (${o.financialYear})` : '';
        const qty = typeof o.totalQuantity === 'number' ? ` — ${o.totalQuantity} device${o.totalQuantity === 1 ? '' : 's'}` : '';
        return `${po}${project}${fy}${qty}`;
    }

    function escapeHtml(s) {
        const d = document.createElement('div');
        d.textContent = s == null ? '' : String(s);
        return d.innerHTML;
    }

    async function loadCollectionSlips(sourceType) {
        try {
            const res = await fetch(`${API_BASE}/collection-slips?sourceType=${sourceType}`);
            if (!res.ok) throw new Error('Failed to load collection slips');
            slips = await res.json();
            
            collectionSlipIdSelect.innerHTML = '<option value="">-- Select Collection Slip --</option>';
            slips.forEach(s => {
                const opt = document.createElement('option');
                opt.value = s.collectionSlipId;
                opt.textContent = `${s.slipNumber} - ${s.schoolName} (${s.emisCode})`;
                collectionSlipIdSelect.appendChild(opt);
            });
        } catch (err) {
            showAlert('Error loading collection slips: ' + err.message, 'danger');
        }
    }

    // Order selection change
    orderIdSelect.addEventListener('change', (e) => {
        const orderId = e.target.value;
        if (!orderId) {
            orderDetails.style.display = 'none';
            return;
        }

        const order = orders.find(o => o.orderId === orderId);
        if (order) {
            const schools = Array.isArray(order.schoolNames) && order.schoolNames.length
                ? order.schoolNames
                : null;
            const schoolsHtml = schools
                ? `<ul class="mb-2">${schools.map(s => `<li>${escapeHtml(s)}</li>`).join('')}</ul>`
                : '<p class="text-muted small mb-2">No school breakdown available.</p>';

            document.getElementById('orderDetailsContent').innerHTML = `
                <p><strong>PO Number:</strong> ${escapeHtml(order.poNumber || order.orderNumber)}</p>
                <p><strong>Project:</strong> ${escapeHtml(order.projectName || 'N/A')}</p>
                <p><strong>Financial Year:</strong> ${escapeHtml(order.financialYear || 'N/A')}</p>
                <p><strong>Supplier:</strong> ${escapeHtml(order.supplierName || 'N/A')}</p>
                <p><strong>Invoice:</strong> ${escapeHtml(order.invoiceNumber || '— set during receiving')}</p>
                <p><strong>Order Date:</strong> ${new Date(order.orderDate).toLocaleDateString()}</p>
                <p><strong>Status:</strong> ${escapeHtml(order.status)}</p>
                <p><strong>Total Expected:</strong> ${order.totalQuantity}</p>
                <p><strong>Schools (${order.schoolCount || (schools ? schools.length : 0)}):</strong></p>
                ${schoolsHtml}
            `;
            orderDetails.style.display = 'block';
            selectedOrderId = orderId;

            // Pre-fill optional fields the receiving form already exposes
            const supplierInp = document.getElementById('supplierName');
            if (supplierInp && order.supplierName) supplierInp.value = order.supplierName;
            const invoiceInp = document.getElementById('invoiceNumber');
            if (invoiceInp && order.invoiceNumber) invoiceInp.value = order.invoiceNumber;
        }
    });

    // Collection slip selection change
    collectionSlipIdSelect.addEventListener('change', (e) => {
        const slipId = e.target.value;
        if (!slipId) {
            slipDetails.style.display = 'none';
            return;
        }

        const slip = slips.find(s => s.collectionSlipId === slipId);
        if (slip) {
            document.getElementById('slipDetailsContent').innerHTML = `
                <p><strong>Slip Number:</strong> ${slip.slipNumber}</p>
                <p><strong>School:</strong> ${slip.schoolName} (${slip.emisCode})</p>
                <p><strong>Type:</strong> ${slip.sourceTypeName}</p>
                <p><strong>Collection Date:</strong> ${new Date(slip.collectionDate).toLocaleDateString()}</p>
                <p><strong>Collected By:</strong> ${slip.collectedBy || 'N/A'}</p>
            `;
            slipDetails.style.display = 'block';
            selectedSlipId = slipId;
        }
    });

    // Navigation buttons
    document.getElementById('backBtn').addEventListener('click', () => {
        step2.style.display = 'none';
        step1.style.display = 'block';
    });

    document.getElementById('nextBtn').addEventListener('click', () => {
        if (selectedSourceType === 1 && !selectedOrderId) {
            showAlert('Please select an order', 'warning');
            return;
        }
        if (selectedSourceType !== 1 && !selectedSlipId) {
            showAlert('Please select a collection slip', 'warning');
            return;
        }

        step2.style.display = 'none';
        step3.style.display = 'block';
    });

    document.getElementById('backBtn2').addEventListener('click', () => {
        step3.style.display = 'none';
        step2.style.display = 'block';
    });

    // Form submission
    document.getElementById('receivingForm').addEventListener('submit', async (e) => {
        e.preventDefault();
        
        console.log('Form submitted');
        console.log('Selected source type:', selectedSourceType);
        console.log('Selected order ID:', selectedOrderId);
        console.log('Selected slip ID:', selectedSlipId);
        
        const receivedBy = document.getElementById('receivedBy').value.trim();
        const notes = document.getElementById('notes').value.trim();
        const supplierEl = document.getElementById('supplierName');
        const invoiceEl = document.getElementById('invoiceNumber');
        const supplierName = supplierEl ? supplierEl.value.trim() : '';
        const invoiceNumber = invoiceEl ? invoiceEl.value.trim() : '';

        const payload = {
            sourceType: selectedSourceType,
            orderId: selectedSourceType === 1 ? selectedOrderId : null,
            collectionSlipId: selectedSourceType !== 1 ? selectedSlipId : null,
            schoolId: null, // Will be populated from collection slip on server
            receivedBy: receivedBy || null,
            notes: notes || null,
            supplierName: supplierName || null,
            invoiceNumber: invoiceNumber || null
        };

        console.log('Payload:', JSON.stringify(payload, null, 2));

        try {
            showAlert('Creating batch...', 'info');
            
            const res = await fetch(`${API_BASE}/batches`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            console.log('Response status:', res.status);
            
            if (!res.ok) {
                const errorData = await res.json().catch(() => ({ error: 'Failed to create receiving batch' }));
                throw new Error(errorData.error || 'Failed to create receiving batch');
            }

            const data = await res.json();
            
            console.log('=== CREATE RECEIVING BATCH RESULT ===');
            console.log('Payload orderId (from dropdown):', payload.orderId);
            console.log('Response.newStockBatchId:', data.newStockBatchId);
            console.log('Response.NewStockBatchId:', data.NewStockBatchId);
            console.log('Response.orderId:', data.orderId);
            console.log('Response data keys:', Object.keys(data));

            showAlert('Receiving batch created successfully!', 'success');
            
            // Store batch ID for file uploads
            currentBatchId = data.receivingBatchId;
            
            if (selectedSourceType === 1) {
                // New Stock Workflow - Auto-generate Blind Copy and proceed to model-driven scanning
                updateWorkflowProgress(2); // Step 2: Physical Verification
                
                // Resolve newStockBatchId with fallback chain
                let newStockBatchId =
                    data.newStockBatchId ||
                    data.NewStockBatchId ||
                    payload.orderId ||    // value from the <select> (most reliable)
                    data.orderId || null;

                console.log('Resolved newStockBatchId:', newStockBatchId);

                // NUCLEAR VALIDATION: Must be a valid GUID
                const guidRegex = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

                if (!newStockBatchId || !guidRegex.test(newStockBatchId)) {
                    const errorMsg = 'Could not determine New Stock Batch ID for model scanning. Please try again.';
                    showAlert(errorMsg, 'danger');
                    console.error('Invalid newStockBatchId:', newStockBatchId);
                    console.error('Response data:', JSON.stringify(data, null, 2));
                    console.error('Payload:', JSON.stringify(payload, null, 2));
                    throw new Error('Invalid newStockBatchId: ' + newStockBatchId);
                }
                
                // Auto-open Blind Copy in new tab
                window.open(`/api/phase1/receiving/batches/${data.receivingBatchId}/blind-copy`, '_blank');
                
                showAlert('Blind Copy generated! Redirecting to model scanning...', 'success');
                
                // Redirect to model-driven scanning page for New Stock (Phase 1 folder for permissions)
                // Use the NewStockBatchId which is the Phase 0 NewStockBatch ID
                const url = '/phase1/model-scanning.html?id=' + encodeURIComponent(newStockBatchId);
                console.log('Redirecting to:', url);
                setTimeout(() => {
                    window.location.href = url;
                }, 2000);
            } else {
                // RnR Workflow
                updateWorkflowProgress(2); // Step 2: Device Scanning
                
                if (selectedSourceType === 3) {
                    // Emergency R&R - Show loan unit assignment first
                    if (confirm('Emergency batch created! Proceed to loan unit assignment?')) {
                        window.location.href = `/phase1/emergency-loan.html?batchId=${data.receivingBatchId}`;
                    }
                } else {
                    // Normal R&R - Continue to scanning
                    if (confirm('Batch created! Print Collection Slip for Receiving Officer?')) {
                        window.open(`/api/phase1/receiving/batches/${data.receivingBatchId}/collection-slip`, '_blank');
                    }
                    
                    // Redirect to R&R scanning page
                    setTimeout(() => {
                        window.location.href = `/phase1/rnr-scanning.html?batchId=${data.receivingBatchId}`;
                    }, 2000);
                }
            }
        } catch (err) {
            console.error('Error creating batch:', err);
            showAlert('Error: ' + err.message, 'danger');
        }
    });

    function showAlert(message, type) {
        alertMsg.textContent = message;
        alertMsg.className = `alert alert-${type}`;
        alertMsg.style.display = 'block';
        setTimeout(() => {
            alertMsg.style.display = 'none';
        }, 5000);
    }
})();

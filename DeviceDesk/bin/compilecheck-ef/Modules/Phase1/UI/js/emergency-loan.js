(() => {
    const API_BASE = `${location.origin}/api/phase1/receiving`;
    
    let batchId = null;
    let batchData = null;
    let availableLoanUnits = [];
    let selectedLoanUnit = null;

    // Get batch ID from URL
    const urlParams = new URLSearchParams(window.location.search);
    batchId = urlParams.get('batchId');

    if (!batchId) {
        alert('No batch ID provided. Redirecting to dashboard.');
        window.location.href = '/phase1/dashboard.html';
        return;
    }

    // Elements
    const emergencyInfo = document.getElementById('emergencyInfo');
    const loanUnitsGrid = document.getElementById('loanUnitsGrid');
    const availableCount = document.getElementById('availableCount');
    const selectedLoanSummary = document.getElementById('selectedLoanSummary');
    const selectedLoanDetails = document.getElementById('selectedLoanDetails');
    const assignLoanBtn = document.getElementById('assignLoanBtn');
    
    // Form elements
    const replacementUser = document.getElementById('replacementUser');
    const emergencyReason = document.getElementById('emergencyReason');
    const expectedCompletionDate = document.getElementById('expectedCompletionDate');
    const expectedReturnDate = document.getElementById('expectedReturnDate');
    const emergencyNotes = document.getElementById('emergencyNotes');

    // Initialize
    init();

    async function init() {
        await loadBatchData();
        await loadAvailableLoanUnits();
        setupEventListeners();
        setDefaultDates();
    }

    function setupEventListeners() {
        assignLoanBtn.addEventListener('click', assignLoanUnit);
        
        // Form validation
        [replacementUser, emergencyReason, expectedCompletionDate, expectedReturnDate].forEach(element => {
            element.addEventListener('input', validateForm);
        });
    }

    function setDefaultDates() {
        const today = new Date();
        const nextWeek = new Date(today.getTime() + 7 * 24 * 60 * 60 * 1000);
        const twoWeeks = new Date(today.getTime() + 14 * 24 * 60 * 60 * 1000);
        
        expectedCompletionDate.value = nextWeek.toISOString().split('T')[0];
        expectedReturnDate.value = twoWeeks.toISOString().split('T')[0];
    }

    async function loadBatchData() {
        try {
            // Mock batch data for demonstration
            batchData = {
                receivingBatchId: batchId,
                sourceType: 3, // Emergency R&R
                status: 'Draft',
                createdAt: new Date().toISOString(),
                collectionSlip: {
                    slipNumber: 'CS-E-' + Math.floor(Math.random() * 1000),
                    schoolName: 'Hillcrest Primary School',
                    emisCode: 'EC12345',
                    schoolId: 'SCH001'
                }
            };
            
            displayEmergencyInfo();
        } catch (error) {
            console.error('Error loading batch data:', error);
            showAlert('danger', 'Failed to load batch data: ' + error.message);
        }
    }

    function displayEmergencyInfo() {
        emergencyInfo.innerHTML = `
            <div class="row">
                <div class="col-md-6">
                    <p class="mb-1"><strong>Batch ID:</strong> ${batchData.receivingBatchId}</p>
                    <p class="mb-1"><strong>Type:</strong> Emergency R&R</p>
                    <p class="mb-0"><strong>Priority:</strong> <span class="badge bg-danger">URGENT</span></p>
                </div>
                <div class="col-md-6">
                    <p class="mb-1"><strong>School:</strong> ${batchData.collectionSlip?.schoolName || 'N/A'}</p>
                    <p class="mb-1"><strong>EMIS Code:</strong> ${batchData.collectionSlip?.emisCode || 'N/A'}</p>
                    <p class="mb-0"><strong>Created:</strong> ${new Date(batchData.createdAt).toLocaleString()}</p>
                </div>
            </div>
        `;
    }

    async function loadAvailableLoanUnits() {
        try {
            // Mock data for demonstration - replace with actual API call when backend is implemented
            availableLoanUnits = [
                {
                    loanUnitId: 'LOAN001',
                    serialNumber: 'LU-TAB-001',
                    brand: 'Samsung',
                    model: 'Galaxy Tab A8',
                    condition: 'Excellent',
                    lastServicedDate: '2024-10-15',
                    notes: 'Recently serviced, ready for deployment'
                },
                {
                    loanUnitId: 'LOAN002',
                    serialNumber: 'LU-TAB-002',
                    brand: 'Lenovo',
                    model: 'Tab M10',
                    condition: 'Good',
                    lastServicedDate: '2024-10-10',
                    notes: 'Minor wear, fully functional'
                },
                {
                    loanUnitId: 'LOAN003',
                    serialNumber: 'LU-TAB-003',
                    brand: 'Samsung',
                    model: 'Galaxy Tab A7',
                    condition: 'Good',
                    lastServicedDate: '2024-09-28',
                    notes: 'Standard loan unit, tested and ready'
                },
                {
                    loanUnitId: 'LOAN004',
                    serialNumber: 'LU-TAB-004',
                    brand: 'Huawei',
                    model: 'MatePad T10s',
                    condition: 'Fair',
                    lastServicedDate: '2024-10-01',
                    notes: 'Some cosmetic wear, fully operational'
                }
            ];
            
            displayLoanUnits();
        } catch (error) {
            console.error('Error loading loan units:', error);
            loanUnitsGrid.innerHTML = `
                <div class="col-12">
                    <div class="alert alert-danger">
                        <i class="bi bi-exclamation-triangle"></i> 
                        Failed to load loan units: ${error.message}
                    </div>
                </div>
            `;
        }
    }

    function displayLoanUnits() {
        availableCount.textContent = availableLoanUnits.length;
        
        if (availableLoanUnits.length === 0) {
            loanUnitsGrid.innerHTML = `
                <div class="col-12">
                    <div class="alert alert-warning text-center">
                        <i class="bi bi-exclamation-triangle" style="font-size: 2rem;"></i>
                        <h5 class="mt-2">No Loan Units Available</h5>
                        <p class="mb-0">All loan units are currently assigned. Contact supervisor for emergency allocation.</p>
                    </div>
                </div>
            `;
            return;
        }

        loanUnitsGrid.innerHTML = availableLoanUnits.map(unit => `
            <div class="col-md-4 mb-3">
                <div class="loan-unit-card card h-100" data-unit-id="${unit.loanUnitId}" onclick="selectLoanUnit('${unit.loanUnitId}')">
                    <div class="card-body">
                        <div class="d-flex justify-content-between align-items-start mb-2">
                            <h6 class="card-title mb-0">${unit.brand} ${unit.model}</h6>
                            <span class="badge bg-success">Available</span>
                        </div>
                        <p class="card-text">
                            <small class="text-muted">
                                <strong>Serial:</strong> ${unit.serialNumber}<br>
                                <strong>Condition:</strong> ${unit.condition || 'Good'}<br>
                                <strong>Last Serviced:</strong> ${unit.lastServicedDate ? new Date(unit.lastServicedDate).toLocaleDateString() : 'N/A'}
                            </small>
                        </p>
                        <div class="mt-2">
                            <small class="text-muted">
                                <i class="bi bi-info-circle"></i> 
                                ${unit.notes || 'Ready for immediate deployment'}
                            </small>
                        </div>
                    </div>
                </div>
            </div>
        `).join('');
    }

    function selectLoanUnit(unitId) {
        // Remove previous selection
        document.querySelectorAll('.loan-unit-card').forEach(card => {
            card.classList.remove('selected');
        });
        
        // Select new unit
        const selectedCard = document.querySelector(`[data-unit-id="${unitId}"]`);
        selectedCard.classList.add('selected');
        
        selectedLoanUnit = availableLoanUnits.find(unit => unit.loanUnitId === unitId);
        displaySelectedLoanUnit();
        validateForm();
    }

    function displaySelectedLoanUnit() {
        if (!selectedLoanUnit) return;
        
        selectedLoanDetails.innerHTML = `
            <div class="row">
                <div class="col-md-6">
                    <h6>Selected Loan Unit</h6>
                    <p class="mb-1"><strong>Device:</strong> ${selectedLoanUnit.brand} ${selectedLoanUnit.model}</p>
                    <p class="mb-1"><strong>Serial Number:</strong> ${selectedLoanUnit.serialNumber}</p>
                    <p class="mb-0"><strong>Condition:</strong> ${selectedLoanUnit.condition || 'Good'}</p>
                </div>
                <div class="col-md-6">
                    <h6>Assignment Details</h6>
                    <p class="mb-1"><strong>School:</strong> ${batchData.collectionSlip?.schoolName || 'N/A'}</p>
                    <p class="mb-1"><strong>Status:</strong> <span class="badge bg-warning">Ready to Assign</span></p>
                    <p class="mb-0"><strong>Assignment Date:</strong> ${new Date().toLocaleDateString()}</p>
                </div>
            </div>
        `;
        
        selectedLoanSummary.style.display = 'block';
    }

    function validateForm() {
        const isFormValid = replacementUser.value.trim() && 
                           emergencyReason.value && 
                           expectedCompletionDate.value && 
                           expectedReturnDate.value && 
                           selectedLoanUnit;
        
        assignLoanBtn.disabled = !isFormValid;
    }

    async function assignLoanUnit() {
        if (!selectedLoanUnit) {
            showAlert('warning', 'Please select a loan unit first.');
            return;
        }

        const formData = {
            receivingBatchId: batchId,
            loanUnitId: selectedLoanUnit.loanUnitId,
            replacementUser: replacementUser.value.trim(),
            emergencyReason: emergencyReason.value,
            expectedCompletionDate: expectedCompletionDate.value,
            expectedReturnDate: expectedReturnDate.value,
            emergencyNotes: emergencyNotes.value.trim(),
            assignedBy: 'Receiving Clerk',
            schoolId: batchData.collectionSlip?.schoolId
        };

        try {
            showAlert('info', 'Assigning loan unit...');
            
            // Mock successful assignment for demonstration
            setTimeout(() => {
                const result = {
                    loanAssignmentId: 'LA-' + Date.now(),
                    success: true
                };
                
                showAlert('success', 'Loan unit assigned successfully! Generating emergency slip...');
                
                // Generate and print emergency slip
                if (confirm('Loan unit assigned! Print Emergency Slip for Receiving Officer?')) {
                    // Mock print - would open actual PDF in real implementation
                    showAlert('info', 'Emergency slip would be printed here (mock)');
                }
                
                // Redirect to emergency scanning page
                setTimeout(() => {
                    window.location.href = `/phase1/emergency-scanning.html?batchId=${batchId}&loanId=${result.loanAssignmentId}`;
                }, 2000);
            }, 1000);
            
        } catch (error) {
            console.error('Error assigning loan unit:', error);
            showAlert('danger', 'Error assigning loan unit: ' + error.message);
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

    // Expose function globally
    window.selectLoanUnit = selectLoanUnit;
})();

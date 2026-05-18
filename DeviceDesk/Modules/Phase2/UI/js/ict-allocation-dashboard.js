// ICT Allocator Dashboard - Student/Teacher Allocation Functions

let allocatorDevices = [];
let currentDevice = null;

// Event listener for radio button changes
document.addEventListener('change', function (e) {
    if (e.target.name === 'allocType') {
        updateAllocationFieldVisibility();
    }
});

function updateAllocationFieldVisibility() {
    const type = getSelectedAllocationType();
    const studentDiv = document.getElementById('studentFields');
    const teacherDiv = document.getElementById('teacherFields');

    if (studentDiv && teacherDiv) {
        studentDiv.style.display = (type === 1) ? 'block' : 'none';
        teacherDiv.style.display = (type === 2) ? 'block' : 'none';
    }
}

function getSelectedAllocationType() {
    const checked = document.querySelector('input[name="allocType"]:checked');
    return checked ? parseInt(checked.value, 10) : 0;
}

async function loadStudentTeacherAllocationView() {
    setActiveNav('navStudentTeacher');
    const main = document.getElementById('mainContent');
    main.innerHTML = `
        <div class="d-flex justify-content-between align-items-center mb-3">
            <div>
                <h4 class="mb-0">Student / Teacher Allocation</h4>
                <small class="text-muted">Receipted devices ready to be assigned to learners or teachers.</small>
            </div>
            <button class="btn btn-sm btn-outline-secondary" onclick="loadStudentTeacherAllocationView()">
                <i class="bi bi-arrow-clockwise"></i> Refresh
            </button>
        </div>
        <div id="allocatorTableContainer">
            <div class="text-muted">Loading devices...</div>
        </div>
    `;

    try {
        const resp = await fetch('/api/phase2/allocation/ready-for-assignment');
        if (!resp.ok) throw new Error('Failed to load devices');
        allocatorDevices = await resp.json();
        renderStudentTeacherAllocationTable();
    } catch (err) {
        document.getElementById('allocatorTableContainer').innerHTML =
            `<div class="alert alert-danger">Error loading devices: ${err.message}</div>`;
    }
}

function renderStudentTeacherAllocationTable() {
    const container = document.getElementById('allocatorTableContainer');
    if (!allocatorDevices.length) {
        container.innerHTML = `<div class="alert alert-info mb-0">
            No receipted devices pending allocation.
        </div>`;
        return;
    }

    const rows = allocatorDevices.map(d => {
        const allocationSummary = formatAllocationSummary(d);
        return `
<tr>
    <td class="text-nowrap">${d.serial}</td>
    <td>${d.schoolName ?? 'N/A'}</td>
    <td>${d.zone}</td>
    <td>${d.stage}</td>
    <td>${allocationSummary}</td>
    <td class="text-end">
        <button class="btn btn-sm btn-primary" onclick="openAllocationModal(${d.phase2DeviceId})">
            <i class="bi bi-pencil-square"></i> Allocate
        </button>
    </td>
</tr>`;
    }).join('');

    container.innerHTML = `
        <div class="table-responsive">
            <table class="table table-sm align-middle table-hover">
                <thead class="table-light">
                    <tr>
                        <th>Serial</th>
                        <th>School</th>
                        <th>Zone</th>
                        <th>Stage</th>
                        <th>Current Allocation</th>
                        <th class="text-end">Action</th>
                    </tr>
                </thead>
                <tbody>
                    ${rows}
                </tbody>
            </table>
        </div>
    `;
}

function formatAllocationSummary(d) {
    if (d.allocationType === 1) {
        return `<span class="badge bg-primary">Student</span> ${d.studentName ?? ''}`;
    }
    if (d.allocationType === 2) {
        return `<span class="badge bg-success">Teacher</span> ${d.teacherName ?? ''}`;
    }
    return '<span class="text-muted">Unallocated</span>';
}

function openAllocationModal(phase2DeviceId) {
    currentDevice = allocatorDevices.find(d => d.phase2DeviceId === phase2DeviceId);
    if (!currentDevice) return;

    document.getElementById('allocModalSerial').textContent = currentDevice.serial;
    document.getElementById('allocDeviceInfo').innerHTML = `
        <strong>${currentDevice.schoolName ?? 'Unknown school'}</strong><br/>
        Zone: ${currentDevice.zone} · Stage: ${currentDevice.stage}
    `;

    // Set radio based on current allocation
    if (currentDevice.allocationType === 1) {
        document.getElementById('allocStudent').checked = true;
    } else if (currentDevice.allocationType === 2) {
        document.getElementById('allocTeacher').checked = true;
    } else {
        document.getElementById('allocNone').checked = true;
    }

    // Pre-fill name/id fields
    document.getElementById('studentName').value = currentDevice.studentName ?? '';
    document.getElementById('studentId').value = currentDevice.studentIdNumber ?? '';
    document.getElementById('teacherName').value = currentDevice.teacherName ?? '';
    document.getElementById('teacherPersal').value = currentDevice.teacherPersalNumber ?? '';

    updateAllocationFieldVisibility();
    document.getElementById('allocError').classList.add('d-none');

    const modal = new bootstrap.Modal(document.getElementById('allocationModal'));
    modal.show();
}

async function saveAllocation() {
    if (!currentDevice) return;

    const type = getSelectedAllocationType();
    const errorDiv = document.getElementById('allocError');
    errorDiv.classList.add('d-none');
    errorDiv.textContent = '';

    // Simple validation
    if (type === 1) {
        const name = document.getElementById('studentName').value.trim();
        if (!name) {
            errorDiv.textContent = 'Please enter the student name.';
            errorDiv.classList.remove('d-none');
            return;
        }
    }
    if (type === 2) {
        const name = document.getElementById('teacherName').value.trim();
        const persal = document.getElementById('teacherPersal').value.trim();
        if (!name || !persal) {
            errorDiv.textContent = 'Please enter teacher name and persal number.';
            errorDiv.classList.remove('d-none');
            return;
        }
    }

    const payload = {
        allocationType: type,
        studentName: (type === 1) ? document.getElementById('studentName').value.trim() : null,
        studentIdNumber: (type === 1) ? document.getElementById('studentId').value.trim() : null,
        teacherName: (type === 2) ? document.getElementById('teacherName').value.trim() : null,
        teacherPersalNumber: (type === 2) ? document.getElementById('teacherPersal').value.trim() : null
    };

    try {
        const resp = await fetch(`/api/phase2/allocation/devices/${currentDevice.phase2DeviceId}/assign`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        if (!resp.ok) {
            const errorData = await resp.json().catch(() => ({ error: 'Failed to save allocation' }));
            throw new Error(errorData.error || 'Failed to save allocation');
        }

        // Close modal and reload list
        const modalEl = document.getElementById('allocationModal');
        const modal = bootstrap.Modal.getInstance(modalEl);
        modal.hide();

        await loadStudentTeacherAllocationView();
    } catch (err) {
        errorDiv.textContent = err.message;
        errorDiv.classList.remove('d-none');
    }
}

async function loadReadyForDispatchView() {
    setActiveNav('navReadyDispatch');
    const main = document.getElementById('mainContent');
    main.innerHTML = `
        <div class="d-flex justify-content-between align-items-center mb-3">
            <h4 class="mb-0">Ready for Dispatch</h4>
            <small class="text-muted">Allocated devices ready to be dispatched to schools.</small>
        </div>
        <div id="dispatchTableContainer">
            <div class="alert alert-info">
                This view will list allocated devices grouped by school and can be extended
                with export/print features.
            </div>
        </div>
    `;
}


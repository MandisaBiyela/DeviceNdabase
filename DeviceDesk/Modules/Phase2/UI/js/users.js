(() => {
    const API_BASE = `/api/phase2/users`;

    const tableBody = document.querySelector('#techniciansTable tbody');
    const emptyState = document.getElementById('emptyState');

    const createForm = document.getElementById('createTechnicianForm');
    const emailInput = document.getElementById('technicianEmail');
    const employeeNumberInput = document.getElementById('technicianEmployeeNumber');
    const fullNameInput = document.getElementById('technicianFullName');
    const createError = document.getElementById('createError');
    const createSpinner = document.getElementById('createSpinner');

    // Details modal elements
    const detailsModalEl = document.getElementById('technicianDetailsModal');
    const detailsForm = document.getElementById('technicianDetailsForm');
    const detailsUserId = document.getElementById('detailsUserId');
    const detailsEmployeeNumber = document.getElementById('detailsEmployeeNumber');
    const detailsEmail = document.getElementById('detailsEmail');
    const detailsFullName = document.getElementById('detailsFullName');
    const detailsError = document.getElementById('detailsError');
    const detailsSpinner = document.getElementById('detailsSpinner');
    const detailsDeleteBtn = document.getElementById('detailsDeleteBtn');
    const detailsDeleteSpinner = document.getElementById('detailsDeleteSpinner');

    let currentList = [];

    document.addEventListener('DOMContentLoaded', () => {
        // Skip client-side pre-auth check; backend enforces roles via cookies
        loadTechnicians();
        setupCreateForm();
        setupDetailsForm();
    });

    // Removed ensureClerkAuth preflight; API responses handle 401/403 and redirect

    async function loadTechnicians() {
        try {
            const res = await fetch(`${API_BASE}?role=IctTechnician`, { credentials: 'include' });

            if (res.status === 401 || res.status === 403) {
                alert('Your session has expired or lacks permissions. Please log in again.');
            window.location.href = '/login.html?logout=1';
                return;
            }

            if (!res.ok) {
                console.error('Failed to load technicians', await res.text());
                return;
            }

            const ct = (res.headers.get('content-type') || '').toLowerCase();
            if (!ct.includes('application/json')) {
                const text = await res.text();
                console.warn('Unexpected response for technicians list:', text.slice(0, 200));
                alert('Unexpected response from server. Please log in again.');
            window.location.href = '/login.html?logout=1';
                return;
            }

            const data = await res.json();
            currentList = Array.isArray(data) ? data : [];
            renderTechnicians(currentList);
        } catch (err) {
            console.error(err);
        }
    }

    function renderTechnicians(list) {
        tableBody.innerHTML = '';

        if (!list || list.length === 0) {
            emptyState.style.display = 'block';
            return;
        }

        emptyState.style.display = 'none';

        list.forEach(user => {
            const tr = document.createElement('tr');

            tr.innerHTML = `
                <td>${escapeHtml(user.employeeNumber || '')}</td>
                <td>${escapeHtml(user.email)}</td>
                <td>${escapeHtml(user.fullName)}</td>
                <td>${(user.roles || []).join(', ')}</td>
                <td>
                    <span class="badge ${user.isActive ? 'bg-success' : 'bg-secondary'}">
                        ${user.isActive ? 'Active' : 'Inactive'}
                    </span>
                </td>
                <td class="d-flex gap-1">
                    <button class="btn btn-sm btn-outline-secondary" title="View" data-action="view" data-id="${user.id}">
                        <i class="bi bi-eye"></i>
                    </button>
                    <button class="btn btn-sm btn-outline-primary" title="Edit" data-action="edit" data-id="${user.id}">
                        <i class="bi bi-pencil"></i>
                    </button>
                    <button class="btn btn-sm btn-outline-danger" title="Delete" data-action="delete" data-id="${user.id}">
                        <i class="bi bi-trash"></i>
                    </button>
                    ${user.isActive
                        ? `<button class="btn btn-sm btn-outline-danger" title="Deactivate" data-action="deactivate" data-id="${user.id}">
                               <i class="bi bi-person-dash"></i>
                           </button>`
                        : `<button class="btn btn-sm btn-outline-success" title="Reactivate" data-action="reactivate" data-id="${user.id}">
                               <i class="bi bi-person-check"></i>
                           </button>`
                    }
                </td>
            `;

            tableBody.appendChild(tr);
        });

        tableBody.querySelectorAll('button[data-action]').forEach(btn => {
            btn.addEventListener('click', () => {
                const id = btn.getAttribute('data-id');
                const action = btn.getAttribute('data-action');
                if (action === 'view') {
                    openDetails(id, 'view');
                } else if (action === 'edit') {
                    openDetails(id, 'edit');
                } else if (action === 'delete') {
                    if (confirm('Delete this technician account? This cannot be undone.')) {
                        deleteTechnician(id);
                    }
                } else if (action === 'deactivate') {
                    if (confirm('Deactivate this technician?')) {
                        toggleActive(id, false);
                    }
                } else if (action === 'reactivate') {
                    toggleActive(id, true);
                }
            });
        });
    }

    async function fetchUser(id) {
        const res = await fetch(`${API_BASE}/${id}`, { credentials: 'include' });
        if (!res.ok) throw new Error(await res.text());
        const ct = (res.headers.get('content-type') || '').toLowerCase();
        if (!ct.includes('application/json')) throw new Error('Unexpected response');
        return await res.json();
    }

    async function openDetails(id, mode) {
        try {
            const user = await fetchUser(id);
            detailsError.classList.add('d-none');
            detailsError.textContent = '';
            detailsUserId.value = user.id;
            detailsEmployeeNumber.value = user.employeeNumber || '';
            detailsEmail.value = user.email || '';
            detailsFullName.value = user.fullName || '';

            const readOnly = mode === 'view';
            detailsEmployeeNumber.readOnly = readOnly;
            detailsFullName.readOnly = readOnly;
            detailsForm.querySelector('#detailsSaveBtn').classList.toggle('d-none', readOnly);

            const modal = new bootstrap.Modal(detailsModalEl);
            modal.show();
        } catch (err) {
            console.error(err);
            alert('Failed to load user details.');
        }
    }

    async function deleteTechnician(id) {
        try {
            detailsDeleteSpinner.classList.remove('d-none');
            const res = await fetch(`${API_BASE}/${id}`, { method: 'DELETE', credentials: 'include' });
            detailsDeleteSpinner.classList.add('d-none');

            if (res.status === 401 || res.status === 403) {
                alert('Not authorized. Please log in again.');
            window.location.href = '/login.html?logout=1';
                return;
            }

            if (!res.ok) {
                const text = await res.text();
                alert(text || 'Failed to delete technician.');
                return;
            }

            const modal = bootstrap.Modal.getInstance(detailsModalEl);
            if (modal) modal.hide();
            await loadTechnicians();
        } catch (err) {
            console.error(err);
            alert('An error occurred while deleting technician.');
        }
    }

    async function toggleActive(id, active) {
        try {
            const url = `${API_BASE}/${id}/${active ? 'reactivate' : 'deactivate'}`;
            const res = await fetch(url, { method: 'POST', credentials: 'include' });

            if (res.status === 401 || res.status === 403) {
                alert('Not authorized. Please log in again.');
            window.location.href = '/login.html?logout=1';
                return;
            }

            if (!res.ok) {
                console.error('Failed to toggle active', await res.text());
                return;
            }

            await loadTechnicians();
        } catch (err) {
            console.error(err);
        }
    }

    function setupCreateForm() {
        createForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            createError.classList.add('d-none');
            createError.textContent = '';

            const payload = {
                email: emailInput.value.trim(),
                fullName: fullNameInput.value.trim(),
                employeeNumber: employeeNumberInput.value.trim()
            };

            if (!payload.email || !payload.fullName || !payload.employeeNumber) {
                createError.textContent = 'Email, Full Name and Employee Number are required.';
                createError.classList.remove('d-none');
                return;
            }

            try {
                createSpinner.classList.remove('d-none');

                const res = await fetch(API_BASE, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    credentials: 'include',
                    body: JSON.stringify(payload)
                });

                if (res.status === 401 || res.status === 403) {
                    createError.textContent = 'Not authorized. Please log in again.';
                    createError.classList.remove('d-none');
                    return;
                }

                if (!res.ok) {
                    const text = await res.text();
                    console.error('Create technician failed', text);
                    createError.textContent = text || 'Failed to create technician.';
                    createError.classList.remove('d-none');
                    return;
                }

                await loadTechnicians();
                const modalEl = document.getElementById('createTechnicianModal');
                const modal = bootstrap.Modal.getInstance(modalEl);
                modal.hide();
                createForm.reset();
                alert('Technician created. An email with login details has been sent.');
            } catch (err) {
                console.error(err);
                createError.textContent = 'An error occurred while creating technician.';
                createError.classList.remove('d-none');
            } finally {
                createSpinner.classList.add('d-none');
            }
        });
    }

    function setupDetailsForm() {
        // Save changes
        detailsForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            detailsError.classList.add('d-none');
            detailsError.textContent = '';

            const id = detailsUserId.value;
            const payload = {
                fullName: detailsFullName.value.trim(),
                employeeNumber: detailsEmployeeNumber.value.trim()
            };

            if (!payload.fullName || !payload.employeeNumber) {
                detailsError.textContent = 'Full Name and Employee Number are required.';
                detailsError.classList.remove('d-none');
                return;
            }

            try {
                detailsSpinner.classList.remove('d-none');
                const res = await fetch(`${API_BASE}/${encodeURIComponent(id)}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    credentials: 'include',
                    body: JSON.stringify(payload)
                });
                detailsSpinner.classList.add('d-none');

                if (res.status === 401 || res.status === 403) {
                    alert('Not authorized. Please log in again.');
                    window.location.href = '/login.html?logout=1';
                    return;
                }

                if (!res.ok) {
                    const text = await res.text();
                    detailsError.textContent = text || 'Failed to update technician.';
                    detailsError.classList.remove('d-none');
                    return;
                }

                const modal = bootstrap.Modal.getInstance(detailsModalEl);
                if (modal) modal.hide();
                await loadTechnicians();
            } catch (err) {
                console.error(err);
                detailsSpinner.classList.add('d-none');
                detailsError.textContent = 'An error occurred while updating technician.';
                detailsError.classList.remove('d-none');
            }
        });

        // Delete handler
        detailsDeleteBtn.addEventListener('click', async () => {
            const id = detailsUserId.value;
            if (!id) return;
            if (!confirm('Delete this technician account? This cannot be undone.')) return;
            await deleteTechnician(id);
        });
    }

    function escapeHtml(str) {
        if (!str) return '';
        return str
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/\"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }
})();

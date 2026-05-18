window.DeviceAllocation = (function () {
    
    /**
     * Renders allocation controls for a single device
     * @param {string} deviceId - The device ID
     * @param {object} existing - Existing allocation data (if any)
     * @returns {string} HTML string for allocation controls
     */
    function renderAllocationControls(deviceId, existing) {
        const allocationType = existing?.allocationType ?? 0;
        
        return `
<div class="device-allocation" data-device-id="${deviceId}">
  <select class="form-select form-select-sm allocation-type">
    <option value="0" ${allocationType === 0 ? "selected" : ""}>No Allocation</option>
    <option value="1" ${allocationType === 1 ? "selected" : ""}>Student</option>
    <option value="2" ${allocationType === 2 ? "selected" : ""}>Teacher</option>
  </select>

  <div class="allocation-student mt-2" style="display:${allocationType === 1 ? "block" : "none"}">
    <input class="form-control form-control-sm student-name" placeholder="Student Name" value="${existing?.studentName ?? ""}">
    <input class="form-control form-control-sm mt-1 student-id" placeholder="Student ID Number" value="${existing?.studentIdNumber ?? ""}">
  </div>

  <div class="allocation-teacher mt-2" style="display:${allocationType === 2 ? "block" : "none"}">
    <input class="form-control form-control-sm teacher-name" placeholder="Teacher Name" value="${existing?.teacherName ?? ""}">
    <input class="form-control form-control-sm mt-1 teacher-persal" placeholder="Teacher Persal Number" value="${existing?.teacherPersalNumber ?? ""}">
  </div>
</div>`;
    }

    /**
     * Wire up event handlers for allocation controls within a container
     * @param {HTMLElement} container - The container element
     */
    function wireUp(container) {
        container.addEventListener("change", function (e) {
            if (!e.target.classList.contains("allocation-type")) return;

            const wrapper = e.target.closest(".device-allocation");
            const type = parseInt(e.target.value, 10);

            const studentDiv = wrapper.querySelector(".allocation-student");
            const teacherDiv = wrapper.querySelector(".allocation-teacher");

            studentDiv.style.display = type === 1 ? "block" : "none";
            teacherDiv.style.display = type === 2 ? "block" : "none";
        });
    }

    /**
     * Collect allocation data from all devices in the container
     * @param {HTMLElement} container - The container element
     * @returns {Array} Array of DeviceAllocationDto objects
     */
    function collectAllocations(container) {
        const items = [];
        container.querySelectorAll(".device-allocation").forEach(wrapper => {
            const deviceId = wrapper.getAttribute("data-device-id");
            const type = parseInt(wrapper.querySelector(".allocation-type").value, 10);

            const dto = {
                deviceId: deviceId,
                allocationType: type
            };

            if (type === 1) {
                // Student
                dto.studentName = wrapper.querySelector(".student-name").value || null;
                dto.studentIdNumber = wrapper.querySelector(".student-id").value || null;
            } else if (type === 2) {
                // Teacher
                dto.teacherName = wrapper.querySelector(".teacher-name").value || null;
                dto.teacherPersalNumber = wrapper.querySelector(".teacher-persal").value || null;
            }

            items.push(dto);
        });

        return items;
    }

    /**
     * Validate allocation data
     * @param {Array} allocations - Array of allocation objects
     * @returns {object} Validation result {valid: boolean, errors: array}
     */
    function validateAllocations(allocations) {
        const errors = [];

        allocations.forEach((alloc, index) => {
            if (alloc.allocationType === 1) {
                // Student validation
                if (!alloc.studentName || alloc.studentName.trim() === "") {
                    errors.push(`Device ${index + 1}: Student name is required`);
                }
                if (!alloc.studentIdNumber || alloc.studentIdNumber.trim() === "") {
                    errors.push(`Device ${index + 1}: Student ID number is required`);
                }
            } else if (alloc.allocationType === 2) {
                // Teacher validation
                if (!alloc.teacherName || alloc.teacherName.trim() === "") {
                    errors.push(`Device ${index + 1}: Teacher name is required`);
                }
                if (!alloc.teacherPersalNumber || alloc.teacherPersalNumber.trim() === "") {
                    errors.push(`Device ${index + 1}: Teacher persal number is required`);
                }
                // Optional: Validate persal number format (numeric)
                if (alloc.teacherPersalNumber && !/^\d+$/.test(alloc.teacherPersalNumber.trim())) {
                    errors.push(`Device ${index + 1}: Persal number must be numeric`);
                }
            }
        });

        return {
            valid: errors.length === 0,
            errors: errors
        };
    }

    /**
     * Format allocation info for display (read-only)
     * @param {object} device - Device object with allocation fields
     * @returns {string} Formatted HTML string
     */
    function formatAllocationDisplay(device) {
        if (!device || device.allocationType === 0 || device.allocationType === undefined) {
            return '<span class="text-muted">Not allocated</span>';
        }

        if (device.allocationType === 1) {
            // Student
            return `
                <div class="allocation-display">
                    <span class="badge bg-primary">Student</span><br>
                    <small><strong>${device.studentName || 'N/A'}</strong></small><br>
                    <small class="text-muted">ID: ${device.studentIdNumber || 'N/A'}</small>
                </div>
            `;
        } else if (device.allocationType === 2) {
            // Teacher
            return `
                <div class="allocation-display">
                    <span class="badge bg-success">Teacher</span><br>
                    <small><strong>${device.teacherName || 'N/A'}</strong></small><br>
                    <small class="text-muted">Persal: ${device.teacherPersalNumber || 'N/A'}</small>
                </div>
            `;
        }

        return '<span class="text-muted">Unknown</span>';
    }

    /**
     * Format allocation info as plain text (for export/print)
     * @param {object} device - Device object with allocation fields
     * @returns {string} Plain text string
     */
    function formatAllocationText(device) {
        if (!device || device.allocationType === 0 || device.allocationType === undefined) {
            return 'Not allocated';
        }

        if (device.allocationType === 1) {
            return `Student: ${device.studentName || 'N/A'} (ID: ${device.studentIdNumber || 'N/A'})`;
        } else if (device.allocationType === 2) {
            return `Teacher: ${device.teacherName || 'N/A'} (Persal: ${device.teacherPersalNumber || 'N/A'})`;
        }

        return 'Unknown';
    }

    // Public API
    return {
        renderAllocationControls,
        wireUp,
        collectAllocations,
        validateAllocations,
        formatAllocationDisplay,
        formatAllocationText
    };
})();


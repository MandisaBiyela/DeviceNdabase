// Shared Logout Modal Component
// Usage: Include this script and call showLogoutModal() or attach to logout links

(function() {
    'use strict';

    // Create modal HTML if it doesn't exist
    function ensureModalExists() {
        if (document.getElementById('logoutModal')) {
            return;
        }

        const modalHTML = `
            <div class="modal fade" id="logoutModal" tabindex="-1" aria-labelledby="logoutModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-dialog-centered">
                    <div class="modal-content" style="border-radius: 12px; border: none; box-shadow: 0 8px 32px rgba(0,0,0,0.2);">
                        <div class="modal-header" style="border-bottom: 1px solid #e9ecef; padding: 1.5rem;">
                            <h5 class="modal-title" id="logoutModalLabel" style="font-weight: 600; color: #2c3e50;">
                                <i class="bi bi-box-arrow-right text-danger me-2"></i>Confirm Logout
                            </h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body" style="padding: 1.5rem;">
                            <p class="mb-0" style="color: #6c757d; font-size: 1rem;">
                                Are you sure you want to logout? You'll need to log in again to access your account.
                            </p>
                        </div>
                        <div class="modal-footer" style="border-top: 1px solid #e9ecef; padding: 1rem 1.5rem;">
                            <button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal" style="border-radius: 8px; padding: 0.5rem 1.5rem;">
                                Cancel
                            </button>
                            <button type="button" class="btn btn-danger" id="confirmLogoutBtn" style="border-radius: 8px; padding: 0.5rem 1.5rem; font-weight: 500;">
                                <i class="bi bi-box-arrow-right me-2"></i>Logout
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        document.body.insertAdjacentHTML('beforeend', modalHTML);

        // Attach confirm handler
        document.getElementById('confirmLogoutBtn').addEventListener('click', async () => {
            try {
                const response = await fetch('/api/auth/logout', {
                    method: 'POST',
                    credentials: 'same-origin'
                });
                
                // Redirect regardless of response (logout should always succeed client-side)
                window.location.href = '/login.html';
            } catch (error) {
                console.error('Logout error:', error);
                // Still redirect even on error
                window.location.href = '/login.html';
            }
        });
    }

    // Show the logout modal
    window.showLogoutModal = function() {
        ensureModalExists();
        const logoutModal = new bootstrap.Modal(document.getElementById('logoutModal'));
        logoutModal.show();
    };

    // Auto-attach to logout links on page load
    document.addEventListener('DOMContentLoaded', function() {
        ensureModalExists();
        
        // Find all logout links and attach handlers
        document.querySelectorAll('a[href="/logout"], a[href*="/logout"]').forEach(link => {
            link.addEventListener('click', function(e) {
                e.preventDefault();
                showLogoutModal();
            });
        });

        // Also handle logout buttons with id="logoutBtn"
        const logoutBtn = document.getElementById('logoutBtn');
        if (logoutBtn && !logoutBtn.hasAttribute('data-logout-handled')) {
            logoutBtn.setAttribute('data-logout-handled', 'true');
            logoutBtn.addEventListener('click', function(e) {
                e.preventDefault();
                showLogoutModal();
            });
        }
    });
})();


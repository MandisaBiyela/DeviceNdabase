// Phase 0 unified sidebar: injects the shared partial, highlights the active
// link based on the current path, and loads the user profile block.
//
// Page contract:
//   - Page provides <aside class="app-sidebar" id="phase0Sidebar"></aside>
//   - Page includes this script (defer-safe).
//
// To force a specific active key (e.g. on a non-canonical path), set
// `window.PHASE0_ACTIVE_NAV = 'new-batches'` BEFORE this script runs.
(function () {
  "use strict";

  const ACTIVE_BY_PATH = {
    "orders.html": "orders",
    "order-detail.html": "orders",
    "new.html": "new",
    "new-batches.html": "new-batches",
    "new-batch.html": "new-batches",
    "batch-items.html": "new-batches",
    "new-stock-batch.html": "new-batches",
    "model-scanning.html": "new-batches",
    "new-all.html": "new-all",
    "rnr.html": "rnr",
    "rnr-batches.html": "rnr-batches",
    "rnr-batch.html": "rnr-batches",
    "rnr-all.html": "rnr-all",
    "new-readiness.html": "readiness",
    "readiness-reports.html": "readiness-reports",
    "readiness-report.html": "readiness-reports",
    "readiness-rooms.html": "readiness-reports"
  };

  function activeKeyFromPath() {
    if (typeof window.PHASE0_ACTIVE_NAV === "string" && window.PHASE0_ACTIVE_NAV) {
      return window.PHASE0_ACTIVE_NAV;
    }
    try {
      const segs = location.pathname.split("/").filter(Boolean);
      const last = (segs[segs.length - 1] || "").toLowerCase();
      return ACTIVE_BY_PATH[last] || null;
    } catch (_) {
      return null;
    }
  }

  function initialsFromUser(user) {
    if (!user) return "U";
    if (user.fullName && user.fullName.trim()) {
      return user.fullName
        .trim()
        .split(/\s+/)
        .map((n) => n[0])
        .join("")
        .substring(0, 2)
        .toUpperCase();
    }
    return (user.email || "U").substring(0, 2).toUpperCase();
  }

  async function loadProfile() {
    const elName = document.getElementById("profileName");
    const elInitials = document.getElementById("profileInitials");
    const elRole = document.getElementById("profileRole");
    if (!elName && !elInitials) return;
    try {
      const res = await fetch("/api/auth/current-user", { credentials: "include" });
      if (!res.ok) return;
      const user = await res.json();
      const initials = initialsFromUser(user);
      const display = user.fullName || user.email || "User";
      const role = (user.roles && user.roles[0]) || user.role || "User";
      if (elInitials) elInitials.textContent = initials;
      if (elName) {
        elName.textContent = display;
        elName.classList.remove("is-loading");
      }
      if (elRole) elRole.textContent = role;

      // Some pages also have a top-bar initials pill.
      const headerPill = document.getElementById("headerUserInitials");
      if (headerPill) headerPill.textContent = initials;
      const avatarPill = document.querySelector(".avatar-btn .avatar-initials");
      if (avatarPill && !avatarPill.dataset.static) avatarPill.textContent = initials;
    } catch (_) {
      if (elName) elName.textContent = "User";
    }
  }

  async function injectSidebar() {
    const host = document.getElementById("phase0Sidebar");
    if (!host) return;
    try {
      // Always resolve to absolute /phase0/partials/sidebar.html so it works
      // from any nested route (orders.html, order-detail.html?id=…, etc).
      const res = await fetch("/phase0/partials/sidebar.html", { cache: "no-store" });
      if (!res.ok) return;
      host.innerHTML = await res.text();

      const key = activeKeyFromPath();
      if (key) {
        const link = host.querySelector(`[data-nav="${key}"]`);
        if (link) link.classList.add("active");
      }

      await loadProfile();
    } catch (err) {
      console.warn("Phase 0 sidebar failed to load", err);
    }
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", injectSidebar);
  } else {
    injectSidebar();
  }
})();

// Phase 2 Shared Utilities
// This file provides common helper functions used across Phase 2 UI pages

function formatLocalTime(dateString) {
    if (!dateString) return "—";
    const date = new Date(dateString);
    if (isNaN(date.getTime())) return dateString;
    return date.toLocaleString([], {
        year: "numeric",
        month: "short",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit"
    });
}


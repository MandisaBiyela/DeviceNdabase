// wwwroot/js/helpers/time.js
(function () {
    function formatDateTime(value) {
        if (!value) return '';
        const d = new Date(value);
        if (isNaN(d.getTime())) return value;
        return d.toLocaleString();
    }

    function formatDate(value) {
        if (!value) return '';
        const d = new Date(value);
        if (isNaN(d.getTime())) return value;
        return d.toLocaleDateString();
    }

    function timeAgo(value) {
        if (!value) return '';
        const d = new Date(value);
        if (isNaN(d.getTime())) return value;
        const diffMs = Date.now() - d.getTime();
        const diffMin = Math.floor(diffMs / 60000);
        if (diffMin < 1) return 'just now';
        if (diffMin < 60) return `${diffMin} min ago`;
        const diffH = Math.floor(diffMin / 60);
        if (diffH < 24) return `${diffH} h ago`;
        const diffD = Math.floor(diffH / 24);
        return `${diffD} day${diffD === 1 ? '' : 's'} ago`;
    }

    window.timeHelpers = { formatDateTime, formatDate, timeAgo };
    window.formatDateTime = formatDateTime;
    window.formatDate = formatDate;
    window.timeAgo = timeAgo;
})();
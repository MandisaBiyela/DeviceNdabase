<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <title>DeviceDesk – Phase 1 Dashboard</title>

    <!-- Bootstrap + Icons (use your existing paths) -->
    <link rel="stylesheet" href="/lib/bootstrap/css/bootstrap.min.css" />
    <link rel="stylesheet" href="/lib/bootstrap-icons/font/bootstrap-icons.css" />

    <!-- New dashboard shell styles -->
    <link rel="stylesheet" href="/css/dashboard-shell.css" />
</head>

<body class="app-shell">
    <!-- SIDEBAR -->
    <aside class="app-sidebar">
        <div class="sidebar-brand">
            <div class="brand-logo">DD</div>
            <div class="brand-text">
                <span class="brand-title">DeviceDesk</span>
                <span class="brand-subtitle">Phase 1 – Main Receiving</span>
            </div>
        </div>

        <nav class="sidebar-nav">
            <span class="sidebar-section">MAIN MENU</span>

            <a href="/phase1/index.html" class="sidebar-link active">
                <i class="bi bi-grid-fill"></i>
                <span>Dashboard</span>
            </a>

            <a href="/phase1/new-batch.html" class="sidebar-link">
                <i class="bi bi-plus-circle"></i>
                <span>New Receiving Batch</span>
            </a>

            <a href="/phase1/batches.html" class="sidebar-link">
                <i class="bi bi-card-list"></i>
                <span>All Batches</span>
            </a>

            <span class="sidebar-section">SYSTEM</span>

            <a href="/logout" class="sidebar-link danger">
                <i class="bi bi-box-arrow-right"></i>
                <span>Logout</span>
            </a>
        </nav>
    </aside>

    <!-- MAIN AREA -->
    <div class="app-main">
        <!-- TOP BAR -->
        <header class="app-header">
            <div>
                <div class="welcome-line">Dashboard – Phase 1</div>
                <div class="welcome-date">Main Receiving</div>
            </div>

            <div class="header-center">
                <button class="date-range-btn">
                    <i class="bi bi-calendar-event"></i>
                    <span id="phase1-date-range">Today</span>
                    <i class="bi bi-chevron-down"></i>
                </button>
            </div>

            <div class="header-right">
                <button class="btn-export" id="btnExportPhase1">
                    <i class="bi bi-download"></i>
                    <span>Export</span>
                </button>
                <button class="avatar-btn">
                    <span class="avatar-initials">PH1</span>
                </button>
            </div>
        </header>

        <!-- CONTENT -->
        <main class="app-content">
            <!-- PAGE TITLE (optional small breadcrumb) -->
            <div class="mb-2">
                <div class="text-muted small">Home</div>
                <h1 class="h4 fw-semibold mt-1">Dashboard</h1>
            </div>

            <!-- KPI CARDS -->
            <section class="stats-grid">
                <!-- Total Batches -->
                <div class="stat-card">
                    <div class="stat-icon blue">
                        <i class="bi bi-inbox"></i>
                    </div>
                    <div class="stat-body">
                        <div class="stat-label">Total Batches</div>
                        <div class="stat-value">
                            <!-- keep existing ID if you already have one -->
                            <span id="totalBatchesCount">0</span>
                        </div>
                    </div>
                </div>

                <!-- Completed -->
                <div class="stat-card">
                    <div class="stat-icon green">
                        <i class="bi bi-check-circle"></i>
                    </div>
                    <div class="stat-body">
                        <div class="stat-label">Completed</div>
                        <div class="stat-value">
                            <span id="completedBatchesCount">0</span>
                        </div>
                    </div>
                </div>

                <!-- In Progress -->
                <div class="stat-card">
                    <div class="stat-icon yellow">
                        <i class="bi bi-hourglass-split"></i>
                    </div>
                    <div class="stat-body">
                        <div class="stat-label">In Progress</div>
                        <div class="stat-value">
                            <span id="inProgressBatchesCount">0</span>
                        </div>
                    </div>
                </div>

                <!-- Total Devices -->
                <div class="stat-card">
                    <div class="stat-icon cyan">
                        <i class="bi bi-box-seam"></i>
                    </div>
                    <div class="stat-body">
                        <div class="stat-label">Total Devices</div>
                        <div class="stat-value">
                            <span id="totalDevicesCount">0</span>
                        </div>
                    </div>
                </div>
            </section>

            <!-- QUICK ACTIONS + SOURCE BREAKDOWN ROW -->
            <section class="content-grid mt-3">
                <!-- Quick Actions panel -->
                <div class="panel">
                    <div class="panel-header">
                        <span>Quick Actions</span>
                    </div>
                    <div class="panel-body quick-actions-grid">
                        <!-- Create Receiving Batch -->
                        <button class="action-card primary" id="btnCreateBatch">
                            <div class="action-icon">
                                <i class="bi bi-plus-lg"></i>
                            </div>
                            <div class="action-text">
                                <div class="action-title">Create Receiving Batch</div>
                                <div class="action-subtitle">Start a new receiving process</div>
                            </div>
                        </button>

                        <!-- View All Batches -->
                        <button class="action-card outlined" id="btnViewAllBatches">
                            <div class="action-icon">
                                <i class="bi bi-card-checklist"></i>
                            </div>
                            <div class="action-text">
                                <div class="action-title">View All Batches</div>
                                <div class="action-subtitle">Browse receiving history</div>
                            </div>
                        </button>
                    </div>
                </div>

                <!-- Source Breakdown panel -->
                <div class="panel">
                    <div class="panel-header">
                        <span>Source Breakdown</span>
                    </div>
                    <div class="panel-body">
                        <!-- put your current “Source Breakdown” UI here -->
                        <div id="sourceBreakdownContainer">
                            <!-- example placeholder -->
                            <!-- Your existing list / chart stays, just wrapped in this div -->
                        </div>
                    </div>
                </div>
            </section>

            <!-- RECENT ACTIVITY -->
            <section class="panel recent-panel mt-3">
                <div class="panel-header">
                    <span>Recent Activity</span>
                </div>
                <div class="panel-body">
                    <div id="recentActivityContainer">
                        <!-- your existing “No recent activity yet” block goes here -->
                        <!-- Example empty state: -->
                        <div class="text-center py-4 text-muted">
                            <i class="bi bi-inbox fs-3 d-block mb-2"></i>
                            <div>No recent activity yet.</div>
                        </div>
                    </div>
                </div>
            </section>
        </main>
    </div>

    <!-- Scripts (reuse your existing file paths) -->
    <script src="/lib/bootstrap/js/bootstrap.bundle.min.js"></script>
    <script src="/js/phase1-dashboard.js"></script>
</body>
</html>

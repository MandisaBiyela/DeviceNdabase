document.addEventListener('DOMContentLoaded',function(){
  try {
    document.body.classList.add('trendex-theme');

    var hasExistingSidebar = !!(document.querySelector('.sidebar') || document.getElementById('dispatch-sidebar') || document.querySelector('.dd-side'));

    // Always reserve space for sidebar in Trendex theme
    document.body.classList.add('tdx-has-sidebar');

    // Inject Trendex sidebar if not present
    if (!hasExistingSidebar) {
      var sidebarHtml = [
        '<nav class="tdx-sidebar">',
        '  <div class="tdx-brand">',
        '    <span class="tdx-logo">T</span>',
        '    <span class="tdx-title">Trendex</span>',
        '  </div>',
        '  <ul class="tdx-nav">',
        '    <li><a class="nav-link" href="/phase0/"><i class="bi bi-speedometer2"></i> Dashboard</a></li>',
        '    <li><a class="nav-link" href="/phase0/new-stock-batch.html"><i class="bi bi-box"></i> New Batches</a></li>',
        '    <li><a class="nav-link" href="/phase0/model-scanning.html"><i class="bi bi-upc-scan"></i> Model Scanning</a></li>',
        '    <li><a class="nav-link" href="/phase1/dashboard.html"><i class="bi bi-clipboard-check"></i> Receiving</a></li>',
        '    <li><a class="nav-link" href="/phase1/receiving-list.html"><i class="bi bi-card-checklist"></i> Receipts</a></li>',
        '    <li><a class="nav-link" href="/phase1/scanning.html"><i class="bi bi-upc"></i> Scanning</a></li>',
        '    <li><a class="nav-link" href="/phase1/reconciliation.html"><i class="bi bi-diagram-3"></i> Reconciliation</a></li>',
        '    <li><a class="nav-link" href="/phase1/workflow.html"><i class="bi bi-diagram-2"></i> Workflow</a></li>',
        '    <li><a class="nav-link" href="/phase2/index.html"><i class="bi bi-cpu"></i> ICT Center</a></li>',
        '    <li><a class="nav-link" href="/dispatch/index.html"><i class="bi bi-truck"></i> Dispatch</a></li>',
        '  </ul>',
        '</nav>'
      ].join('');
      document.body.insertAdjacentHTML('afterbegin', sidebarHtml);
    }

    // Active link highlight
    var path = location.pathname.toLowerCase();
    document.querySelectorAll('.tdx-sidebar .nav-link').forEach(function(a){
      var href = a.getAttribute('href') || '';
      if (href && path.startsWith(href.toLowerCase())) {
        a.classList.add('active');
      }
    });
  } catch (e) {}
});
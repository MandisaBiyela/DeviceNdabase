// Common utilities for DeviceDesk - Classic script version (no modules)

// Utility functions attached to window for global access
window.$ = (s) => document.querySelector(s);
window.enable = (sel, on) => { const el = window.$(sel); if(el) el.disabled = !on; };

window.templateCsv = () =>
  "Serial,Brand,Model,Qty,EMIS\nSN001,Dell,3100,1,500123\nSN002,HP,ProBook,1,500123\n864500001234567,,,\n";

window.downloadCsv = function(name, content){
  const a = document.createElement('a');
  a.href = URL.createObjectURL(new Blob([content], {type:'text/csv'}));
  a.download = name; 
  a.click(); 
  URL.revokeObjectURL(a.href);
};

// Sidebar loading function
window.ddLoadSidebar = function(options) {
    console.log('Loading sidebar for area:', options?.area, 'active:', options?.active);
    // For now, just log - the actual sidebar implementation would go here
    // This prevents the "ddLoadSidebar is not defined" error
    
    // Basic sidebar placeholder - you can enhance this later
    if (options?.area === 'phase1') {
        console.log('Phase 1 sidebar loaded successfully');
    }
};

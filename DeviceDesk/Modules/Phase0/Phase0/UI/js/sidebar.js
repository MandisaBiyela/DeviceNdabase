// Sidebar loader that fetches shared partial and wires active/toggle
(function(){
  async function ddLoadSidebar(activeKey){
    try {
      const host = document.getElementById('ddSidebarHost');
      if(!host) return;
      const res = await fetch('partials/sidebar.html', { cache: 'no-store' });
      host.innerHTML = await res.text();
      // highlight active link
      host.querySelectorAll('.dd-side__link').forEach(a=>{
        if(a.dataset.nav === activeKey) a.classList.add('active');
      });
      // mobile toggle
      const btn = host.querySelector('#ddSideToggle');
      if(btn){ btn.onclick = ()=> document.body.classList.toggle('dd-open'); }
    } catch(e) {
      console.warn('Sidebar failed to load', e);
    }
  }
  window.ddLoadSidebar = ddLoadSidebar;
})();
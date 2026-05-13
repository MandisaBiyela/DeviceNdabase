// Inject brandbar partial and set subtitle
(function(){
  async function ddInjectBrandbar(subtitle){
    try{
      const host = document.getElementById('ddBrandbarHost');
      if(!host) return;
      const res = await fetch('partials/brandbar.html', { cache: 'no-store' });
      host.innerHTML = await res.text();
      const s = host.querySelector('#ddBrandbarSubtitle');
      if(s) s.textContent = subtitle || '';
    }catch(e){
      console.warn('Brandbar failed to inject', e);
    }
  }
  window.ddInjectBrandbar = ddInjectBrandbar;
})();
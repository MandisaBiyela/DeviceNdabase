export const $ = (s) => document.querySelector(s);
export const enable = (sel, on) => { const el=$(sel); if(el) el.disabled = !on; };
export const templateCsv = () =>
  "Serial,Brand,Model,Qty,EMIS\nSN001,Dell,3100,1,500123\nSN002,HP,ProBook,1,500123\n864500001234567,,,\n";
export function downloadCsv(name, content){
  const a=document.createElement('a');
  a.href=URL.createObjectURL(new Blob([content],{type:'text/csv'}));
  a.download=name; a.click(); URL.revokeObjectURL(a.href);
}
'use strict';
// The QR fragment alone contains the AES key. The rendezvous service gets a
// one-use topic derived from the key, and an authenticated encrypted answer.
const PortoSignal = (() => {
  function encode(bytes) {
    return btoa(String.fromCharCode(...bytes)).replaceAll('+','-').replaceAll('/','_').replace(/=+$/,'');
  }
  function decode(text) {
    return Uint8Array.from(atob(text.replaceAll('-','+').replaceAll('_','/')),c=>c.charCodeAt(0));
  }
  async function session(secret,offer) {
    if (!/^[A-Za-z0-9_-]{43}$/.test(secret)) throw Error('Codice automatico non valido.');
    const key = await crypto.subtle.importKey('raw',decode(secret),'AES-GCM',false,['encrypt','decrypt']);
    const encoder = new TextEncoder();
    const topic = 'porto-'+sha256('porto-web-pair-v1:'+secret).slice(0,40);
    const url = 'https://ntfy.sh/'+topic;
    const algorithm = iv => ({name:'AES-GCM',iv,additionalData:encoder.encode(offer)});
    return {
      listen(onReady,onAnswer,onUnavailable) {
        const source = new EventSource(url+'/sse');
        let closed=false;
        const close=()=>{closed=true; clearTimeout(timer); source.close();};
        const unavailable=()=>{if (!closed) {close(); onUnavailable();}};
        let timer=setTimeout(unavailable,8000);
        // ntfy emits a named "open" event as well as the browser's transport open.
        source.addEventListener('open',event=>{
          if (closed || !event.data) return;
          clearTimeout(timer); timer=setTimeout(unavailable,180000); onReady();
        });
        source.onerror=unavailable;
        source.onmessage=async event=>{
          if (closed || event.data.length>8192) return;
          try {
            const box=JSON.parse(event.data).message;
            if (typeof box!=='string' || !/^[A-Za-z0-9_-]{40,3800}$/.test(box)) return;
            const bytes=decode(box);
            const plain=await crypto.subtle.decrypt(algorithm(bytes.slice(0,12)),key,bytes.slice(12));
            const answer=new TextDecoder().decode(plain);
            if (closed || !/^PORTO1\.[A-Za-z0-9_-]+$/.test(answer)) return;
            close(); onAnswer(answer);
          } catch { /* Ignore unrelated or unauthenticated messages. */ }
        };
        return close;
      },
      async reply(answer) {
        const iv=crypto.getRandomValues(new Uint8Array(12));
        const encrypted=new Uint8Array(await crypto.subtle.encrypt(algorithm(iv),key,encoder.encode(answer)));
        const bytes=new Uint8Array(iv.length+encrypted.length);
        bytes.set(iv); bytes.set(encrypted,iv.length);
        const body=encode(bytes);
        if (body.length>3800) throw Error('Risposta troppo lunga per il collegamento automatico.');
        const controller=new AbortController();
        const timer=setTimeout(()=>controller.abort(),8000);
        try {
          const response=await fetch(url+'?cache=no&firebase=no',{
            method:'POST',body,credentials:'omit',referrerPolicy:'no-referrer',
            cache:'no-store',redirect:'error',signal:controller.signal
          });
          if (!response.ok) throw Error('Servizio di collegamento indisponibile.');
        } finally {clearTimeout(timer);}
      }
    };
  }
  return {session,secret:()=>encode(crypto.getRandomValues(new Uint8Array(32)))};
})();

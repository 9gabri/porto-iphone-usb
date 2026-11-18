'use strict';
const Porto = (() => {
  const block = 512 * 1024, packet = 16 * 1024;
  const media = /\.(jpe?g|jfif|png|gif|webp|heic|heif|avif|dng|tiff?|bmp|mov|mp4|m4v|avi|mkv|webm|mpeg|mpg|3gp|mts|m2ts)$/i;
  let folder, selection, pickerStart, pickerSeconds, sentStart, firstByteAt;
  let batch, current, saved = 0, reused = 0, completedBytes = 0;
  let pending, incoming = Promise.resolve(), queuedBytes = 0, ended = false, complete = false;
  const copies = new Map(), index = new Map();
  const seconds = ms => (ms / 1000).toLocaleString('it-IT',{maximumFractionDigits:1}) + ' s';
  const sizeLabel = n => (n/1048576).toLocaleString('it-IT',{maximumFractionDigits:1}) + ' MiB';
  const send = obj => channel.send(JSON.stringify(obj));
  function expect(condition,message='Sequenza di trasferimento non valida.') {
    if (!condition) throw Error(message);
  }
  async function digest(file) {
    const hash = sha256.create();
    for (let pos=0;pos<file.size;pos+=block) {
      expect(!ended,'Copia interrotta.');
      hash.update(await file.slice(pos,pos+block).arrayBuffer());
    }
    return hash.hex();
  }
  function waitReply(obj, types) {
    return new Promise((resolve,reject) => {
      pending = {types,resolve,reject};
      try { send(obj); } catch (error) { pending = null; reject(error); }
    });
  }
  function sourceSelected() {
    selection = Array.from($('files').files);
    pickerSeconds = seconds(performance.now()-pickerStart);
    $('times').textContent = 'Dall’apertura di Foto alla selezione pronta (inclusi i tocchi): ' + pickerSeconds;
    $('send').hidden = true;
    if (!selection.length) { statusText('Nessun elemento selezionato.'); return; }
    const unknown = selection.find(f => !media.test(f.name));
    if (unknown) { statusText('Formato non supportato nella prova: ' + unknown.name + '. Scegli nuovamente gli elementi.'); return; }
    const filter = $('kind').value;
    if (filter !== 'image/*,video/*' && selection.some(f => !f.type.startsWith(filter.split('/')[0]+'/'))) {
      statusText('La selezione contiene elementi di tipo diverso o non riconoscibile. Controlla il filtro.'); return;
    }
    $('send').hidden = false;
    statusText(selection.length + ' elementi pronti · ' + sizeLabel(selection.reduce((n,f)=>n+f.size,0)));
  }
  async function sendSelection() {
    $('send').disabled = $('pick').disabled = $('kind').disabled = true;
    const total = selection.reduce((n,f)=>n+f.size,0);
    sentStart = performance.now();
    statusText('In attesa di “Accetta e copia” sul computer.');
    await waitReply({t:'batch',count:selection.length,total},['accept']);
    let sent = 0;
    for (const file of selection) {
      statusText('Controllo ' + file.name + ' (' + (sent+1) + ' di ' + selection.length + ')');
      const hash = await digest(file);
      const reply = await waitReply({t:'file',name:file.name,size:file.size,hash},['send','skip']);
      if (reply.t === 'send') {
        for (let pos=0;pos<file.size;pos+=block) {
          const data = new Uint8Array(await file.slice(pos,pos+block).arrayBuffer());
          for (let start=0;start<data.length;start+=packet) channel.send(data.subarray(start,start+packet));
          await waitReply({t:'flush'},['ack']);
          $('progress').textContent = file.name + ' · ' + sizeLabel(pos+data.length) + ' / ' + sizeLabel(file.size);
        }
        await waitReply({t:'end'},['verified']);
      }
      sent++;
      $('progress').textContent = sent + ' / ' + selection.length + ' verificati sul PC.';
    }
    await waitReply({t:'done'},['done']);
    complete = true;
    $('cancel').hidden = true;
    statusText('Trasferimento completato. ' + sent + ' elementi verificati sul computer.');
    $('times').textContent += ' · Da Invia alla conferma: ' + seconds(performance.now()-sentStart);
  }
  async function approve() {
    expect(batch && !batch.approved);
    $('accept').hidden = true;
    // Only index files created by this prototype, directly inside its own folder.
    folder = await folder.getDirectoryHandle('Porto Web',{create:true});
    for await (const [name,entry] of folder.entries()) {
      const match = /--([0-9a-f]{64}-\d+)--[0-9a-f-]{36}\.[a-z0-9]+$/.exec(name);
      if (entry.kind !== 'file' || !match) continue;
      const list = index.get(match[1]) || [];
      list.push(entry); index.set(match[1],list);
    }
    sentStart = performance.now();
    batch.approved = true; send({t:'accept'}); statusText('In attesa del primo file.');
  }
  function progress() {
    $('progress').textContent = (saved+reused) + ' / ' + batch.count + ' verificati · ' + saved +
      ' nuove copie · ' + reused + ' già presenti · ' + sizeLabel(completedBytes + (current?.received || 0));
  }
  async function begin(m) {
    expect(batch.approved && !current && saved+reused<batch.count);
    expect(typeof m.name === 'string' && m.name.length<=512 && media.test(m.name) &&
      Number.isSafeInteger(m.size) && m.size>=0 && m.size<=batch.total-completedBytes &&
      typeof m.hash==='string' && /^[a-f0-9]{64}$/.test(m.hash),'Dati del file non validi.');
    const key = sha256(JSON.stringify([m.name,m.size,m.hash]));
    const ordinal = (copies.get(key) || 0)+1; copies.set(key,ordinal);
    const identity = key+'-'+ordinal;
    for (const entry of index.get(identity) || []) {
      const candidate = await entry.getFile();
      if (candidate.size===m.size && await digest(candidate)===m.hash) {
        reused++; completedBytes+=m.size; progress(); send({t:'skip'}); return;
      }
    }
    const ext = m.name.match(media)[1].toLowerCase();
    const stem = m.name.slice(0,m.name.lastIndexOf('.')).replace(/[^a-zA-Z0-9_-]/g,'_').slice(0,55) || 'media';
    const name = 'Porto_'+stem+'--'+identity+'--'+crypto.randomUUID()+'.'+ext;
    // A unique destination also keeps same-name, same-byte selections distinct.
    try { await folder.getFileHandle(name); throw Error('Nome già presente.'); }
    catch (error) { if (error.name!=='NotFoundError') throw error; }
    const entry = await folder.getFileHandle(name,{create:true});
    const writer = await entry.createWritable();
    if (ended) { await writer.abort(); throw Error('Collegamento interrotto.'); }
    current = {entry,writer,size:m.size,hash:m.hash,received:0,parts:[],buffered:0};
    statusText('Ricezione di ' + m.name); send({t:'send'});
  }
  async function handle(data) {
    if (ended) return;
    if (typeof data !== 'string') {
      expect(!senderMode && current && data instanceof ArrayBuffer && data.byteLength>0 && data.byteLength<=packet);
      expect(current.buffered+data.byteLength<=block && current.received+data.byteLength<=current.size);
      if (firstByteAt===undefined) {
        firstByteAt = performance.now();
        $('times').textContent='Da Accetta al primo dato: '+seconds(firstByteAt-sentStart);
      }
      current.parts.push(data); current.buffered+=data.byteLength; current.received+=data.byteLength; return;
    }
    expect(data.length<=4096);
    const m = JSON.parse(data);
    if (senderMode) {
      expect(pending && pending.types.includes(m.t));
      const waiter = pending; pending=null; waiter.resolve(m); return;
    }
    if (m.t === 'batch') {
      expect(!batch && Number.isSafeInteger(m.count) && m.count>0 && m.count<=100000 &&
        Number.isSafeInteger(m.total) && m.total>=0);
      batch={count:m.count,total:m.total,approved:false};
      statusText('Il telefono propone '+m.count+' elementi · '+sizeLabel(m.total)+'.');
      $('accept').hidden=false; return;
    }
    expect(batch?.approved);
    if (m.t === 'file') return begin(m);
    if (m.t === 'done') {
      expect(!current && saved+reused===batch.count && completedBytes===batch.total && !complete);
      complete=true; $('cancel').hidden=true; send({t:'done'}); statusText('Copia completata. Apri “Porto Web” nella cartella scelta.');
      $('times').textContent+=' · Da Accetta alla verifica finale: '+seconds(performance.now()-sentStart);
      return;
    }
    expect(current);
    if (m.t==='flush') {
      expect(current.buffered>0); await current.writer.write(new Blob(current.parts));
      current.parts=[]; current.buffered=0; progress(); send({t:'ack'}); return;
    }
    expect(m.t==='end' && current.received===current.size && current.buffered===0);
    await current.writer.close(); current.writer=null;
    const file=await current.entry.getFile();
    expect(file.size===current.size && await digest(file)===current.hash,'La verifica del file salvato è fallita.');
    saved++; completedBytes+=current.size; current=null; progress(); send({t:'verified'});
  }
  return {
    setup() {
      onButton('accept',approve); onButton('send',sendSelection);
      $('pick').onclick=()=>{ pickerStart=performance.now(); $('files').value=''; $('files').click(); };
      $('files').onchange=sourceSelected;
      $('kind').onchange=()=>{ $('files').accept=$('kind').value; selection=null; $('send').hidden=true; };
    },
    setFolder(value){folder=value;},
    finished(){return complete;},
    stop(){ended=true; pending?.reject(Error('Collegamento interrotto.')); pending=null; current?.writer?.abort().catch(()=>{});},
    receive(data){
      const amount=typeof data==='string'? data.length+64 : data.byteLength+64;
      queuedBytes+=amount;
      if (queuedBytes>2*block) {fail(Error('Troppi dati in attesa.')); return;}
      incoming=incoming.then(()=>handle(data)).catch(fail).finally(()=>{queuedBytes-=amount;});
    }
  };
})();

'use strict';
const $ = id => document.getElementById(id);
const statusText = text => { $('status').textContent = text; };
const offerPrefix = '#porto1=';
const offerCode = location.hash.startsWith(offerPrefix) ? location.hash.slice(offerPrefix.length) : '';
const senderMode = Boolean(offerCode);
let peer, channel, cameraStream, pairingTimer;
let sessionEnded = false;
function stopCamera() {
  cameraStream?.getTracks().forEach(track => track.stop());
  cameraStream = null;
  $('camera').srcObject = null;
  $('camera').hidden = true;
}
function fail(error) {
  if (sessionEnded) return;
  sessionEnded = true;
  $('cancel').hidden = true;
  clearTimeout(pairingTimer);
  stopCamera();
  Porto.stop();
  peer?.close();
  for (const id of ['accept','send','pick','scan','connect']) $(id).disabled = true;
  statusText('Prova interrotta. ' + (error.message || String(error)) + ' Per ricollegarti, riapri la pagina sul PC.');
}
function onButton(id, action) {
  $(id).onclick = () => Promise.resolve().then(action).catch(error => {
    if (error.name === 'AbortError' && !peer) return;
    fail(error);
  });
}
function drawQR(canvas, content) {
  const qr = qrcode(0, 'L');
  qr.addData(content); qr.make();
  const n = qr.getModuleCount(), cell = 6;
  canvas.width = canvas.height = (n + 8) * cell;
  const ctx = canvas.getContext('2d');
  ctx.fillStyle = '#fff'; ctx.fillRect(0,0,canvas.width,canvas.height);
  ctx.fillStyle = '#000';
  for (let y=0;y<n;y++) for (let x=0;x<n;x++) {
    if (qr.isDark(y,x)) ctx.fillRect((x+4)*cell,(y+4)*cell,cell,cell);
  }
}
async function pack(sdp) {
  const stream = new Blob([sdp]).stream().pipeThrough(new CompressionStream('gzip'));
  const bytes = new Uint8Array(await new Response(stream).arrayBuffer());
  return btoa(String.fromCharCode(...bytes)).replaceAll('+','-').replaceAll('/','_').replace(/=+$/,'');
}
async function unpack(code) {
  if (!/^[A-Za-z0-9_-]{1,6000}$/.test(code)) throw Error('Codice di collegamento non valido.');
  const bytes = Uint8Array.from(atob(code.replaceAll('-','+').replaceAll('_','/')), c => c.charCodeAt(0));
  const reader = new Blob([bytes]).stream().pipeThrough(new DecompressionStream('gzip')).getReader();
  let sdp = '';
  const decoder = new TextDecoder();
  for (;;) {
    const {done,value} = await reader.read();
    if (done) break;
    if (sdp.length + value.length > 16000) { await reader.cancel(); throw Error('Codice troppo grande.'); }
    sdp += decoder.decode(value, {stream:true});
  }
  sdp += decoder.decode();
  if (!sdp.startsWith('v=0') || !/^m=application /m.test(sdp) || /^m=(?!application )/m.test(sdp))
    throw Error('Questa prova accetta soltanto un collegamento dati.');
  return sdp;
}
function initPeer() {
  peer = new RTCPeerConnection({iceServers:[]});
  peer.onconnectionstatechange = () => {
    if (peer.connectionState === 'failed') fail(Error('La rete ha interrotto il collegamento diretto.'));
  };
}
async function localCode(desc) {
  await peer.setLocalDescription(desc);
  if (peer.iceGatheringState !== 'complete') await new Promise((resolve,reject) => {
    const timer = setTimeout(() => { peer.removeEventListener('icegatheringstatechange',check); reject(Error('Ricerca del collegamento locale scaduta.')); },15000);
    function check() {
      if (peer.iceGatheringState !== 'complete') return;
      clearTimeout(timer); peer.removeEventListener('icegatheringstatechange',check); resolve();
    }
    peer.addEventListener('icegatheringstatechange',check); check();
  });
  return pack(peer.localDescription.sdp);
}
function bind(dc) {
  if (channel || dc.label !== 'porto-web-v1') { dc.close(); return; }
  channel = dc; dc.binaryType = 'arraybuffer';
  dc.onopen = () => {
    clearTimeout(pairingTimer); stopCamera();
    $('pairing').hidden = $('answerBox').hidden = true;
    $('choose').hidden = !senderMode;
    $('cancel').hidden = false;
    statusText(senderMode ? 'Collegato. Scegli gli elementi da inviare.' : 'Telefono collegato. In attesa della selezione.');
  };
  dc.onmessage = e => Porto.receive(e.data);
  dc.onclose = () => { if (!Porto.finished()) fail(Error('Collegamento chiuso prima della conferma finale.')); };
  dc.onerror = () => fail(Error('Errore del canale dati.'));
}
async function acceptCode(code) {
  if (!code.startsWith('PORTO1.')) throw Error('Usa il codice mostrato sul telefono.');
  stopCamera();
  await peer.setRemoteDescription({type:'answer',sdp:await unpack(code.slice(7))});
  statusText('Collegamento diretto in corso…');
  if (channel.readyState !== 'open') pairingTimer = setTimeout(() => fail(Error('I browser non riescono a comunicare. Controlla che siano sulla stessa rete e che la rete consenta il collegamento tra dispositivi.')),30000);
}
async function scan() {
  if (cameraStream) { stopCamera(); return; }
  try {
    cameraStream = await navigator.mediaDevices.getUserMedia({video:true,audio:false});
    const video = $('camera'); video.srcObject = cameraStream; video.hidden = false; await video.play();
    const canvas = document.createElement('canvas'), ctx = canvas.getContext('2d',{willReadFrequently:true});
    function frame() {
      if (!cameraStream) return;
      if (video.videoWidth) {
        canvas.width = video.videoWidth; canvas.height = video.videoHeight;
        ctx.drawImage(video,0,0);
        const pixels = ctx.getImageData(0,0,canvas.width,canvas.height);
        const hit = jsQR(pixels.data,pixels.width,pixels.height);
        if (hit?.data.startsWith('PORTO1.')) { acceptCode(hit.data).catch(fail); return; }
      }
      setTimeout(frame,170);
    }
    frame();
  } catch { stopCamera(); statusText('Webcam indisponibile. Apri “Usa i codici come testo” e incolla il codice del telefono.'); }
}
async function startPC() {
  if (!window.showDirectoryPicker) { statusText('Per ricevere, apri questa pagina in Edge o Chrome su Windows.'); return; }
  const folder = await showDirectoryPicker({mode:'readwrite'});
  $('start').disabled = true;
  Porto.setFolder(folder);
  statusText('Preparo il codice del computer…');
  initPeer(); bind(peer.createDataChannel('porto-web-v1'));
  const code = await localCode(await peer.createOffer());
  const url = new URL(location.href); url.hash = 'porto1=' + code;
  $('link').value = url.href; drawQR($('offerQR'),url.href); $('pairing').hidden = false;
  statusText('Pronto per collegare l’iPhone.');
}
async function startPhone() {
  $('pc').hidden = true; $('mobile').hidden = false; $('title').textContent = 'Invia al computer';
  history.replaceState(null,'',location.pathname + location.search);
  statusText('Preparo il codice del telefono…');
  initPeer(); peer.ondatachannel = e => bind(e.channel);
  await peer.setRemoteDescription({type:'offer',sdp:await unpack(offerCode)});
  const code = 'PORTO1.' + await localCode(await peer.createAnswer());
  $('answer').value = code; drawQR($('answerQR'),code); $('answerBox').hidden = false;
  statusText('Sul PC premi “Leggi QR del telefono”.');
}
onButton('start',startPC); onButton('connect',() => acceptCode($('reply').value.trim())); onButton('scan',scan);
onButton('cancel',()=>fail(Error('Interruzione richiesta.')));
onButton('copy',async () => { await navigator.clipboard.writeText($('answer').value); statusText('Codice copiato. Incollalo sul computer.'); });
Porto.setup();
window.addEventListener('hashchange', () => {
  if (location.hash.startsWith(offerPrefix)) location.reload();
});
if (senderMode) startPhone().catch(fail);

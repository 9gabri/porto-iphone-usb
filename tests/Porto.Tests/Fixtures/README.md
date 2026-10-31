# Media di prova generati

Tre clip di 0,6 secondi, 64×64 pixel e 5 fotogrammi al secondo, senza audio.
Immagini piatte generate localmente con FFmpeg già installato; nessun video
personale o contenuto scaricato. Decodifica completa delle tre clip verificata
con `ffmpeg -nostdin -hide_banner -loglevel error -i FILE -f null NUL`.

- synthetic-h264.mov: sorgente lavfi color=blue, libx264, yuv420p, faststart.
- synthetic-h264.m4v: stessa traccia H.264, remux in contenitore ipod/M4V.
- synthetic-hevc.mp4: sorgente lavfi color=green, libx265, yuv420p, hvc1, faststart.

I test copiano e rileggono queste clip senza conversione. FFmpeg non viene
distribuito con Porto e non è una dipendenza dell'app o del test runner.
Queste clip non sostituiscono un test di riprese provenienti da un iPhone reale.

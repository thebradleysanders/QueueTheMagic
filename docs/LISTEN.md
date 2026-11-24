# Listen Live (show audio on visitors' phones)

Visitors tap **Listen** on the home page and hear the show through their phone, about **0.15 to 0.3 seconds** behind the lights. It's meant for people standing outside without an FM radio. Keep your FM transmitter for people in cars.

**No port forwarding and no extra servers.** The audio goes through the site's normal address, just like the rest of the page.

## How it works

```
FPP ──Opus RTP (UDP 5004, your LAN)──▶ QTM API ──WebSocket (site's normal port)──▶ phones
```

- FPP sends the show audio as **Opus RTP** to the QTM server on your local network.
- The QTM API strips the RTP headers and passes the Opus packets straight to every listener over a WebSocket. Nothing is re-encoded, and relaying adds about 1 ms.
- The phone decodes Opus and plays it with a small buffer (about 120 ms). The buffer adjusts itself:
  - if the connection stalls, it restarts with a slightly bigger cushion (never more than 0.4 s);
  - if audio piles up after a stall, it skips ahead, so the delay never creeps up during a song.
- The Listen button only appears while audio is actually arriving from FPP.

### Where the delay comes from

| Step | Typical |
|---|---|
| FPP to QTM server (LAN) | under 1 ms |
| Relay inside QTM | about 1 ms |
| Server to phone (Wi-Fi / 4G / 5G) | 10 to 80 ms |
| Phone's playback buffer | about 120 ms (adjusts on bad signal) |
| Phone's audio output | 10 to 40 ms (Bluetooth headphones add 100 to 200 ms) |

## 1. FPP

In FPP 10.x, open the **PipeWire Audio Network Streams** settings, enable **Opus RTP Audio Streaming**, and set:

| Setting | Value |
|---|---|
| Destination / host | The **LAN** IP of the machine running QTM |
| Port | `5004` (or whatever you set `LISTEN_RTP_PORT` to) |

The other FPP options (AES67, VBAN) are not used.

## 2. docker-compose

Nothing to set. The compose file already maps `5004/udp`. If you need a different port, set `LISTEN_RTP_PORT` in `.env`.

Do **not** port-forward 5004 on your router. Only FPP on your LAN needs to reach it.

If the site is behind your own reverse proxy (Caddy, Traefik, Nginx Proxy Manager, Cloudflare Tunnel), make sure WebSockets are allowed for `/api/listen/ws`. The live queue already needs this for `/showHub`, so it's usually already on.

## 3. Turn it on

**Admin → Config → Listen live**: tick **Show the Listen button** and set **Max listeners**. The badge at the top of that section shows whether audio is arriving and how many people are listening.

## Bandwidth

Each listener uses your **upload** at FPP's Opus bitrate, usually 64 to 128 kbps. 20 listeners at 96 kbps is about 2 Mbps. Lower **Max listeners** if your connection is small. Once it's full, new visitors are told to use the radio instead.

## Troubleshooting

| Symptom | Check |
|---|---|
| Admin badge says "No audio from FPP" | FPP's Opus RTP destination IP and port; the server firewall allows UDP 5004 in; a sequence with audio is playing. `docker logs qtm` prints "receiving audio from …" when packets arrive. |
| Button appears but nothing connects | Your reverse proxy must allow WebSockets on `/api/listen/ws` |
| Audio is choppy | Weak cell signal; lower the Opus bitrate in FPP |
| Audio stops when the phone locks | iPhones pause web audio when locked; keep the screen on |
| Noticeable delay | Bluetooth headphones or car Bluetooth add delay; use the phone speaker or wired headphones |

## Security notes

- UDP 5004 accepts audio from your LAN only (don't forward it). Anyone on your LAN could send audio to it, the same as they could to FPP.
- Listeners only receive audio and can't send anything that's acted on.
- New connections are rate-limited per IP, and the listener count is capped.
- The site's content security policy allows `wasm-unsafe-eval`, because the Opus decoder that phones use is WebAssembly.

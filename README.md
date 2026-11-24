# QueueTheMagic

[<img src="https://img.shields.io/badge/DockerHub-latest-sucess.svg?logo=Docker">](https://hub.docker.com/r/thebradleysanders/queue-the-magic)

A real-time holiday light show song request and donation web app. Visitors pay via Stripe to queue songs played by [Falcon Player (FPP)](https://falconchristmas.com/). A default playlist runs between requests. Includes an admin dashboard with live queue management, reporting, and MQTT integration.

**Built by [Brad Sanders](https://github.com/thebradleysanders)**

## Goal
I often get asked 
 * 'how can I donate?'
 * 'how can I see the song list, when can I see it?'
 * 'I love x song from last year, can I see it again?'
   
 I have solved this with a simple self-hosted website users can access from social media or by simple scanning a QR code, for example a yard sign.

---

## Screenshots
![App Screenshots](QueueTheMagicWebApp.png)

## Features

- Song request queue with [Stripe](https://stripe.com/) payment ($1+ per request)
- Bump queue position with a donation
- "Just Because" donations (no song required)
- Live queue updates via SignalR WebSockets
- FPP integration, controls playback directly, import sequences, playlists & schedule.
- Weekly show schedule (hours of operation)
- Season on/off toggle with custom off-season message
- Song ratings with admin report
- Visitor stats: visits and unique devices (anonymous), per day, in the admin Reports tab
- Admin dashboard: config, reports, live queue, diagnostics
- MQTT event publishing (optional) for phone alerts, Home Assistant, signs and more. **[See the MQTT guide](docs/MQTT.md)**
- "Listen live" phone audio in sync with the lights, relayed from FPP with no port forwarding (optional). **[See the Listen guide](docs/LISTEN.md)**
- Friends & family passes: give people a personal code to sign in, see the show before it opens and pick songs for free, any time (Admin → Config → Friends & family)
    - ```test``` - When the test button is clicked in the UI.
    - ```donation``` - When a 'Just because' donation occurs.
    - ```nowplaying``` - When now playing changes.
    - ```songqueued``` - When someone donates to queue a song.
    - ```visit``` - When a device opens the site (once per device per 30 minutes).
- Dark theme with **automatic seasonal themes** (Christmas, Halloween, Valentine's, St. Patrick's, Spring, 4th of July, Autumn, Winter) with a matching string of lights and falling decorations
- Mobile-first responsive design

---

## Docker Setup

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/) and Docker Compose
- A [Stripe](https://stripe.com) account (test keys work fine for development)
- [Falcon Player](https://github.com/FalconChristmas/fpp) running on your network
- A reverse proxy like [Traefik](https://github.com/traefik/traefik) or [Nginx Proxy Manager](https://github.com/NginxProxyManager/nginx-proxy-manager), for HTTPS (Required for [Stripe](https://stripe.com))
    - Ensure websocket support is enabled. 

### 1. Create a docker-compose.yml

Create a new folder and add this file, no repo clone needed:

```yaml
services:
  app:
    image: thebradleysanders/queuethemagic:latest
    container_name: qtm
    ports:
      - "80:80"
    environment:
      - Stripe__SecretKey=sk_live_...
      - Stripe__PublishableKey=pk_live_...
      - AdminPassword=your-secure-password
      - Jwt__Secret=change-this-to-a-long-random-string-min-32-chars
      - AllowedOrigins=http://your-server-ip
    volumes:
      - qtm-data:/app/data
    restart: unless-stopped

volumes:
  qtm-data:
    name: qtm-data
```

### 2. Start the container

```bash
docker compose up -d
```

### 3. First-time setup

1. Open `http://your-server-ip` and the public site is live
2. Go to `http://your-server-ip/admin` and log in with your `AdminPassword`
3. In **Config → Falcon Player (FPP)**:
   - Verify the FPP Address matches your player
   - Click **Import Playlists & Songs** to sync your FPP library
   - Select your **Default Playlist** from the dropdown
4. Set your show schedule, site name, FM station, and pricing
5. Click **Save Configuration**

### Useful commands

```bash
# View logs
docker compose logs -f

# Stop
docker compose down

# Stop and delete all data
docker compose down -v

# Pull latest images and restart
docker compose pull && docker compose up -d
```

---

## Environment Variables

All configuration is passed via environment variables, no config files to edit.

| Variable | Default | Description |
|---|---|---|
| `Stripe__SecretKey` | - | Stripe secret key (server-side) |
| `Stripe__PublishableKey` | - | Stripe publishable key (returned to frontend via API) |
| `AdminPassword` | - | Password for `/admin` login. **Required**, min 8 characters |
| `Jwt__Secret` | - | Token signing secret. **Required**, min 32 characters, keep private (`openssl rand -base64 48`) |
| `AllowedOrigins` | `https://localhost` | CORS origin (your domain or IP) |

> In production the app **refuses to start** if `AdminPassword` or `Jwt__Secret` is missing, too short, or a well-known placeholder (`changeme`, `change-this…`). This is deliberate, so a live site can never run with default credentials.

> FPP address and all other show settings are configured via the admin dashboard and stored in the database.

---

## Seasonal Themes

The site picks its theme from the calendar, no configuration needed:

| Dates | Theme |
|---|---|
| Oct 1 – Nov 3 | 🎃 Halloween |
| Nov 4 – Nov 14 | 🍁 Autumn |
| Nov 15 – Dec 31 | 🎄 Christmas |
| Jan 1 – Jan 31 | ⛄ Winter |
| Feb 1 – Feb 14 | 💝 Valentine's Day |
| Mar 1 – Mar 17 | 🍀 St. Patrick's Day |
| Mar 18 – May 31 | 🌷 Spring |
| Jun 25 – Jul 6 | 🎆 4th of July |
| Any other time | ✨ Default |

Preview any theme by adding `?season=` to the URL, e.g. `https://your-site/?season=halloween`. Dates and colours live in `web/src/composables/useSeason.js` and `web/src/assets/main.css`.

---

## Security Notes

- Every payment is verified server-side with Stripe: it must have succeeded, match the type (song / boost / donation) and the specific song, cover the configured price, and can only be redeemed **once**. Donation amounts come from Stripe, never from the browser.
- Admin login, payment-intent creation and song rating are rate limited per client IP. nginx only trusts `X-Forwarded-For` from private-network proxies.
- nginx sends a Content-Security-Policy (own assets + Stripe only), `nosniff`, frame and referrer protections. If you add third-party scripts or fonts, extend the CSP in `docker/nginx.conf.template`.
- Terminate HTTPS at your reverse proxy and enable HSTS there.
- Admin tokens last 8 hours. Rotate `Jwt__Secret` to sign everyone out.

---

## Data Persistence

All data (songs, queue history, donations, ratings, configuration) is stored in a named Docker volume `qtm-data`. It survives container restarts and image updates.

**Backup:**
```bash
docker run --rm -v qtm-data:/data -v $(pwd):/backup alpine \
  tar czf /backup/qtm-backup.tar.gz -C /data .
```

**Restore:**
```bash
docker run --rm -v qtm-data:/data -v $(pwd):/backup alpine \
  tar xzf /backup/qtm-backup.tar.gz -C /data
```

---

## FPP Integration

QueueTheMagic communicates directly with your Falcon Player over HTTP. Make sure:

- FPP is reachable from the Docker host at the IP set in `FppAddress`
- FPP API is enabled (it is by default on all FPP installs)
- After adding or removing sequences in FPP, click **Import Playlists & Songs** in the admin config to re-sync

The app polls FPP every 5 seconds (configurable in admin). When a user song is queued, the poller wakes immediately and starts playback. When the song ends, the default playlist resumes automatically.

---

### Local development (without Docker)

**Backend:**
```bash
cd api
dotnet restore
dotnet run   # http://localhost:5288
```

**Frontend:**
```bash
cd web
npm install
npm run dev  # http://localhost:5173
```

Create `web/.env.local`:
```env
VITE_API_BASE_URL=http://localhost:5288
```

---

## Stack

- **Backend**: ASP.NET Core 9, SQLite, Entity Framework Core, SignalR, Stripe.net, MQTTnet
- **Frontend**: Vue 3, Pinia, PrimeVue, Tailwind CSS v4, Vite
- **Infrastructure**: Docker, nginx, SQLite

---

## License

MIT

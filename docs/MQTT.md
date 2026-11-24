# MQTT Integration

QueueTheMagic can announce what is happening on your show to an [MQTT](https://mqtt.org/) broker. MQTT is a tiny publish/subscribe messaging system that smart-home and hobby-electronics tools speak. It is **optional**, one-way (QueueTheMagic only publishes), and the show works exactly the same with it off.

## Why would I want this?

Things people do with it:

- **Phone alerts**: get a notification the moment someone donates, requests a song, or opens the site.
- **Make the yard react**: flash a porch light, ring a bell, or fire a relay on every donation (Home Assistant, ESPHome, Node-RED).
- **Show the song on a sign**: an ESP32 or e-ink display near the road that shows "Now playing: …" and "Next up: …".
- **Post to chat**: send requests and donations to Discord, Slack or Telegram through Node-RED.
- **Log it**: store events in a database or spreadsheet for end-of-season stats.

If none of that sounds useful, leave MQTT turned off.

## Setup

1. You need a broker on your network. [Mosquitto](https://mosquitto.org/) is the usual choice (Home Assistant has an add-on for it).
2. In **Admin → Config → MQTT**:
   - tick **Enable MQTT**
   - enter the broker **host** and **port** (default `1883`)
   - enter a username and password if your broker needs them
   - choose a **topic prefix** (default `qtm`)
3. **Save Configuration**, then press **Send Test Message**.

To watch messages arrive from any computer with Mosquitto's tools installed:

```bash
mosquitto_sub -h <broker-ip> -u <user> -P <password> -t 'qtm/#' -v
```

## Topics

Every topic is `<prefix>/<event>`. Payloads are JSON. Messages are not retained, so a subscriber only sees events that happen while it is connected.

| Topic | Sent when | Payload |
|---|---|---|
| `qtm/songqueued` | Someone pays to request a song | `{"title":"Jingle Bells","artist":"","amount":1.00,"position":3,"timestamp":"2026-12-01T00:15:00Z"}` |
| `qtm/donation` | Someone makes a "Just Because" donation | `{"amount":5.00,"type":"just_because"}` |
| `qtm/nowplaying` | A **requested** song starts playing | `{"title":"Jingle Bells","artist":"","playing":true}` |
| `qtm/nowplaying` | A requested song finishes | `{"title":null,"playing":false}` |
| `qtm/visit` | A device opens the site (see below) | `{"newDevice":true,"visitsToday":42,"timestamp":"2026-12-01T00:15:00Z"}` |
| `qtm/cheer` | Someone taps **Cheer** on the home page (max once per 2 s per phone) | `{"cheersToday":212,"timestamp":"2026-12-01T00:15:00Z"}` |
| `qtm/status` | The show mode changes or config is saved (**retained**) | `{"mode":"auto","seasonActive":true,"open":true,"running":true,"seasonStart":"2026-12-01","seasonEnd":"2027-01-05"}` |
| `qtm/test` | You press the test button in admin | `{"message":"QueueTheMagic MQTT test","timestamp":"..."}` |

### Controlling the show

QueueTheMagic also **listens** on one topic, so Home Assistant (or anything else) can switch the show:

| Topic | Payload | Effect |
|---|---|---|
| `qtm/control/show` | `on` | Force the show open now, ignoring the season dates and weekly hours |
| `qtm/control/show` | `off` | Force the show closed |
| `qtm/control/show` | `auto` | Go back to following the season dates and weekly hours |

`{"mode":"on"}` also works. The new state is echoed on `qtm/status`. Anyone who can publish to your broker can switch the show, so use broker credentials and ACLs.

```bash
mosquitto_pub -h <broker-ip> -u <user> -P <password> -t 'qtm/control/show' -m on
```

Notes:

- `nowplaying` reports requested songs only. Songs from your default playlist are not announced.
- Payloads never contain visitor identifiers or payment details.
- Amounts are in US dollars.
- If the broker is unreachable, publishing fails quietly (logged as a warning) and the show is unaffected.

### What counts as a visit

Each time someone opens the site on a device it counts as one visit. A device is one browser, identified by a random anonymous ID, with no IP address or personal data stored. If the same device opens the site again within 30 minutes (a refresh, switching apps and back), that is the same visit, so stats and MQTT aren't flooded. `newDevice` is `true` the first time that device has ever been seen. Admin pages are never counted.

The same numbers appear in **Admin → Reports** (visits today, visits and unique devices over 30 days, and a daily chart).

## Examples

### Home Assistant: phone notification on donation

```yaml
automation:
  - alias: "Light show donation"
    trigger:
      - platform: mqtt
        topic: "qtm/donation"
    action:
      - service: notify.mobile_app_your_phone
        data:
          title: "Thank you!"
          message: "New ${{ trigger.payload_json.amount }} donation"
```

### Home Assistant: flash a light on every song request

```yaml
automation:
  - alias: "Song requested"
    trigger:
      - platform: mqtt
        topic: "qtm/songqueued"
    action:
      - service: light.turn_on
        target: { entity_id: light.porch }
        data: { flash: short }
```

### Home Assistant: let the audience flash a prop

```yaml
automation:
  - alias: "Audience cheer"
    mode: single          # ignore cheers while the flash is still running
    trigger:
      - platform: mqtt
        topic: "qtm/cheer"
    action:
      - service: switch.turn_on
        target: { entity_id: switch.strobe }
      - delay: "00:00:02"
      - service: switch.turn_off
        target: { entity_id: switch.strobe }
```

### Home Assistant: show on/off switch

```yaml
mqtt:
  switch:
    - name: "Light Show"
      command_topic: "qtm/control/show"
      state_topic: "qtm/status"
      value_template: "{{ value_json.mode }}"
      payload_on: "on"
      payload_off: "off"
      state_on: "on"
      state_off: "off"
```

### Home Assistant: count visitors on a dashboard

```yaml
mqtt:
  sensor:
    - name: "Light Show Visits Today"
      state_topic: "qtm/visit"
      value_template: "{{ value_json.visitsToday }}"
```

### Home Assistant: "now playing" sensor for a dashboard or sign

```yaml
mqtt:
  sensor:
    - name: "Light Show Now Playing"
      state_topic: "qtm/nowplaying"
      value_template: "{{ value_json.title if value_json.playing else 'Idle' }}"
```

## Security

- The connection is plain TCP (no TLS), so keep the broker on your local network and don't expose it to the internet.
- Use a broker username and password, and give them publish-only access to your prefix if your broker supports ACLs.
- The MQTT password is stored in the app's database and is visible to anyone who can log in to admin.

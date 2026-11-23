# MQTT Integration

QueueTheMagic can announce what is happening on your show to an [MQTT](https://mqtt.org/) broker. MQTT is a tiny publish/subscribe messaging system that smart-home and hobby-electronics tools speak. It is **optional**, one-way (QueueTheMagic only publishes), and the show works exactly the same with it off.

## Why would I want this?

Things people do with it:

- **Phone alerts**: get a notification the moment someone donates or requests a song.
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
| `qtm/test` | You press the test button in admin | `{"message":"QueueTheMagic MQTT test","timestamp":"..."}` |

Notes:

- `nowplaying` reports requested songs only. Songs from your default playlist are not announced.
- Payloads never contain visitor identifiers or payment details.
- Amounts are in US dollars.
- If the broker is unreachable, publishing fails quietly (logged as a warning) and the show is unaffected.

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

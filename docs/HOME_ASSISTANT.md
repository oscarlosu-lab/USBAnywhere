# Home Assistant, Stream Deck & AI Automation Guide

USBAnywhere exposes two local automation interfaces without requiring cloud relays:

1. **Loopback Local Control REST API** (`127.0.0.1:13241`) on the Windows Client.
2. **Stdio Model Context Protocol (MCP) Server** (`scripts/usbanywhere-mcp.py`) for AI assistants and local scripts.

## 1. Windows Client Local Control API

Enable `LocalApiEnabled` and configure `LocalApiToken` in your client configuration (`127.0.0.1:13241`). When `LocalApiToken` is set, include `Authorization: Bearer <token>` on every request.

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/v1/status` | Driver state, connected devices count, favorites count, active streaming state |
| `GET` | `/api/v1/doctor` | Overall health check JSON (`healthy`, `driverInstalled`, `autoDetachOnSleep`) |
| `GET` | `/api/v1/connected` | List currently attached USB/IP ports |
| `GET` | `/api/v1/favorites` | List saved favorite devices & `attachMode` |
| `POST` | `/api/v1/connect` | Body: `{"serverIp":"203.0.113.10","busId":"1-2","mode":"low-latency"}` |
| `POST` | `/api/v1/detach` | Body: `{"port":0}` or `{"all":true}` |
| `POST` | `/api/v1/stream` | Body: `{"active":true,"provider":"apollo"}` |

### Home Assistant `configuration.yaml` Example

```yaml
rest_command:
  usbanywhere_attach_simwheel:
    url: "http://127.0.0.1:13241/api/v1/connect"
    method: POST
    headers:
      Authorization: !secret usbanywhere_api_bearer
      Content-Type: "application/json"
    payload: '{"serverIp":"203.0.113.10","busId":"1-2","mode":"low-latency"}'

  usbanywhere_detach_all:
    url: "http://127.0.0.1:13241/api/v1/detach"
    method: POST
    headers:
      Authorization: !secret usbanywhere_api_bearer
      Content-Type: "application/json"
    payload: '{"all":true}'

sensor:
  - platform: rest
    name: USBAnywhere Status
    resource: "http://127.0.0.1:13241/api/v1/status"
    headers:
      Authorization: !secret usbanywhere_api_bearer
    value_template: "{{ value_json.connectedCount }}"
    json_attributes:
      - driverInstalled
      - streamActive
      - streamProvider
```

## 2. Streaming Handoff Providers

Both the Windows Client (`StreamingSessionMonitor`) and Android TV App (`SunshineHandoffServer`, port `3242`) recognize:
- `sunshine`
- `apollo`
- `vibepollo`
- `vibeshine`
- `parsec`
- `steam_remote_play`

Set `AttachMode = "only-during-stream"` on favorites that should auto-attach when a stream starts and auto-detach when the stream ends.

## 3. MCP Server (`usbanywhere-mcp.py`)

```json
{
  "mcpServers": {
    "usbanywhere": {
      "command": "python3",
      "args": ["/usr/local/bin/usbanywhere-mcp.py"]
    }
  }
}
```

# SignalR Realtime Connection

## Hub URL and configuration

- **Backend hub path:** `GET /hubs/tickets` (negotiate) then WebSocket/SSE/LongPolling.
- **Full hub URL:** `{API_BASE}/hubs/tickets` (e.g. `http://localhost:5000/hubs/tickets`).

The frontend **does not** use `NEXT_PUBLIC_API_BASE_URL` directly for SignalR. It uses the **same resolved base as the REST API** via `getApiBaseUrl()` from `lib/api-client.ts` (which may detect port 5000 or 5001 and cache). So the hub always connects to the same host/port as `/api/*` requests, avoiding 1006 from URL mismatch.

- **Env (optional):** `NEXT_PUBLIC_API_BASE_URL` – if set, the app uses it after health-check; otherwise it detects (e.g. `http://localhost:5000` or `http://localhost:5001`). The SignalR hub URL is `getApiBaseUrl() + "/hubs/tickets"`.

## Backend

- **Hub:** `Ticketing.Backend.Infrastructure.Hubs.TicketHub`
- **Route:** `app.MapHub<TicketHub>("/hubs/tickets").RequireCors("DevCors");`
- **Auth:** `[Authorize]` on the hub; JWT is read from `access_token` query (see `OnMessageReceived` in Program.cs for `/hubs/tickets`).
- **CORS:** Policy `DevCors` allows configured origins, `AllowCredentials()`, `AllowAnyHeader()`, `AllowAnyMethod()`. Development adds `http(s)://localhost:3000`, `3001`, `127.0.0.1`, etc.
- **WebSockets:** `app.UseWebSockets()` is called before `MapHub`.

## Frontend

- **Client:** `@microsoft/signalr` in `hooks/use-signalr.ts`.
- **Provider:** `RealtimeProvider` in `lib/realtime-context.tsx` wraps the app and uses `useSignalR()`.
- **Transports:** WebSockets, ServerSentEvents, LongPolling (negotiation chooses; no `skipNegotiation`).
- **Reconnect:** `withAutomaticReconnect([0, 2000, 5000, 10000, 30000])`.
- **Single connection:** One connection per app instance; cleanup on unmount and when token is removed.

## If you see "WebSocket closed with status code: 1006"

1. **Hub URL mismatch** – Fixed by using `getApiBaseUrl()` for the hub URL so it matches the backend the app talks to.
2. **Backend not running or different port** – Start backend; ensure frontend and backend agree on port (check Network tab: WS request host/port).
3. **CORS** – Backend must allow the frontend origin and credentials; `DevCors` includes `http://localhost:3000` etc. in development.
4. **Auth** – Token must be valid; hub uses same JWT as REST. If token expires, connection may close; reconnect will use new token after login.
5. **Proxy** – If using nginx/reverse proxy in front of the backend, add WebSocket upgrade headers for `/hubs/tickets`:
   - `proxy_http_version 1.1`
   - `proxy_set_header Upgrade $http_upgrade`
   - `proxy_set_header Connection "upgrade"`
   - `proxy_read_timeout` (e.g. 3600)

## Verification

1. Start backend and frontend.
2. Open DevTools → Network → filter "WS" (or "All" and look for `hubs/tickets`).
3. After login you should see one WebSocket (or SSE/LongPolling) to `{backend}/hubs/tickets`.
4. No repeated 1006; if WS fails, client should fall back to SSE or LongPolling and stay connected.
5. Backend logs: On connect, `TicketHub: User ... connected`. On disconnect with error, `TicketHub: User ... disconnected with error` with exception type and message.

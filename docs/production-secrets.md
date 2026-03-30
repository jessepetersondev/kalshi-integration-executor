# Production secret handling

The executor now supports production-safe Kalshi credential injection without relying on repo-local key files.

## Supported secret injection patterns

### 1. Direct environment variables

Use these when your runtime can inject multiline secrets safely:

```bash
export KALSHI_ACCESS_KEY_ID="..."
export KALSHI_PRIVATE_KEY_PEM="$(cat /secure/path/kalshi-private-key.pem)"
```

### 2. Base64-encoded environment variable

Useful when multiline PEM handling is awkward:

```bash
export KALSHI_ACCESS_KEY_ID="..."
export KALSHI_PRIVATE_KEY_PEM_BASE64="$(base64 -w0 /secure/path/kalshi-private-key.pem)"
```

### 3. Mounted secret file path

Use this when the orchestrator mounts a secret into the container or VM:

```bash
export KALSHI_ACCESS_KEY_ID="..."
export KALSHI_PRIVATE_KEY_PATH="/run/secrets/kalshi/private-key.pem"
```

## Resolution order

The executor resolves Kalshi secrets in this order:

1. explicit config value
2. configured environment variable
3. base64 PEM environment variable (for the private key)
4. readable file path

## Production guardrails

- committed `appsettings*.json` files no longer point at repo-local private key files
- outside `Development`, relative private-key paths are rejected at startup
- startup validation fails fast when access key ID or private key material is missing or invalid
- the private key must parse as a valid RSA PEM before the worker starts

## Local development

For local dev you can still use a readable file path, but prefer env injection so your local workflow matches production more closely.

## Docker compose example

The included `docker-compose.yml` expects secrets to be injected via environment variables instead of a checked-out key file.

For example:

```bash
export KALSHI_ACCESS_KEY_ID="..."
export KALSHI_PRIVATE_KEY_PEM_BASE64="$(base64 -w0 ~/secrets/kalshi-private-key.pem)"
docker compose up --build
```

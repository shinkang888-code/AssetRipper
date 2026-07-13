# Self-hosting AssetRipper's web GUI (with PMKE support)

This directory contains a container setup for running AssetRipper's browser-based GUI as a
self-hosted service. The build automatically includes the `PmkeBundleScheme`, so PMKE-encrypted
AssetBundles are detected and decrypted transparently — no extra configuration.

## Quick start

```bash
docker build -f deploy/Dockerfile -t assetripper-web .
docker run --rm -p 8080:8080 \
  -e AR_PMKE_KEY=<32-hex-character-key> \
  -v /path/to/game:/data:ro \
  assetripper-web
# open http://localhost:8080  (enter /data as the game path)
```

## PMKE key (required for encrypted bundles)

The PMKE AES-128 key is game/build specific and is **not** embedded in source. Provide it at
runtime via the `AR_PMKE_KEY` environment variable (32 hex characters). Without it, PMKE files are
reported as failed with a message telling you to set the variable; all other bundles still work.

## What changed to make this deployable

The desktop app binds to `127.0.0.1` and opens a local browser. Two things enable headless/container use:

- `--headless` — skips the automatic browser launch (there is no desktop in a container).
- `AR_BIND_HOST=0.0.0.0` — the launcher now reads this environment variable to choose the bind
  address (defaults to `127.0.0.1`, so existing desktop behavior is unchanged).

The image publishes `AssetRipper.GUI.Free` framework-dependent (`PublishAot=false`) onto the
ASP.NET 10 runtime image, which avoids needing the native AOT toolchain during `docker build`.

## Important limitations (read before exposing this)

AssetRipper's web GUI is a **single-user local tool**, not a multi-tenant web service:

- **No authentication.** Anyone who can reach the port has full access. Keep it on a trusted
  network or put an authenticating reverse proxy in front.
- **Whole game loaded into server RAM.** Memory scales with the game size; concurrent users are
  not isolated. Size the host accordingly and treat it as one-session-at-a-time.
- **File access.** The GUI's native folder picker does not work headless; load games by path that
  is mounted into the container, e.g. `-v /path/to/game:/data:ro` and enter `/data` in the UI.

For a public, multi-user SaaS these would need real work (auth, upload handling for multi-GB
games, per-session sandboxing and resource limits). For personal/self-host use, the container
above is sufficient.

## Two deployment tiers

| Need | Use | Deployable |
| --- | --- | --- |
| Just decrypt a PMKE bundle | The static browser decryptor (client-side, no server) | Any static host / already live as an Artifact |
| Full asset extraction | This container (self-host) | Personal / trusted-network self-host |

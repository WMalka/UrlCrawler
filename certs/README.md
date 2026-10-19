# Extra trusted CA certificates (optional)

Put PEM files named `*.crt` here to add them to the trust store of the API and Worker images at build time.
Needed only behind a TLS-intercepting proxy or filter (corporate proxy, NetFree, ...): without it, HTTPS
pages fail in the containers with `Network error: SecureConnectionError`.

Files in this folder are git-ignored (except this README and `.gitkeep`). Rebuild after changes:
`docker compose build api worker`.

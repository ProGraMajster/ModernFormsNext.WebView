# Owned native security fixture

This dependency-free console process binds two random IPv4 loopback ports, emits one
JSON startup record and expires after three minutes. HTTP `/state` reports bounded
request counters. `/download-<test-guid>.txt` serves a harmless attachment. TLS uses an
short-lived self-signed localhost/127.0.0.1 certificate; nothing is added to an OS trust
store and browser validation is never overridden. Only negative certificate tests may
use this listener. Do not interpret a failed connection as certificate denial without
the browser's typed Certificate result and a positive server connection counter.

Build with the same frozen source-root property as repository tools (the repository
build guard still applies). Keep the process/logs/profile owned by the test. An Android
test can temporarily reverse **only these ports** on its explicitly selected emulator
or approved test phone, then remove those exact mappings. Never use `reverse --remove-all`.
This is not a product request interceptor, download service or certificate exception API.

Windows Schannel rejects ephemeral server private keys. The fixture imports its own
PKCS12 bytes into the default temporary key set, clears those bytes, and disposes the
certificate/key at process exit; it never uses `PersistKeySet` or installs a trusted
certificate. A broken server handshake is a fixture FAIL, not a TLS-denial PASS.

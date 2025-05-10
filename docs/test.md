# 🧪 Test Strategy for certfwd

**certfwd** is intentionally minimal in structure and dependencies — designed to be readable, portable, and practical. This makes automated testing both challenging and rewarding.

This document outlines the philosophy and methods used to test certfwd reliably across platforms without compromising its simplicity.

---

## ✅ Guiding Principles

- **Test what users rely on**, not what’s convenient to isolate
- **Avoid mocks or abstractions that exist only for testing**
- **End-to-end tests are the most valuable** for certfwd’s behavior
- **Cross-platform consistency is critical** — certfwd is infrastructure

---

## 🧪 End-to-End Testing via GitHub Actions

certfwd uses a dedicated workflow to validate TLS client certificate handling across:

- **Linux** (with CoreFX-compatible X509Store)
- **Windows** (via PowerShell and `Cert:\CurrentUser\My`)
- **macOS** (with expected failure due to `security import` restrictions)

Workflow file: `.github/workflows/certfwd-certstore-test.crossplatform.yml`

### Scenarios tested:

| OS       | Certificate Behavior                          | Expected Outcome                                 |
|----------|------------------------------------------------|--------------------------------------------------|
| Linux    | Cert created and stored in CoreFX path         | Must be used and logged                          |
| Windows  | Cert created with `New-SelfSignedCertificate`  | Must be used and logged                          |
| macOS    | Cert not imported (sandbox restriction)        | Must log `[ERROR] Certificate not found`         |

This validates both success and failure paths of `FindCertificate(...)` without mocks.

---

## 🧩 Future Testing Ideas

- Validate response encoding with `--preserve-encoding`
- Confirm `--log-body=false` prevents body log lines
- Add CLI argument parser tests (e.g. `--help`, `--version`)
- Introduce a basic test harness in `tests/end-to-end/`

---

## 📌 Manual Testing Still Matters

certfwd is often used in complex environments (e.g. SOAP APIs, hardware-bound TLS). Manual QA under realistic conditions (VPN, client certs, etc.) is still an important part of release readiness.


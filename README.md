# 🧭 certfwd

**certfwd** is a lightweight, standalone reverse proxy tool for forwarding SOAP/TLS requests with injected client certificates and path-aware forwarding. Ideal for cases where the client cannot natively handle certificates appropriately, or where you want full visibility and control over secure traffic.

Designed to work equally well on Windows, Linux and Mac, without dependencies.

[![Latest Release](https://img.shields.io/github/v/release/bemafred/certfwd?label=release)](https://github.com/bemafred/certfwd/releases)
[![Build Status](https://github.com/bemafred/certfwd/actions/workflows/certfwd-release.yml/badge.svg)](https://github.com/bemafred/certfwd/actions/workflows/certfwd-release.yml)
[![License](https://img.shields.io/github/license/bemafred/certfwd)](https://github.com/bemafred/certfwd/blob/main/LICENSE)

---

## ✨ Features

- Self-contained, single-file executable (`.exe` or ELF)
- TLS client certificate support via system store or `.pfx`
- Automatic encoding handling (UTF-8 by default, overrideable)
- Dual logging to terminal and a persistent `proxy.log`
- Supports both text and binary SOAP/XML
- Cross-platform, no .NET runtime required

---

## 🚀 Usage

```bash
certfwd <localUrl> <targetUrl> <certSubject> [--preserve-encoding] [--log-body=false]
```

### Arguments

| Argument            | Description                                                        |
|---------------------|--------------------------------------------------------------------|
| `localUrl`          | Local URL to listen on, e.g. `http://localhost:5000/`              |
| `targetUrl`         | Remote URL to forward requests to, e.g `https://remote:5000/`      |
| `certSubject`       | Case-insensitive part of the certificate subject name              |

### Options

| Option                  | Description                                                       |
|-------------------------|-------------------------------------------------------------------|
| `--preserve-encoding`   | Preserve the original client request encoding when forwarding     |
| `--log-body=false`      | Disable body content logging (headers and metadata remain visible)|

---

## 🔐 Certificate Handling

### Windows
Uses certificates from X.509 `CurrentUser` and `LocalMachine` stores. Manage them using `certmgr.msc`.

### Linux
Place `.pfx` files into:
```
~/.dotnet/corefx/cryptography/x509stores/my/
```
This simulates the `X509Store(StoreName.My, StoreLocation.CurrentUser)` path for .NET.

---

## 📁 Logging

certfwd writes logs to a file in addition to the terminal.

| Platform   | Log File Path                                         |
|------------|--------------------------------------------------------|
| Windows    | `%LocalAppData%\certfwd\proxy.log`                    |
| Linux/macOS| `~/.local/share/certfwd/proxy.log`                    |

The directory is created automatically if it doesn't exist.

---

## 🔐 Download

### Binaries

| Platform       | Binary | SHA256 | 
|----------------|--------|--------|
| **Windows x64** | [certfwd-win-x64.zip](https://github.com/bemafred/certfwd/releases/latest/download/certfwd-win-x64.zip) | [sha256](https://github.com/bemafred/certfwd/releases/latest/download/certfwd-win-x64.zip.sha256) |
| **Linux x64**   | [certfwd-linux-x64.tar.gz](https://github.com/bemafred/certfwd/releases/latest/download/certfwd-linux-x64.tar.gz) | [sha256](https://github.com/bemafred/certfwd/releases/latest/download/certfwd-linux-x64.tar.gz.sha256) |
| **macOS ARM64** | [certfwd-osx-arm64.tar.gz](https://github.com/bemafred/certfwd/releases/latest/download/certfwd-osx-arm64.tar.gz) | [sha256](https://github.com/bemafred/certfwd/releases/latest/download/certfwd-osx-arm64.tar.gz.sha256) |


### 🔍 Verify download (SHA256)

Verify that the checksum of the downloaded file matches the .sha256 file included in the release.

---

### 💻 Windows (PowerShell)

```powershell
Get-FileHash .\certfwd-win-x64.zip -Algorithm SHA256
```

### 🐧 Linux (Bash)

```bash
sha256sum certfwd-linux-x64.tar.gz
```

### 🍎 macOS (Bash)

```bash
shasum -a 256 certfwd-osx-arm64.tar.gz
```

---

## 🛠 Building

To build a fully self-contained binary with trimming and AOT (Requires installed tooling for C/C++):

```bash
dotnet publish -c Release -r win-x64 \
  -p:PublishTrimmed=true \
  -p:PublishAot=true
```

Replace win-x64 with linux-x64, osx-x64, or osx-arm64 to build for Linux or macOS.

---

## 🧾 Changelog

### [v1.0.3](https://github.com/bemafred/certfwd/releases/tag/v1.0.3) – 2025-05-08
- Add CI check for workflow and security file integrity

### [v1.0.2](https://github.com/bemafred/certfwd/releases/tag/v1.0.2) – 2025-05-08
- Added trigger for Probot settings sync
  
### [v1.0.1](https://github.com/bemafred/certfwd/releases/tag/v1.0.1) – 2025-05-07
- Fixed direction arrow for proxy to client Body log

### [v1.0.0](https://github.com/bemafred/certfwd/releases/tag/v1.0.0) – 2025-05-06
- Initial release

---

## ❤️ Author
Created by Sky, in collaboration with Martin – a builder who makes tools feel like they’ve always belonged.




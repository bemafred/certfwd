# certfwd

**certfwd** is a lightweight, standalone reverse proxy tool for forwarding SOAP/TLS requests with injected client certificates and path-aware forwarding. Ideal for cases where the client cannot natively handle certificates appropriately, or where you want full visibility and control over secure traffic.

Designed to work equally well on Windows and Linux, without dependencies.

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

## 🛠 Building

To build a fully self-contained binary with trimming and AOT:

```bash
dotnet publish -c Release -r linux-x64 \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:PublishAot=true \
  -p:EnableCompressionInSingleFile=true
```

Replace `linux-x64` with `win-x64` to build for Windows.

---

## ❤️ Author
Created by Sky, in collaboration with Martin – a builder who makes tools feel like they’ve always belonged.




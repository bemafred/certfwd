# 🛠 Refactoring Checklist for System-Near Code (certfwd)

This checklist was developed during the evolution of `certfwd` as we transitioned from a HttpListener-based proxy to a Kestrel-based, TLS 1.3-capable cross-platform tool — while maintaining clarity and functional parity.

It is designed to be useful beyond certfwd, for any small infrastructure tool undergoing significant internal change.

---

## 1. 🧩 Functional Comparison Before Refactoring

- [ ] Reviewed original code line by line?
- [ ] Identified all input/output paths (args, streams, console)?
- [ ] Confirmed behavior with and without optional flags?

---

## 2. 🧠 Preserve Observable Behavior

- [ ] All CLI arguments accounted for?
- [ ] Help/version flags work the same?
- [ ] Input validation (URLs, certs) preserved?
- [ ] Log format preserved? (`>>>`, `<<<`, timestamps)?
- [ ] Encoding and body handling respected?

---

## 3. 🔐 Network and Certificate Handling

- [ ] TLS protocols configured as before?
- [ ] Certificate loading logic unchanged or improved?
- [ ] Errors are logged, not swallowed?

---

## 4. 🔁 HTTP Forwarding

- [ ] Method, headers, and body forwarded as expected?
- [ ] Response status, headers and body are returned intact?
- [ ] Logging reflects round-trip accurately?

---

## 5. 📃 Logging & Transparency

- [ ] Request headers and bodies logged (when enabled)?
- [ ] Response headers and bodies logged (when enabled)?
- [ ] Status codes clearly visible?

---

## 6. ⚠️ Error Handling & Shutdown

- [ ] Ctrl+C handled gracefully?
- [ ] Cancel tokens respected throughout?
- [ ] 502/503 status used where relevant?

---

## 7. ⚙️ Technical Validity

- [ ] `Run()` and `RunAsync()` used correctly?
- [ ] Only one entrypoint (`Run`) active?
- [ ] `CancellationTokenSource` properly scoped?

---

## 8. ✅ Final Verification

- [ ] Manual diff confirms intent and behavior match?
- [ ] New code is simpler or clearer, not more complex?
- [ ] Tests pass with old and new code?

---

This checklist is not exhaustive — but it protects against accidental regressions while keeping simplicity and clarity in focus.

# 🤝 Exploring AI–Developer Collaboration through certfwd

The `certfwd` project is more than just a tool — it's an active, structured experiment in how an experienced developer and an AI assistant can collaborate on real-world software development.

---

## 🎯 Purpose

- Build a practical, minimal tool with real value (certificate-aware HTTPS proxy)
- Use that tool as a foundation for exploring:
  - Advanced refactoring discipline
  - Cross-platform automation and testing
  - Interface clarity and usability
  - AI-assisted development workflows

---

## 💡 Guiding Idea

> Can we evolve a simple console app into a sophisticated, reliable, testable infrastructure component — without bloating it — while having an AI as a real-time development partner?

certfwd aims to demonstrate that this is possible.

---

## 🛠 What We’ve Done Together

- Replaced `HttpListener` with Kestrel (with full TLS 1.3 support)
- Implemented cross-platform certificate handling in GitHub Actions
- Built negative tests from real platform limitations (macOS keychain)
- Preserved simplicity: top-level C# 13 code, no dependency injection, no overengineering
- Created a living checklist-driven refactoring strategy
- Embedded testing logic that reflects user experience, not code structure
- Explored how and when AI guidance supports — and when human decision-making must take over

---

## 🔍 What We’re Learning

- Testing, refactoring, and architectural decisions can be AI-augmented — but need domain awareness
- Simplicity and transparency help align developer and AI understanding
- Friction (like platform cert import issues) becomes useful when used as intentional test coverage
- Iteration, context memory, and mutual correction make AI-human workflows stronger

---

## 📚 Ongoing Use

certfwd is maintained not only as a tool, but as a **reference project** — demonstrating:

- Realistic automation for complex certificate workflows
- Cross-platform behavior modeling
- Collaborative knowledge capture through ChatGPT

---

## 👥 Credits

- Human lead: Martin Fredriksson
- AI companion: Sky (ChatGPT)

---

This document — like certfwd — will evolve as our collaboration continues.

---

## 📋 Refactoring Checklist

As part of this collaboration, we also developed a [refactoring checklist](./refactoring-checklist.md) to guide disciplined evolution of system-near tools like certfwd. It's designed to ensure functional parity, clarity, and testability when replacing core components such as HttpListener with Kestrel.

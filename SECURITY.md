# Security Policy for certfwd

This document outlines the security configuration and best practices used in the `certfwd` repository to protect code integrity, release safety, and CI compliance.

---

## 🔐 Branch Protection Rules

The `main` branch is protected using a GitHub ruleset. The following policies are enforced:

### ✳️ Protection Scope

* Applies to: `main`
* Enforcement status: **Enabled**

### ✅ Enabled Rules

* [x] Require a pull request before merging

  * Required approvals: **1**
  * Merge methods: **Merge, Squash, Rebase**
* [x] Require status checks to pass

  * Required check: `build (certfwd-cross-platform.yml)`
* [x] Require linear history
* [x] Restrict deletions
* [x] Block force pushes

### 🚫 Bypass Policy

* No bypass users or roles are configured. Even repository owners must follow protections.

---

## 🔁 Recommended Developer Workflow

1. **Create a feature branch** for any change
2. Push and open a **pull request targeting `main`**
3. Let **GitHub Actions run the build**
4. Wait for ✅ status checks and review approval
5. **Merge via squash or merge**, not force-push
6. Tag releases manually with `git tag vX.Y.Z` + `git push origin vX.Y.Z`

---

## 🛡️ Release Security

All builds are:

* Triggered by **Git tags** (matching `v*`)
* Built using **GitHub-hosted runners**
* Packaged with **SHA256 checksums**

---

## 🔧 Automation Readiness

To replicate or enforce this setup via code, create a `.github/settings.yml` file using the [Probot Settings app](https://probot.github.io/apps/settings/) or manage with [GitHub CLI](https://cli.github.com/) or Terraform.

---

Maintainer: [@bemafred](https://github.com/bemafred)

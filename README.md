# Sharkey WinUI

### i should mention this is for a school project that required using AI to build an application. I did this - no, I won't be maintaining it since it's a bit of slopish

A native Windows client for [Misskey](https://misskey-hub.net/) and [Sharkey](https://activitypub.software/ahrienby/Sharkey) — built with WinUI 3 and .NET 8.

![Build & Package](https://github.com/rynizx/sharkey-winui/actions/workflows/build.yml/badge.svg)

---

## Features

### Timelines
- **Home** — notes from people you follow (+ real-time streaming)
- **Local** — public notes from the same instance
- **Social / Hybrid** — local notes + followed remote users
- **Global** — all federated public notes
- **Bubble** *(Sharkey-specific)* — notes from bubble partner servers

### Notes
- Create notes with **visibility** control (public / home / followers / specified users)
- **Content warnings** (CW)
- **Poll** creation with expiry and multiple-vote support
- **File attachments** via the Drive picker (up to 16)
- **Renote** and **quote-renote**
- **Reply** threads with paginated reply loading
- **Reactions** — Unicode emoji + custom server emoji, with live reaction counts via streaming
- **Favourites**

### Users & Social
- Full **profile view** with banner, bio, roles, profile fields, and follower/following counts
- **Follow / Unfollow / Pending** state
- **Block / Unblock** with confirmation dialog
- **Mute** users with optional expiry

### Notifications
- All Misskey notification types: follow, mention, reply, renote, quote, reaction, poll ended, follow request, achievement, role assigned, app, and more
- **Filter by type** chips (Follow / Mention / Reply / Renote / Quote / Reaction / Polls / Requests)
- **Mark all as read**
- Real-time streaming push via WebSocket

### Notification Settings
- Per-type **receive configuration** — choose who can send each notification type:
  `all` · `following` · `follower` · `mutualFollow` · `followingOrFollower` · `never`
- **Email notification** type toggles

### Account Settings
| Section | What you can change |
|---|---|
| **Profile** | Display name, bio, location, birthday, language |
| **Privacy** | Lock account, explore visibility, online status, public reactions, AI training prevention, no-crawl, following/followers list visibility |
| **Muted words** | Word/phrase lists and muted instance hostnames |
| **Security** | Change password (with 2FA support), change email address |

### ActivityPub / Federation
- Resolve any ActivityPub URI (Note or User) via `ap/show`
- Remote user profiles show their home instance badge
- Federated notes display `@user@host` format

---

## Requirements

- Windows 10 version 1809 (build 17763) or later
- .NET 8 runtime (bundled in the self-contained build)

---

## Building from source

```bash
# Restore NuGet packages (including Microsoft.WindowsAppSDK)
dotnet restore SharkeyWinUI/SharkeyWinUI.csproj

# Build
dotnet build SharkeyWinUI/SharkeyWinUI.csproj -c Release

# Run
dotnet run --project SharkeyWinUI/SharkeyWinUI.csproj
```

---

## CI / CD

GitHub Actions automatically builds, tests, and packages the app:

| Trigger | Jobs |
|---|---|
| Push / PR to `main` or `copilot/**` | **Build** → compile, restore, test |
| Merge to `main` | **Build** + **Package** → MSIX + portable ZIP artifacts |
| Push a version tag `v*.*.*` | **Build** + **Package** + **Release** → MSIX + ZIP + SHA256 checksums attached to GitHub Release |

See [`.github/workflows/build.yml`](.github/workflows/build.yml).

**To cut a release:**
```bash
git tag v1.0.0
git push origin v1.0.0
```

---

## Signing

The package workflow signs artifacts when certificate secrets are available.

Required repository secrets:
- `PACKAGE_CERT_PFX_BASE64` — base64-encoded `.pfx` certificate
- `PACKAGE_CERT_PASSWORD` — certificate password
- `PACKAGE_CERT_SUBJECT` *(optional but recommended)* — certificate subject string used for manifest validation (example: `CN=Your Company, O=Your Org, C=US`)

Where to set them:
- GitHub repository `Settings` → `Secrets and variables` → `Actions` → `New repository secret`
- Add each secret by the exact names above.

Behavior:
- If both secrets are present, package artifacts are signed and named with `-signed`.
- If either secret is missing, the workflow falls back to unsigned packaging and names artifacts with `-unsigned`.
- If `PACKAGE_CERT_SUBJECT` is set, CI validates it against `SharkeyWinUI/Package.appxmanifest` publisher before publish.

No cert yet:
- You can keep shipping now. CI will still build and publish `-unsigned` artifacts.
- For real end-user trust (SmartScreen reputation + cleaner install experience), obtain an OV/EV code-signing certificate from a trusted CA.
- For testing the signed pipeline only, use a local self-signed cert (not for public distribution).

Manifest identity requirement:
- `SharkeyWinUI/Package.appxmanifest` `<Identity Publisher="..." />` must match your signing certificate subject exactly.
- Example:

```xml
<Identity Name="SharkeyWinUI" Publisher="CN=Your Company, O=Your Org, C=US" Version="1.0.0.0" />
```

Example (PowerShell) to create the base64 value for `PACKAGE_CERT_PFX_BASE64`:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\path\to\certificate.pfx"))
```

Generate a test cert and print ready-to-paste secret values:

```powershell
./scripts/prepare-dev-signing-secrets.ps1 -CertName "SharkeyWinUI Dev" -Password "Use-A-Unique-Password"
```

Release assets include:
- `.msix` package (primary installer)
- Portable `.zip` package
- `SHA256SUMS.txt` checksums

---

## Authentication

Two sign-in methods are supported:

1. **MiAuth** *(recommended)* — opens your instance in a browser; you approve the app there. No password is ever entered in this app.
2. **API token** — paste a token from your instance's *Settings → API*.

Credentials are stored in two places:
- **API token** — encrypted in the Windows Credential Manager (PasswordVault / DPAPI, scoped to your Windows user account)
- **Server URL, user ID, username** — stored in Windows local app settings (`ApplicationData.Current.LocalSettings`)

---

## License

This project is [MIT licensed](LICENSE).  
Misskey is [AGPL-3.0](https://github.com/misskey-dev/misskey/blob/develop/LICENSE).

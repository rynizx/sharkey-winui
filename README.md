# Sharkey WinUI

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
| Merge to `main` | **Build** + **Package** → MSIX artifact |
| Push a version tag `v*.*.*` | **Build** + **Package** + **Release** → MSIX attached to GitHub Release |

See [`.github/workflows/build.yml`](.github/workflows/build.yml).

**To cut a release:**
```bash
git tag v1.0.0
git push origin v1.0.0
```

---

## Signing

The CI workflow generates a self-signed code-signing certificate and signs the MSIX package so it can be installed via side-loading.  
The exported `.cer` file is included in the MSIX artifact bundle; install/trust that certificate on the target machine before installing the `.msix`.

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

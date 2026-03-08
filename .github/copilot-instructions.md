# Sharkey WinUI — Copilot Instructions

## Project Overview

Sharkey WinUI is a native Windows desktop client for [Misskey](https://misskey-hub.net/) and [Sharkey](https://activitypub.software/ahrienby/Sharkey) social platforms, built with **WinUI 3** and **.NET 8**.

## Tech Stack

- **UI Framework**: WinUI 3 via Windows App SDK 1.6
- **Target Framework**: `net8.0-windows10.0.19041.0`, minimum `10.0.17763.0`
- **Language**: C# with nullable reference types enabled and implicit usings enabled
- **MVVM Toolkit**: CommunityToolkit.Mvvm 8.3.2
- **HTTP**: `System.Net.Http.HttpClient` with `System.Text.Json`
- **Real-time**: WebSocket streaming via `MisskeyStreamingService`
- **Auth**: MiAuth flow (Misskey's browser-based OAuth-like auth)

## Project Structure

```
SharkeyWinUI/
  App.xaml / App.xaml.cs       — App entry point; hosts singleton services
  MainWindow.xaml / .cs        — Shell with NavigationView
  Assets/                      — Icons and image assets
  Controls/                    — Reusable UserControls (e.g. NoteCard)
  Helpers/                     — Utility classes (e.g. EmojiTextHelper)
  Models/                      — Plain data models (Note, User, Notification, etc.)
  Pages/                       — NavigationView pages (Timeline, Profile, Compose, etc.)
  Services/                    — API client, auth, streaming, settings, theme, Windows Hello
```

## Conventions

### Services
- Singleton services are exposed as static properties on `App`: `App.ApiClient`, `App.AuthService`, `App.Streaming`.
- Services reside in `SharkeyWinUI.Services` namespace.

### Pages & Code-behind
- Pages use **code-behind** (not a separate ViewModel class) with `ObservableCollection<T>` for list data.
- Async data loading methods are named `LoadAsync(...)` and fired with `_ = LoadAsync(...)` from event handlers to avoid `async void` where possible.
- Use `CancellationTokenSource _cts` per page to cancel in-flight requests on navigation away.
- On `OnNavigatedFrom`, cancel and dispose `_cts` then unsubscribe from streaming.

### Controls
- Custom controls are `sealed partial` `UserControl` subclasses.
- Expose data via `DependencyProperty` (use `Register` pattern with a static `OnXxxChanged` callback).
- Raise events via `event Action<T>?` delegates (not commands).

### Models
- Models are simple C# classes/records with nullable properties and `[JsonPropertyName]` attributes where the JSON key differs from the C# name.
- Keep models free of UI logic.

### API Client (`MisskeyApiClient`)
- All authenticated requests POST JSON bodies with an `"i"` field set to the bearer token.
- Use `PostAsync<TResponse>(endpoint, body)` helpers; endpoints are relative paths like `"notes/timeline"`.
- JSON serialization uses `PropertyNameCaseInsensitive = true` and `DefaultIgnoreCondition = WhenWritingNull`.

### Streaming (`MisskeyStreamingService`)
- Connects to the Misskey WebSocket streaming API.
- Pages subscribe to typed channel events; always unsubscribe when navigating away.

### Error Handling
- Wrap API calls in `try/catch` and surface errors via WinUI `ContentDialog` or inline `TextBlock`.
- `Debug.WriteLine` for non-fatal diagnostic output.
- The global `App.OnUnhandledException` marks exceptions as handled to prevent crashes from `async void` handlers.

### Theming
- Call `ThemeService.ApplySavedTheme()` and `ThemeService.ApplySavedAccent()` at startup; do not apply theme before the window content exists.

### Styling & XAML
- Follow Fluent Design principles; prefer WinUI 3 built-in controls and styles.
- Use `x:Bind` (compiled binding) over `Binding` where possible.
- Resource keys and styles should use PascalCase.

## Key Patterns to Follow

1. **Pagination**: Track `_untilId` (oldest loaded note id) and pass as `untilId` parameter to load older pages.
2. **Renote display**: When a note has a `Renote` child, show the renote author header and render the inner `Renote` as the card body.
3. **Reaction handling**: Reactions are a `Dictionary<string, int>` on `Note`; custom emoji use the `name@server` format.
4. **Settings persistence**: Use `LocalSettingsService` (backed by `ApplicationData.Current.LocalSettings`) for all user preferences.
5. **Windows Hello**: `WindowsHelloService` wraps `KeyCredentialManager`; the lock page is shown before navigation if enabled.

## Out of Scope

- Do not add third-party HTTP or JSON libraries; use the built-in `HttpClient` and `System.Text.Json`.
- Do not introduce a separate ViewModel layer unless explicitly requested — pages use code-behind.
- Do not target non-Windows platforms.

## AI Agent Guidance

- AI agents should use official Microsoft Learn documentation as the primary reference for WinUI best practices, implementation quality, accessibility, and performance guidance.

### Documentation Workflow

1. Use Microsoft Learn search first to quickly find the most relevant and current guidance.
2. Use Microsoft Learn code sample search when implementing WinUI features to align code with official patterns.
3. Fetch full Microsoft Learn pages for final implementation details, prerequisites, troubleshooting, and accessibility/performance recommendations.
4. Prefer Microsoft Learn over third-party tutorials when guidance conflicts.

### Definition of Done

- For major WinUI UI changes, completion requires a Microsoft Learn doc-backed check for best practices, quality, accessibility, and performance.

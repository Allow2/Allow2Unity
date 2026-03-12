# Allow2 Unity SDK -- Design Document

**Version:** 1.0
**Date:** 11 March 2026
**Status:** Initial Design

---

## 1. Executive Summary

The Allow2 Unity SDK is a Device SDK library that Unity game developers drop into their projects to provide Allow2 Parental Freedom enforcement. It follows the same Device Operational Lifecycle as all Allow2 integrations: pairing, child identification, continuous permission checks, warnings, block screens, requests, and feedback.

**This is NOT an OS-level control** (like allow2linux/allow2mac/allow2windows). It runs inside a Unity game/app, controlling that specific title's gameplay time.

---

## 2. Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                     Unity Game/App                        │
│                                                          │
│  ┌────────────────────────────────────────────────────┐  │
│  │               Allow2 Unity SDK                      │  │
│  │                                                     │  │
│  │  ┌──────────────┐  ┌───────────────────────────┐   │  │
│  │  │ Allow2Manager│  │  Pure C# Core             │   │  │
│  │  │ (MonoBehaviour│  │                           │   │  │
│  │  │  Singleton)  │  │  • Allow2Daemon           │   │  │
│  │  │              │  │  • Allow2Api (UnityWebReq) │   │  │
│  │  │  DontDestroy │  │  • Allow2Checker          │   │  │
│  │  │  OnLoad      │  │  • Allow2ChildShield      │   │  │
│  │  │              │  │  • Allow2Warnings          │   │  │
│  │  │  Coroutine   │  │  • Allow2Offline          │   │  │
│  │  │  bridge for  │  │  • Allow2Pairing          │   │  │
│  │  │  async ops   │  │  • Allow2Request          │   │  │
│  │  │              │  │  • Allow2Feedback          │   │  │
│  │  └──────┬───────┘  │  • Allow2Updates          │   │  │
│  │         │          │  • Allow2Credentials       │   │  │
│  │         ▼          └───────────────────────────┘   │  │
│  │  ┌──────────────────────────────────────────────┐  │  │
│  │  │               UI Prefabs                      │  │  │
│  │  │  • Lock Screen Canvas                         │  │  │
│  │  │  • Child Selector Canvas                      │  │  │
│  │  │  • Warning Banner (top bar)                   │  │  │
│  │  │  • Request More Time Dialog                   │  │  │
│  │  │  • Pairing Screen (QR + PIN)                  │  │  │
│  │  │  • Feedback Dialog                            │  │  │
│  │  └──────────────────────────────────────────────┘  │  │
│  └────────────────────────────────────────────────────┘  │
│                          │                                │
│                          │ HTTPS (UnityWebRequest)        │
│                          ▼                                │
│                 ┌─────────────────┐                       │
│                 │ Allow2 Platform  │                       │
│                 │ api.allow2.com   │                       │
│                 └─────────────────┘                       │
└─────────────────────────────────────────────────────────┘
```

---

## 3. Distribution

### Unity Package Manager (UPM)

Primary distribution via UPM git URL:

```json
// manifest.json (Packages/)
{
  "dependencies": {
    "com.allow2.sdk": "https://github.com/Allow2/allow2unity.git#2.0.0-alpha.1"
  }
}
```

### Package Layout (UPM-compliant)

```
com.allow2.sdk/
├── package.json                    # UPM manifest
├── Runtime/
│   ├── Allow2.Runtime.asmdef       # Assembly definition
│   ├── Core/
│   │   ├── Allow2Daemon.cs         # State machine orchestrator
│   │   ├── Allow2Api.cs            # UnityWebRequest-based API client
│   │   ├── Allow2Checker.cs        # Check loop with per-activity enforcement
│   │   ├── Allow2ChildShield.cs    # PIN hashing (SHA-256+salt), rate limiting
│   │   ├── Allow2Warnings.cs       # Progressive warning scheduler
│   │   ├── Allow2Offline.cs        # Cache + grace period + deny-by-default
│   │   ├── Allow2Pairing.cs        # PIN + QR code pairing flow
│   │   ├── Allow2Request.cs        # Request More Time/Day Type/Ban Lift
│   │   ├── Allow2Updates.cs        # getUpdates polling
│   │   ├── Allow2Feedback.cs       # Bug reports + feature requests
│   │   └── Allow2VoiceCode.cs      # Offline HMAC-SHA256 challenge-response
│   ├── Models/
│   │   ├── Allow2Config.cs         # Configuration (VID, token, activities)
│   │   ├── Allow2State.cs          # State enum (Unpaired, Pairing, Paired, Enforcing, Parent)
│   │   ├── Allow2Child.cs          # Child record
│   │   ├── Allow2CheckResult.cs    # Per-activity check result
│   │   ├── Allow2Activity.cs       # Activity definition
│   │   ├── Allow2Warning.cs        # Warning level + remaining time
│   │   └── Allow2RequestResult.cs  # Request status
│   ├── Credentials/
│   │   ├── ICredentialStore.cs     # Interface
│   │   ├── PlayerPrefsStore.cs     # Default (all platforms)
│   │   ├── KeychainStore.cs        # macOS/iOS (native plugin)
│   │   ├── DPAPIStore.cs           # Windows (native plugin)
│   │   └── AndroidKeystoreStore.cs # Android (native plugin)
│   ├── Bridge/
│   │   ├── Allow2Manager.cs        # MonoBehaviour singleton, DontDestroyOnLoad
│   │   ├── Allow2Coroutines.cs     # Coroutine wrappers for async operations
│   │   └── Allow2SceneLoader.cs    # Optional: pause game, load lock scene
│   └── Events/
│       ├── Allow2Events.cs         # C# events for all lifecycle transitions
│       └── Allow2UnityEvents.cs    # UnityEvent wrappers for Inspector binding
├── UI/
│   ├── Allow2.UI.asmdef            # Separate assembly for UI
│   ├── Prefabs/
│   │   ├── Allow2LockScreen.prefab
│   │   ├── Allow2ChildSelector.prefab
│   │   ├── Allow2WarningBanner.prefab
│   │   ├── Allow2RequestDialog.prefab
│   │   ├── Allow2PairingScreen.prefab
│   │   └── Allow2FeedbackDialog.prefab
│   ├── Scripts/
│   │   ├── Allow2LockScreenUI.cs
│   │   ├── Allow2ChildSelectorUI.cs
│   │   ├── Allow2WarningBannerUI.cs
│   │   ├── Allow2RequestDialogUI.cs
│   │   ├── Allow2PairingScreenUI.cs
│   │   └── Allow2FeedbackDialogUI.cs
│   └── Resources/
│       ├── Allow2Theme.asset       # Scriptable Object for theming
│       └── Sprites/
├── Editor/
│   ├── Allow2.Editor.asmdef
│   ├── Allow2ManagerEditor.cs      # Custom inspector
│   └── Allow2SetupWizard.cs        # Window: configure VID, activities
├── Samples~/
│   ├── BasicIntegration/           # Minimal example
│   └── FullIntegration/            # All features demo
├── Documentation~/
│   ├── index.md
│   └── quick-start.md
├── Tests/
│   ├── Runtime/
│   │   └── Allow2.Tests.asmdef
│   └── Editor/
│       └── Allow2.EditorTests.asmdef
├── CHANGELOG.md
├── LICENSE
└── README.md
```

---

## 4. Core Architecture Decisions

### 4.1 Pure C# Core + MonoBehaviour Bridge

The SDK core (`Core/` directory) is pure C# with no Unity dependencies. This enables:
- Unit testing without Unity Test Runner
- Potential reuse in non-Unity .NET projects
- Clean separation of concerns

The `Bridge/` layer provides Unity-specific integration:
- `Allow2Manager` (MonoBehaviour, singleton, DontDestroyOnLoad) — main entry point
- Coroutine wrappers for WebGL compatibility
- Unity lifecycle integration (OnApplicationPause, OnApplicationFocus, OnApplicationQuit)

### 4.2 Async/Await with Coroutine Fallback

```csharp
// Primary: async/await (Unity 2023.1+ / .NET Standard 2.1)
var result = await Allow2Manager.Instance.CheckAsync(activities);

// Fallback: coroutine (WebGL, older Unity)
Allow2Manager.Instance.Check(activities, result => {
    if (!result.Allowed) ShowLockScreen();
});
```

WebGL cannot use `System.Threading.Tasks` (single-threaded). The SDK detects the platform and uses `UnityWebRequest` + coroutines automatically.

### 4.3 HTTP Client: UnityWebRequest

All HTTP communication uses `UnityWebRequest` (not `HttpClient`), because:
- Works on ALL Unity platforms (including WebGL, consoles)
- Handles platform-specific TLS/certificate stores
- Integrates with Unity's coroutine system
- Respects Unity's threading model

### 4.4 Credential Storage

| Platform | Backend | Notes |
|----------|---------|-------|
| **All (default)** | `PlayerPrefs` | Encrypted with device-specific key |
| **macOS/iOS** | Keychain Services | Via native plugin |
| **Windows** | DPAPI | Via native plugin |
| **Android** | Android Keystore | Via native plugin |
| **WebGL** | `localStorage` | Browser sandbox |
| **Consoles** | Platform save system | Sony/MS/Nintendo APIs |

The `ICredentialStore` interface allows developers to provide custom implementations.

---

## 5. Integration Guide (Developer Perspective)

### 5.1 Minimal Integration (5 Minutes)

```csharp
using Allow2.Runtime;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        // Configure (VID + token from developer.allow2.com)
        Allow2Manager.Instance.Configure(new Allow2Config {
            Vid = 456,
            DeviceToken = SystemInfo.deviceUniqueIdentifier,
            DeviceName = SystemInfo.deviceName,
            Activities = new[] {
                new Allow2Activity(3, "Gaming"),     // Gaming
                new Allow2Activity(8, "Screen Time") // Screen Time
            }
        });

        // Subscribe to events
        Allow2Manager.Instance.OnSoftLock += reason => {
            // Show lock screen (SDK has a prefab, or use your own)
            Allow2Manager.Instance.ShowLockScreen();
        };

        Allow2Manager.Instance.OnWarning += warning => {
            Allow2Manager.Instance.ShowWarningBanner(warning);
        };

        // Start the daemon (loads credentials, starts check loop if paired)
        Allow2Manager.Instance.StartDaemon();
    }
}
```

### 5.2 Full Integration

```csharp
// Subscribe to all events
Allow2Manager.Instance.OnPairingRequired += info => {
    // Show pairing screen (QR + PIN)
    Allow2Manager.Instance.ShowPairingScreen();
};

Allow2Manager.Instance.OnChildSelectRequired += children => {
    Allow2Manager.Instance.ShowChildSelector();
};

Allow2Manager.Instance.OnWarning += warning => {
    Allow2Manager.Instance.ShowWarningBanner(warning);
};

Allow2Manager.Instance.OnSoftLock += reason => {
    Time.timeScale = 0;  // Pause game
    Allow2Manager.Instance.ShowLockScreen();
};

Allow2Manager.Instance.OnUnlock += () => {
    Time.timeScale = 1;  // Resume game
};

Allow2Manager.Instance.OnCheckResult += result => {
    // Update HUD with remaining time
    hudTimeRemaining.text = FormatTime(result.GetActivity(3).Remaining);
};
```

### 5.3 Inspector Configuration

The `Allow2Manager` component exposes fields in the Unity Inspector:
- VID (int)
- Activities (list)
- Auto-show pairing UI (bool)
- Auto-show child selector (bool)
- Auto-show warnings (bool)
- Auto-pause on lock (bool)
- Custom lock scene (SceneReference)
- Theme (Allow2Theme ScriptableObject)

---

## 6. Module List (Gold Standard Compliance)

| Module | File | Status |
|--------|------|--------|
| **Daemon/Core** | `Allow2Daemon.cs` | NEW — state machine (Unpaired→Pairing→Paired→Enforcing→Parent) |
| **API Client** | `Allow2Api.cs` | NEW — UnityWebRequest-based, handles both api.allow2.com and service.allow2.com |
| **Pairing** | `Allow2Pairing.cs` | NEW — PIN + QR code flows |
| **Child Shield** | `Allow2ChildShield.cs` | NEW — SHA-256+salt PIN hashing, rate limiting, lockout |
| **Checker** | `Allow2Checker.cs` | NEW — 30-60s check loop, per-activity enforcement, activity stacking |
| **Warnings** | `Allow2Warnings.cs` | NEW — progressive: 15min→5min→1min→30sec→10sec→BLOCKED |
| **Offline** | `Allow2Offline.cs` | NEW — response cache, grace period (5min default), deny-by-default |
| **Requests** | `Allow2Request.cs` | NEW — more time, day type change, ban lift with polling |
| **Voice Codes** | `Allow2VoiceCode.cs` | NEW — HMAC-SHA256 offline challenge-response |
| **Updates** | `Allow2Updates.cs` | NEW — getUpdates polling for children/quotas/bans |
| **Feedback** | `Allow2Feedback.cs` | NEW — submit/load/reply to feedback discussions |
| **Credentials** | `ICredentialStore.cs` + backends | NEW — pluggable, per-platform |

---

## 7. UI Prefabs

| Prefab | Canvas Type | Behaviour |
|--------|-------------|-----------|
| **Allow2PairingScreen** | Screen-space overlay | Full screen, QR code + 6-digit PIN, "Scan with Allow2 app" |
| **Allow2ChildSelector** | Screen-space overlay | List of children + "Parent" option, search/filter if 3+ children |
| **Allow2PinEntry** | Screen-space overlay | 4-digit PIN pad with rate limiting feedback |
| **Allow2LockScreen** | Screen-space overlay | "Time's up!" + Request More Time + Switch Child + Voice Code |
| **Allow2WarningBanner** | Screen-space overlay | Top bar, semi-transparent, remaining time + activity name |
| **Allow2RequestDialog** | Screen-space overlay | Duration picker + message field + status polling |
| **Allow2FeedbackDialog** | Screen-space overlay | Category picker + message + submission |

All prefabs:
- Use Unity UI (Canvas + RectTransform)
- Respect `Allow2Theme` ScriptableObject for colours/fonts
- Can be replaced entirely with developer's own UI (just handle the events)
- Render on a high-sort-order canvas (above game UI)

---

## 8. Platform Matrix

| Platform | HTTP | Credentials | Overlay | Notes |
|----------|------|-------------|---------|-------|
| **Windows** | UnityWebRequest | PlayerPrefs / DPAPI | Canvas overlay | Full support |
| **macOS** | UnityWebRequest | PlayerPrefs / Keychain | Canvas overlay | Full support |
| **Linux** | UnityWebRequest | PlayerPrefs | Canvas overlay | Full support |
| **Android** | UnityWebRequest | Android Keystore | Canvas overlay | Full support |
| **iOS** | UnityWebRequest | Keychain | Canvas overlay | Full support |
| **WebGL** | UnityWebRequest | localStorage | Canvas overlay | No threading; coroutine-only |
| **Xbox** | UnityWebRequest | Platform save | Canvas overlay | Console cert required |
| **PlayStation** | UnityWebRequest | Platform save | Canvas overlay | Console cert required |
| **Switch** | UnityWebRequest | Platform save | Canvas overlay | Console cert required |

---

## 9. Implementation Phases

| Phase | Scope | Effort |
|-------|-------|--------|
| **Phase 1** | Core SDK: Daemon, Api, Checker, Credentials, Config | 2-3 weeks |
| **Phase 2** | Pairing, ChildShield, child selector flow | 1-2 weeks |
| **Phase 3** | Warnings, Offline handler, Request flow | 1-2 weeks |
| **Phase 4** | UI Prefabs (all 7 screens) | 2-3 weeks |
| **Phase 5** | Allow2Manager bridge, Inspector integration, Editor wizard | 1 week |
| **Phase 6** | Platform credential backends (Keychain, DPAPI, Android Keystore) | 1-2 weeks |
| **Phase 7** | Voice codes, Feedback, Updates polling | 1 week |
| **Phase 8** | Samples, documentation, WebGL testing | 1 week |
| **Total** | | **10-15 weeks** |

---

## 10. Differences from Existing v1 Unity SDK

The existing code at `examples/unity/Allow2/Allow2.cs` is a v1 implementation:
- Single monolithic file
- Direct API calls (no daemon, no state machine)
- No pairing flow (assumes pre-paired)
- No warnings, no offline, no requests
- No UI prefabs

The v2 rewrite is a complete replacement — no backward compatibility with v1.

---

## 11. Open Questions

| # | Question | Notes |
|---|----------|-------|
| 1 | Should the SDK include a "parent mode" for testing? | Developer sets a flag to bypass checks during development |
| 2 | Console (Xbox/PS/Switch) credential storage APIs | Need platform-specific native plugins; may defer to Phase 2 |
| 3 | WebGL: can we open a popup for QR code pairing? | Browser popup blockers may interfere; may need in-game display only |
| 4 | Minimum Unity version? | 2021.3 LTS (for .NET Standard 2.1, async/await) |
| 5 | UPM scoped registry vs git URL? | Git URL is simpler; scoped registry needs hosting |

---

## Document History

- **Created**: 2026-03-11
- **Related**: Allow2 C# SDK (sdk/csharp), allow2linux, Node.js SDK v2 (gold standard)

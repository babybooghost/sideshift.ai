module SideShift.Types

type WidgetMode =
    | Ask
    | ELI5
    | Verify
    | Diff

type ChatMsg = { Role: string; Text: string }

/// A highlighted screen region + what we know about it.
type Capture =
    { ImageDataUrl: string
      // Origin rect in overlay CSS px, kept so the minimap node can hint where it came from.
      X: float
      Y: float
      W: float
      H: float }

type Widget =
    { Id: int
      Mode: WidgetMode
      Title: string
      Capture: Capture
      Messages: ChatMsg list
      Input: string
      Streaming: bool
      StreamBuf: string
      Error: string option
      // which backend actually served this widget (for the header badge)
      Via: string
      PosX: float
      PosY: float
      Width: float
      Height: float
      Z: int
      Minimized: bool
      Color: string }

type ClosePolicy =
    | Discard
    | Merge

type Model =
    { // provider settings
      AnthropicKey: string option
      OpenRouterKey: string option
      DefaultModel: string       // Anthropic model for Ask/ELI5/Diff (and Verify fallback)
      CriticModel: string        // OpenRouter model used for Verify's independent critic
      ShowSettings: bool
      AnthropicDraft: string
      OpenRouterDraft: string
      CriticDraft: string
      // workspace
      Widgets: Widget list
      NextId: int
      TopZ: int
      CaptureMode: bool
      Screenshot: (string * float * float * float) option // dataUrl, w, h, scale
      Pending: (Capture * float * float) option            // capture + action-bar x,y
      PendingCode: bool                                    // region auto-detected as code
      Drag: (int * float * float) option                   // widgetId, offsetX, offsetY
      Resize: int option                                   // widgetId being resized
      Closing: int option                                  // widget showing merge/discard menu
      SharedContext: string list }                         // merged side-quest summaries

type Msg =
    | KeyLoaded of string * string option // name, value
    | StateLoaded of obj                  // persisted workspace (or null)
    | OpenSettings
    | CloseSettings
    | AnthropicDraftChanged of string
    | OpenRouterDraftChanged of string
    | CriticDraftChanged of string
    | SaveSettings
    | SettingsSaved
    // capture flow
    | ToggleCapture
    | ScreenshotReady of string * float * float * float
    | CaptureCancelled
    | RegionDrawn of float * float * float * float // overlay-css rect: x, y, w, h
    | RegionReady of Capture
    | PendingClassified of bool
    | QuickAction of WidgetMode
    | DismissPending
    // widget lifecycle
    | Focus of int
    | StartDrag of int * float * float
    | StartResize of int
    | PointerMove of float * float
    | PointerUp
    | Minimize of int
    | Restore of int
    | RequestClose of int
    | CloseWith of int * ClosePolicy
    | Merged of string
    // chat
    | InputChanged of int * string
    | Send of int
    | StreamDelta of int * string
    | StreamDone of int
    | StreamError of int * string
    | Noop

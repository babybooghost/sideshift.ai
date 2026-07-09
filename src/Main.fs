module SideShift.Main

open Elmish
open Elmish.React
open Browser
open SideShift.Types
open SideShift.Update
open SideShift.View

// Two renderer modes share this program: the chrome-less overlay, and the
// dedicated native Settings window (?settings=1). Each wires only what it needs.
let private subscribe (_: Model) : Sub<Msg> =
    [ [ "wiring" ],
      fun dispatch ->
          if Interop.isSettingsWindow then
              Interop.setTitle "SideShift AI Settings"
              Interop.onSettingsError (fun m -> dispatch (SettingsError m))
              document.addEventListener (
                  "keydown",
                  fun e ->
                      let ke = e :?> Browser.Types.KeyboardEvent
                      if ke.key = "Escape" then dispatch CloseSettings)
          else
              Interop.installHitTest ()
              Interop.onToggleCapture (fun () -> dispatch ToggleCapture)
              Interop.onNudge (fun dx dy -> dispatch (NudgeFocused(dx, dy)))
              Interop.onOpenSettings (fun () -> dispatch OpenSettings)
              // live sync from the settings window
              Interop.onPrefsChanged (fun p -> dispatch (PrefsChanged p))
              Interop.onKeysChanged (fun () -> dispatch KeysReload)
              document.addEventListener (
                  "keydown",
                  fun e ->
                      let ke = e :?> Browser.Types.KeyboardEvent
                      if ke.key = "Escape" then
                          // Esc backs out of any transient overlay state
                          dispatch CaptureCancelled
                          dispatch DismissPending
                          dispatch CancelClose)
          { new System.IDisposable with
              member _.Dispose() = () } ]

Program.mkProgram init update view
|> Program.withSubscription subscribe
|> Program.withReactSynchronous "root"
|> Program.run

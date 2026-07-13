module SideShift.Main

open Elmish
open Elmish.React
open Browser
open SideShift.Types
open SideShift.Update
open SideShift.View

// Two renderer modes share this program: the chrome-less overlay, and the
// dedicated native Settings window (?settings=1). Each wires only what it needs.
// Wire one bridge listener defensively: a missing/throwing bridge method must not
// abort the rest of the subscription (that once left prefs/keys sync dead).
let private wire (f: unit -> unit) = try f () with _ -> ()

let private subscribe (_: Model) : Sub<Msg> =
    [ [ "wiring" ],
      fun dispatch ->
          if Interop.isSettingsWindow then
              wire (fun () -> Interop.setTitle "SideShift AI Settings")
              wire (fun () -> Interop.onSettingsError (fun m -> dispatch (SettingsError m)))
              document.addEventListener (
                  "keydown",
                  fun e ->
                      let ke = e :?> Browser.Types.KeyboardEvent
                      if ke.key = "Escape" then dispatch CloseSettings)
          else
              wire Interop.installHitTest
              wire (fun () -> Interop.onToggleCapture (fun () -> dispatch ToggleCapture))
              wire (fun () -> Interop.onSelection (fun t -> dispatch (SelectionCaptured t)))
              wire (fun () -> Interop.onToast (fun m -> dispatch (ShowToast m)))
              wire (fun () -> Interop.onNudge (fun dx dy -> dispatch (NudgeFocused(dx, dy))))
              wire (fun () -> Interop.onOpenSettings (fun () -> dispatch OpenSettings))
              // live sync from the settings window
              wire (fun () -> Interop.onPrefsChanged (fun p -> dispatch (PrefsChanged p)))
              wire (fun () -> Interop.onKeysChanged (fun () -> dispatch KeysReload))
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

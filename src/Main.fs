module SideShift.Main

open Elmish
open Elmish.React
open Browser
open SideShift.Types
open SideShift.Update
open SideShift.View

// Global-hotkey + Esc-to-cancel wired as an Elmish subscription.
let private subscribe (_: Model) : Sub<Msg> =
    [ [ "hotkey" ],
      fun dispatch ->
          Interop.installHitTest ()
          Interop.onToggleCapture (fun () -> dispatch ToggleCapture)
          Interop.onNudge (fun dx dy -> dispatch (NudgeFocused(dx, dy)))
          Interop.onOpenSettings (fun () -> dispatch OpenSettings)
          document.addEventListener (
              "keydown",
              fun e ->
                  let ke = e :?> Browser.Types.KeyboardEvent
                  if ke.key = "Escape" then dispatch CaptureCancelled)
          { new System.IDisposable with
              member _.Dispose() = () } ]

Program.mkProgram init update view
|> Program.withSubscription subscribe
|> Program.withReactSynchronous "root"
|> Program.run

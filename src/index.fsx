(**
 - title: Fable behaviors sample
 - tagline: fun with behaviors
 - app-style: width:1024px; margin:20px auto 50px auto;
 - require-paths: `'PIXI':'https://cdnjs.cloudflare.com/ajax/libs/pixi.js/3.0.11/pixi.min'`
 - intro: Just a try to implement the Fable architecture over Pixi. This is an experimentation for the moment
*)

#r "../node_modules/fable-core/Fable.Core.dll"

open Fable.Core
open Fable.Core.JsInterop
open Fable.Import

// Types -------------------------------------------
type WebSocketPort =
  abstract ``open``: unit->unit
  abstract send: obj -> unit 

[<Global>]
let osc : obj = jsNative
[<Emit("new osc.WebSocketPort($0)")>]
let wsp(conn:obj): WebSocketPort = jsNative

let oscPort : WebSocketPort = wsp(createObj
  ["url" ==> "ws://localhost:8081"]
)
  
let openp() = 
  // connect to teh web socket
  oscPort.``open``()

  let send() = 
    let note = JS.Math.random() * 20. + 40.
    // send the note to our sonic-pi music
    // using kyb variable
    oscPort.send (
      createObj ["address" ==> "/kyb";"args" ==> int note]
    )
    printfn "sending data"
  //send()
  Browser.window.setInterval( send, 500. ) |> ignore

// wait before opening the connection to the web socket
Browser.window.setTimeout( openp, 3000. )
printfn "starting"

(**
 - title: Fable behaviors sample
 - tagline: fun with behaviors
 - app-style: width:1024px; margin:20px auto 50px auto;
 - require-paths: `'PIXI':'https://cdnjs.cloudflare.com/ajax/libs/pixi.js/3.0.11/pixi.min'`
 - intro: Just a try to implement the Fable architecture over Pixi. This is an experimentation for the moment
*)

#r "../node_modules/fable-core/Fable.Core.dll"
#r "../node_modules/fable-powerpack/Fable.PowerPack.dll"
#load "../node_modules/fable-import-pixi/Fable.Import.Pixi.fs"
#load "Easing.fsx"

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Fable.Import.PIXI
open Fable.Import.PIXI.extras
open Fable.Import
open Fable.PowerPack

// Types -------------------------------------------


// For performance, we use delegates instead of curried F# functions
type Behavior = Func<ESprite, float, JS.Promise<bool>>

and ESprite(t:Texture, id: string, behaviors: Behavior list) =
    inherit Sprite(t)
    let mutable _behaviors = behaviors
    let mutable _disposed = false
    let mutable _prevTime = 0.

    member __.Id = id
    member __.IsDisposed = _disposed

    member self.Behave(b:Behavior) =
        _behaviors <- b :: _behaviors

    // Use a promise computation expression to iterate
    // through the behaviors as if they were synchronous
    member self.Update(dt: float) = promise {
        let behaviors = _behaviors
        _behaviors <- []
        let mutable notCompletedBehaviors = []
        let dt =
            let tmp = _prevTime
            _prevTime <- dt
            if tmp = 0. then 0. else dt - tmp
        for b in behaviors do
            let! complete = b.Invoke(self, dt)
            if not complete then
                notCompletedBehaviors <- b::notCompletedBehaviors
        // Some behaviors may have been added in the meanwhile with Behave
        _behaviors <- _behaviors @ notCompletedBehaviors
    }

    // System.IDisposable is the usual interface for disposable objects
    // in C#/F#. It lets you use language constructs like `use`.
    interface IDisposable with
        member self.Dispose() =
            if not _disposed then
                _disposed <- true
                self.parent.removeChild(self) |> ignore


//
and State =
  | Nothing 
  | Loading 
  | Play 
  | MainTitle

type Easing = Func<float, float, float, float, float>

// Behaviors -------------------------------------------
module Behaviors =
    let private distanceBetween2Points (p1:Point) (p2:Point) =
        let tx = p2.x - p1.x
        let ty = p2.y - p1.y
        JS.Math.sqrt( tx * tx + ty * ty)
    let fade(easeFunction: Easing, duration) =
        let mutable ms = 0.
        Behavior(fun s dt ->
            if s.alpha > 0.
            then
                ms <- ms + dt
                let result = easeFunction.Invoke(ms, 0., 1., duration)
                s.alpha <- 1. - result
                if s.alpha < 0.
                then s.alpha <- 0.; true
                else false
            else true
            |> Promise.lift)
    let fadeOut(easeFunction: Easing, duration, onCompleted) =
        let mutable ms = 0.
        Behavior(fun s dt ->
            if s.alpha <1.
            then
              ms <- ms + dt
              let result = easeFunction.Invoke(ms, 0., 1., duration)
              s.alpha <- result
              false
            else 
              if s.alpha > 1. then s.alpha <- 1.
              onCompleted s
              true
          |> Promise.lift)
    let curveTo(easeFunction: Easing, duration, radius, p:Point, onCompleted) =
        let mutable ms = -1.
        let mutable distance = 0.
        let mutable a = 0.
        let where = if JS.Math.random() > 0.5 then 1. else -1.
        let curve = JS.Math.random() * 5. / 500. * where
        Behavior(fun s dt ->
          let vx,vy = (p.x - s.position.x, p.y - s.position.y)
          a <- JS.Math.atan2(vy,vx) + curve
          if ms < 0. 
          then
            let vx,vy = (p.x - s.position.x, p.y - s.position.y)
            distance <- JS.Math.sqrt(vx * vx + vy * vy)
            ms <- 0.
            false
          else 
            ms <- ms + dt
            let result = easeFunction.Invoke(ms, 0., 1., duration)
            let d = JS.Math.abs(distance) * ( 1. - result)
            if result < 1. || d > radius then
              let factor = if distance > 0. then -1. else 1.
              s.rotation <- a
              s.position <- Point( p.x + d * JS.Math.cos(a) * factor, p.y + d * JS.Math.sin(a) * factor)
              false
            else 
              onCompleted s
              true
        |> Promise.lift)          
    let breathe(amplitude, speed) = 
      let mutable scale = Point(0.,0.)
      let mutable ms = -1.      
      let mutable a = 0.
      Behavior(fun s _ ->
        if ms < 0. 
        then
          scale <- Point( s.scale.x, s.scale.y )
          ms <- 0.
        else
          a <- a + speed
          s.scale.x <- scale.x + JS.Math.cos(a) * amplitude
          s.scale.y <- scale.y + JS.Math.cos(a) * amplitude
        false 
      |> Promise.lift)
    let alphaDeath(onCompleted) = Behavior(fun s _ ->
        if s.alpha <= 0.
        then (s :> IDisposable).Dispose(); onCompleted s; true
        else false
        |> Promise.lift)
    let killOffScreen(bounds: Rectangle, onCompleted) = Behavior(fun s _ ->
        let sx = s.position.x
        let sy = s.position.y
        let offScreen =
            (sx + s.width) < bounds.x
            || (sy + s.height) < bounds.y
            || (s.y - s.height) >= bounds.height
            || (sx - s.width) > bounds.width
        if offScreen then
            onCompleted s
            (s :> IDisposable).Dispose()
        Promise.lift offScreen)

    let grow(easeFunction: Easing, duration, max, min) =
        let mutable ms = -1.
        let mutable scale = max
        let diff = max - min        
        Behavior(fun s dt ->
          ms <- ms + dt
          let result = easeFunction.Invoke(ms, 0., 1., duration)
          scale <- min + (result*diff)
          if scale >= max 
          then true
          else 
            s.scale <- Point(scale, scale)
            false
        |> Promise.lift)          
    

// Animation ------------------------------------------------
open Behaviors

let updateLoop(renderer:WebGLRenderer) (stage:Container) =
  let centerX = renderer.width * 0.5
  let centerY = renderer.height * 0.5
  let rwidth = renderer.width
  let rheight = renderer.height
  let fps = 60.

  let mutable id = -1
  let mutable state : State = Loading
  let mutable resources  = Unchecked.defaultof<loaders.ResourceDictionary>
  let mutable sprites : ESprite list = []

  // sprites
  //let mutable planet = Unchecked.defaultof<Sprite>

  (*
  // prepare our containers  
  let createParticleContainer max = 
    let options : ParticleContainerProperties list = [
                ParticleContainerProperties.Scale true
                ParticleContainerProperties.Rotation true
                ParticleContainerProperties.Alpha true
              ]
    ParticleContainer(max, options)

  let backgroundContainer = Container()
  let yellowContainer = createParticleContainer 500000.
  let minusContainer = createParticleContainer 50000.
  let planetContainer = Container()
  let greenContainer = createParticleContainer 500000.
  let plusContainer = createParticleContainer 500000.
  // add our containers to the stage
  let bindContainer (c:DisplayObject) = stage.addChild c |> ignore 
  [|
      backgroundContainer
      ;unbox yellowContainer // ParticleContainer is not a Container
      ;unbox minusContainer // ParticleContainer is not a Container
      ;unbox greenContainer // ParticleContainer is not a Container
      ;unbox plusContainer // ParticleContainer is not a Container
      ;planetContainer
  |]
    |> Seq.iter bindContainer
  *)

  // sprite & ESprite helper functions
  let nextId() = 
    id <- id + 1
    sprintf "%i" id    
  let makeSprite t = 
    Sprite t
  let makeESprite (behaviors:Behavior list) (t:Texture)= 
    new ESprite(t, nextId(), behaviors)
  let getTexture name = 
    resources.Item(name).texture
  let addToContainer (c:Container) (s:Sprite)=
    c.addChild s |> ignore
    s
  let setPosition x y (s:Sprite)= 
    s.position <- Point(x, y)
    s
  let setAnchor x y (s:Sprite)= 
    s.anchor <- Point(x, y)
    s
  let setScale sx sy (s:Sprite)= 
    s.scale <- Point(sx, sy)
    s
  let drawRect (g:Graphics) color width height = 
    g.beginFill(float color) |> ignore
    g.drawRect(0.,0.,width, height) |> ignore 
    g.endFill() |> ignore
    g
  let setRotation r (s:Sprite) = 
    s.rotation <- r
    s
  let setAlpha a (s:Sprite) = 
    s.alpha <- a
    s
  let addToESprites (s:ESprite) = 
    sprites <- [s] @ sprites
    s
    
  // our update loop
  let update(currentState) =
    
    match currentState with 
    | Nothing -> State.Nothing

    | Loading -> 

      let onLoadComplete r =

        // hide loading bar
        Browser.window.document.getElementById("loading").style.display <- "none" 
        // save resources for later usage
        resources <- unbox<loaders.ResourceDictionary> r
        state <- MainTitle

      // start Loading
      let errorCallback(e) = Browser.console.log e 
      let onProgress(e) = Browser.console.log e

      let addAssetToLoad rawName =
        // extract the name of an asset
        // to be able to load easily
        let extractName (rawName:string) =
          let rawName = rawName.Substring(rawName.LastIndexOf('/')+1)
          let keys = rawName.Split([|'.'|])
          keys.[0]      
        Globals.loader.add(extractName rawName, "out/" + rawName) |> ignore

      addAssetToLoad "font.fnt"
      ["background"
      ] 
        |> Seq.map( fun(name) -> sprintf "%s.png" name)
        |> Seq.iter(addAssetToLoad)

      Globals.loader?on("error", errorCallback ) |> ignore
      Globals.loader.load() |> ignore
      Globals.loader?on("progress", onProgress ) |> ignore
      Globals.loader?once("complete", fun loader resources -> onLoadComplete resources ) |> ignore
      Nothing

    | MainTitle ->
        
(*      getTexture "background" 
        |> makeSprite
        |> addToContainer backgroundContainer
        |> setAlpha 0.6
        |> ignore
*)
      
      Play

    | Play -> Play

  // actual game loop
  let rec updateLoop render (dt:float) =
    promise {
        let mutable xs = []
        for x in sprites do
            do! x.Update(dt)
            if not x.IsDisposed then xs <- x::xs
        return xs }
    |> Promise.iter(fun sprites ->    
      state <- update(state)
      render()
      Browser.window.requestAnimationFrame(fun dt -> 
        updateLoop render dt) |> ignore )

  updateLoop 

let start divName = 
  let renderer =
        WebGLRenderer(1024., 600.,
            [ Antialias true
              BackgroundColor ( float 0x000000 )])
  Browser.document.getElementById("game").appendChild(renderer.view) |> ignore
  // start actual game loop
  let stage = Container(interactive=true)
  updateLoop renderer stage (fun () -> renderer.render(stage)) 0.

// start our game
start "game" 
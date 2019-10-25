module Update

open Elmish
open Fable.Core
open Fable.Core.JsInterop
open Fable.Import

open Model
open View

let init _ = Game.Fresh(), Cmd.none
let update msg model =
    match msg with
    | ToggleOptions ->
        let showOptions = not model.showOptions
        if not showOptions then // done button was hit, so persist settings
            let encode = Thoth.Json.Encode.Auto.toString(1, model.settings)
            Browser.WebStorage.localStorage.["settings"] <- encode
        { model with showOptions = showOptions }, Cmd.none
    | Reset -> Game.Fresh(), Cmd.none
    | Setting msg ->
        let settings = model.settings
        let settings' =
            match msg with
            | SettingChange.Sound v -> { settings with sound = v }
            | SettingChange.AutoEnter v -> { settings with autoEnter = v }
            | SettingChange.ProgressiveDifficulty v -> { settings with progressiveDifficulty = v }
            | SettingChange.MathBase v -> { settings with mathBase = v }
            | SettingChange.Operation v -> { settings with mathType = v }
            | SettingChange.Maximum v -> { settings with size = v }
        match msg with
        | Operation _ | Maximum _ | MathBase _ ->
            { model with settings = settings'; cells = Model.ComputeHints settings'; reviewList = [] } |> Game.nextProblem, Cmd.none
        | _ ->
            { model with settings = settings'; }, Cmd.none
    | _ -> model, Cmd.none

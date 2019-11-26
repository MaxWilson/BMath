module Model
open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
open Browser.WebStorage
open Common

module Seq =
    let every pred = not << Seq.exists (not << pred)

type SoundState = On | Off | CheerOnly | BombOnly
type Settings = {
    sound: SoundState
    feedbackDuration: int
    } with
    static member Default = {
        sound = On
        feedbackDuration = 1000
    }

let inline persist key value =
    localStorage.[key:string] <- Thoth.Json.Encode.Auto.toString(1, value)

let inline retrievePersisted key defaultValue =
    match localStorage.[key:string] with
    | null -> defaultValue
    | rawValue ->
        match Thoth.Json.Decode.Auto.fromString(unbox<string> rawValue) with
        | Ok v -> v
        | Error _ -> defaultValue

type Term = | N of int | Plus of Term * Term | Times of Term * Term | Divide of Term * Term | Minus of Term * Term | Square of Term | Variable
type Equation = Equation of lhs:Term * rhs: Term * variableValue: int option

let rand = System.Random()
let r x = 1 + rand.Next x
let isSmall x =
    let x = abs x
    0 <= x && x <= 12 || x <= 100 && x % 10 = 0
let generateSmallNumber() =
    match r 10 with
    | x when x <= 2 -> 0
    | x when x <= 4 -> r 5
    | x when x <= 7 -> r 12
    | _ -> r 10 * 10
let generateSmallInteger() =
    if r 2 = 1 then generateSmallNumber() else - (generateSmallNumber())
let eval env =
    let rec eval = function
        | N n -> n
        | Plus(t1, t2) -> eval t1 + eval t2
        | Minus(t1, t2) -> eval t1 - eval t2
        | Times(t1, t2) -> eval t1 * eval t2
        | Divide(t1, t2) -> eval t1 / eval t2
        | Square t -> eval t * eval t
        | Variable -> env
    eval

let generateSmallWhere ctor commutative counterpart fallback =
    let candidate = generateSmallNumber()
    match counterpart candidate with
    | Some term ->
        if commutative = false || r 2 = 1 then
            Some <| ctor(N candidate, term)
        else
            Some <| ctor(term, N candidate)
    | None ->
        None

let rec generatePermute depth constraint1 =
    if r (3 - depth) = 1 then
        match generateSmallNumber() with
        | 0 -> N constraint1
        | n -> Plus(N n, N (constraint1 - n))
    else
        let rec loop counter =
            if counter >= 10 then N constraint1
            else
                let t =
                    match r 4 with
                    | 1 ->
                        generateSmallWhere Plus true (fun candidate -> if constraint1 - candidate |> isSmall then Some (generatePermute (depth + 1) (constraint1 - candidate)) else None) constraint1
                    | 2 ->
                        generateSmallWhere Minus false (fun candidate -> if constraint1 + candidate |> isSmall then Some (generatePermute (depth + 1) (constraint1 + candidate)) else None) constraint1
                    | 3 ->
                        generateSmallWhere Times true (fun candidate -> if candidate <> 0 && candidate <> 1 && constraint1 % candidate = 0 && constraint1 / candidate |> isSmall then Some (generatePermute (depth + 1) (constraint1 / candidate)) else None) constraint1
                    | _ ->
                        let candidate = generateSmallNumber()
                        let diff = (candidate * candidate - constraint1)
                        if isSmall diff then
                            if diff = 0 then
                                Some (Square(generatePermute (depth+1) candidate))
                            else
                                Some(Minus(Square(generatePermute (depth+1) candidate), N diff))
                        else None
                match t with
                | Some t -> t
                | None -> loop (counter + 1)
        loop 0

let generate() =
    let y = r 12
    let coefficient = match generateSmallInteger() with 0 -> 1 | n -> n
    let mutable obfuscated = None // only want one variable in the equation
    let rec obfuscate term =
        if obfuscated.IsSome then term
        else
            match term with
            | N n -> if r 3 = 1 then obfuscated <- Some n; Variable else term
            | Plus(t1, t2) -> Plus(obfuscate t1, obfuscate t2)
            | Minus(t1, t2) -> Minus(obfuscate t1, obfuscate t2)
            | Times(t1, t2) -> Times(obfuscate t1, obfuscate t2)
            | Divide(t1, t2) -> Divide(obfuscate t1, obfuscate t2)
            | Square(t) -> Square(obfuscate t)
            | _ -> term
    let term = generatePermute 1 (y * coefficient) |> obfuscate
    if coefficient <> 1 then
        Equation(Divide(term, N coefficient), N y, obfuscated)
    else Equation(term, N y, obfuscated)
let rec renderTerm parentPrecedence term =
    let addParens myPrecedence rendering =
        if myPrecedence > parentPrecedence then rendering
        else sprintf "(%s)" rendering
    match term with
    | N n -> n.ToString()
    | Plus(t1, t2) -> sprintf "%s + %s" (renderTerm 1 t1) (renderTerm 1 t2) |> addParens 1
    | Minus(t1, t2) -> sprintf "%s - %s" (renderTerm 1 t1) (renderTerm 1 t2) |> addParens 1
    | Times(t1, t2) -> sprintf "%s %s" (renderTerm 2 t1) (renderTerm 2 t2) |> addParens 2
    | Divide(t1, t2) -> sprintf "%s / %s" (renderTerm 3 t1) (renderTerm 3 t2) |> addParens 3
    | Square t -> sprintf "%s²" (renderTerm 4 t) |> addParens 4
    | Variable -> "x"
let renderEquation (Equation(lhs, rhs, env)) =
    match env with
    | Some x ->
        sprintf "%s = %s when x = %s" (renderTerm 0 lhs) (renderTerm 0 rhs) (x.ToString())
    | None ->
        sprintf "%s = %s" (renderTerm 0 lhs) (renderTerm 0 rhs)
let renderProblem (Equation(lhs, rhs, env)) =
    match env with
    | Some x ->
        sprintf "%s when x = %s" (renderTerm 0 lhs) (x.ToString())
    | None ->
        sprintf "%s " (renderTerm 0 lhs)

type Game = {
    settings: Settings
    problem: Equation
    score: int
    messageToUser: {| color: string; msg: string |} option
    currentAnswer: string
    showOptions: bool
    } with
    static member Fresh(?settings) =
        let settings = match settings with | Some v -> v | None -> retrievePersisted "settings" Settings.Default
        {
            settings = settings
            problem = generate()
            score = 0
            currentAnswer = ""
            messageToUser = None
            showOptions = false
        }
    static member nextProblem (g: Game) = { g with problem = generate() }

    static member CurrentProblem (this: Game) =
        renderProblem this.problem
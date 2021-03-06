(*
  B2R2.FsOptParse - FSharp-based Command Line Argument Parsing

  Author: Sang Kil Cha <sangkil.cha@gmail.com>

  Copyright (c) 2014 Sang Kil Cha

  Permission is hereby granted, free of charge, to any person obtaining a copy
  of this software and associated documentation files (the "Software"), to deal
  in the Software without restriction, including without limitation the rights
  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
  copies of the Software, and to permit persons to whom the Software is
  furnished to do so, subject to the following conditions:

  The above copyright notice and this permission notice shall be included in
  all copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
  THE SOFTWARE.
*)

module B2R2.FsOptParse

open System
open System.Text.RegularExpressions

exception SpecErr of string
exception RuntimeErr of string

let specErr msg = raise (SpecErr msg)
let rtErr msg = raise (RuntimeErr msg)

type Args = string array

let sanitizeExtra (n: int) =
  if n < 0 then specErr "Extra field should be positive"
  else n

let rec removeDashes (s: string) =
  if s.[0] = '-' then removeDashes s.[1..] else s

let sanitizeShort (opt: string) =
  if opt.Length = 0 then opt
  else
    let opt = removeDashes opt
    if opt.Length = 1 then "-" + opt
    else specErr (sprintf "Invalid short option %s is given" opt)

let sanitizeLong (opt: string) =
  if opt.Length = 0 then opt
  else
    let opt = "--" + (removeDashes opt)
    if opt.Length > 2 then opt
    else specErr (sprintf "Invalid long option %s is given" opt)

type 'a Option ( descr, ?callback, ?required, ?extra, ?help,
                        ?short, ?long, ?dummy, ?descrColor ) =
  let cbDefault opts (_args:Args) = opts

  member __.Descr : string = descr
  member __.DescrColor : ConsoleColor option = descrColor
  member __.Callback : ('a -> Args -> 'a) = defaultArg callback cbDefault
  member __.Required : bool = defaultArg required false
  member __.Extra : int = defaultArg extra 0 |> sanitizeExtra
  member __.Help: bool = defaultArg help false
  member __.Short : string = defaultArg short "" |> sanitizeShort
  member __.Long : string = defaultArg long "" |> sanitizeLong
  member __.Dummy : bool = defaultArg dummy false

  interface IComparable<'a Option> with
    member this.CompareTo obj =
      compare (this.Short, this.Long) (obj.Short, obj.Long)

  interface IComparable with
    member this.CompareTo obj =
      match obj with
        | :? ('a Option) as obj -> (this :> IComparable<_>).CompareTo obj
        | _ -> specErr "Not an option"

  interface IEquatable<'a Option> with
    member this.Equals obj =
      this.Short = obj.Short && this.Long = obj.Long

  override this.Equals obj =
    match obj with
      | :? ('a Option) as obj -> (this :> IEquatable<_>).Equals obj
      | _ -> specErr "Not an option"

  override this.GetHashCode () =
    hash (this.Short, this.Long)

type 'a Spec = 'a Option list

let rec rep acc ch n = if n <= 0 then acc else rep (ch::acc) ch (n-1)

let getExtra extraCnt descr =
  let pattern = @"<([a-zA-Z0-9]+)>"
  let m = Regex.Matches(descr, pattern)
  if m.Count > 0 && m.Count <= extraCnt then
    Seq.fold (fun (acc:string) (m:Match) ->
      acc + " <" + m.Groups.[1].Value + ">"
    ) "" (Seq.cast m)
  else
    " <OPT>"

let extraString extraCnt descr =
  if extraCnt > 0 then getExtra extraCnt descr else ""

let optStringCheck short long =
  if short = "" && long = "" then specErr "Optstring not given" else short, long

let fullOptStr (opt: 'a Option) =
  let l = opt.Long.Length
  let s = opt.Short.Length
  if l > 0 && s > 0 then
    opt.Short + "," + opt.Long + (extraString opt.Extra opt.Descr)
  elif l > 0 then opt.Long + (extraString opt.Extra opt.Descr)
  else opt.Short + (extraString opt.Extra opt.Descr)

let reqOpts reqset =
  Set.fold (fun (sb: System.Text.StringBuilder) (reqopt: 'a Option) ->
    let short, long = optStringCheck reqopt.Short reqopt.Long
    if short.Length = 0 then
      sprintf "%s%s " long (extraString reqopt.Extra reqopt.Descr) |> sb.Append
    else
      sprintf "%s%s " short (extraString reqopt.Extra reqopt.Descr) |> sb.Append
  ) (new System.Text.StringBuilder ()) reqset
  |> (fun sb -> let sb = sb.Append "[opts...]" in (sb.ToString ()).Trim())

let setColor = function
  | None -> ()
  | Some color -> Console.ForegroundColor <- color

let clearColor = function
  | None -> ()
  | Some _ -> Console.ResetColor ()

/// Show usage and exit.
let usageExec prog usgGetter (spec: 'a Spec) maxwidth reqset termFn =
  let spaceFill (str: string) =
    let margin = 5
    let space = maxwidth - str.Length + margin
    String.concat "" (rep [] " " space)
  (* printing a simple usage *)
  let usgForm = usgGetter ()
  let usgForm = if String.length usgForm = 0 then "Usage: %p %o" else usgForm
  let usgForm = usgForm.Replace ("%p", prog)
  let usgForm = usgForm.Replace ("%o", reqOpts reqset)
  (* required option must be presented in the usage *)
  Console.Write usgForm
  Console.Write "\n\n"
  (* printing a list of options *)
  List.iter (fun (opt: 'a Option) ->
    setColor opt.DescrColor
    if opt.Dummy then
      sprintf "%s\n" opt.Descr |> Console.Write
    else
      let _short, _long = optStringCheck opt.Short opt.Long
      let optstr = fullOptStr opt
      sprintf "%s%s: %s\n" optstr (spaceFill optstr) opt.Descr |> Console.Write
    clearColor opt.DescrColor
  ) spec
  "\n" |> Console.Write
  termFn ()

let setUpdate (opt: string) optset =
  if opt.Length > 0 then
    if Set.exists (fun s -> s = opt) optset then
      specErr (sprintf "Duplicated opt: %s" opt)
    else Set.add opt optset
  else optset

let checkSpec (spec: 'a Spec) =
  let _optset =
    List.fold (fun optset (opt: 'a Option) ->
      if opt.Dummy then optset
      else let short, long = optStringCheck opt.Short opt.Long
           setUpdate short optset |> setUpdate long
    ) Set.empty<string> spec
  in
  spec

let getSpecInfo (spec: 'a Spec) =
  List.fold (fun (width, (reqset: Set<'a Option>)) (optarg: 'a Option) ->
    let w =
      let opt = fullOptStr optarg
      let newwidth = opt.Length
      if newwidth > width then newwidth else width
    let r =
      if optarg.Required && not optarg.Dummy then Set.add optarg reqset
      else reqset
    w, r
  ) (0, Set.empty) spec (* maxwidth, required opts *)

let rec parse left (spec: 'a Spec) (args: Args) reqset usage state =
  if args.Length <= 0 then
    if Set.isEmpty reqset then List.rev left, state
    else rtErr "Required arguments not provided"
  else
    let args, left, reqset, state = specLoop args reqset left usage state spec
    parse left spec args reqset usage state
and specLoop args reqset left usage state = function
  | [] ->
      args.[1..], (args.[0] :: left), reqset, state
  | (optarg: 'a Option)::rest ->
      let m, args, reqset, state =
        if optarg.Dummy then false, args, reqset, state
        else argMatch optarg args reqset usage state
      if m then args, left, reqset, state
      else specLoop args reqset left usage state rest
and argMatch (optarg: 'a Option) args reqset usage state =
  let argNoMatch = (false, args, reqset, state)
  let s, l = optStringCheck optarg.Short optarg.Long
  let extra = optarg.Extra
  if s = args.[0] || l = args.[0] then
    argMatchRet optarg args reqset extra usage state
  elif String.length s > 0 && args.[0].StartsWith(s) && extra > 0 then
    (* Short options can have extra argument without having a space char. *)
    let splittedArg = [| args.[0].[0..1]; args.[0].[2..] |]
    let args = Array.concat [splittedArg; args.[1..]]
    argMatchRet optarg args reqset extra usage state
  elif args.[0].Contains("=") then
    let splittedArg = args.[0].Split([|'='|], 2)
    if s = splittedArg.[0] || l = splittedArg.[0] then
      let args = Array.concat [splittedArg; args.[1..]]
      argMatchRet optarg args reqset extra usage state
    else argNoMatch
  else argNoMatch
and argMatchRet (optarg: 'a Option) args reqset extra usage state =
  if (args.Length - extra) < 1 then
    rtErr (sprintf "Extra arg not given for %s" args.[0])
  elif optarg.Help then usage (); exit 0
  else
    let state': 'a =
      try optarg.Callback state args.[1..extra]
      with e -> (eprintfn "Callback failure for %s" args.[0]); rtErr e.Message
    (true, args.[(1+extra)..], Set.remove optarg reqset, state')

/// Parse command line arguments and return a list of unmatched arguments.
let optParse spec usageGetter prog (args: Args) state =
  let maxwidth, reqset = checkSpec spec |> getSpecInfo
  let usage () = usageExec prog usageGetter spec maxwidth reqset ignore
  if args.Length < 0 then usage (); rtErr "No argument given"
  else parse [] spec args reqset usage state

let usagePrint spec prog usageGetter termFn =
  let maxwidth, reqset = checkSpec spec |> getSpecInfo
  usageExec prog usageGetter spec maxwidth reqset termFn

// vim: set tw=80 sts=2 sw=2:

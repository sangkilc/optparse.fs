B2R2.FsOptParse: An F# Command Line Parsing Library
===============================================

B2R2.FsOptParse library (B2R2.FsOptParse.dll) implements command-line parsing
APIs that are succinct and clean. It is completely written in a single F# file
(fs). It is very intuitive to use, and also provides lots of convenient
command-line parsing features.

B2R2.FsOptParse exposes just two functions including `optParse` and `usageExit`.
The `optParse` function takes in a specification of command line options, a
program name, and a list of arguments from a user as input. It then parses the
input arguments and calls corresponding callback functions registered through
the specification as per interpreting each encountered option. Finally, it
returns a list of unmatched arguments. The `usageExit` function prints out a
well-formed usage based on a given specification, and terminates the program.

Build
-----
B2R2.FsOptParse relies on .NET Core. Simply type `dotnet build` in a terminal.

Package
-------
Available in NuGet.

[![NuGet Status](http://img.shields.io/nuget/v/B2R2.FsOptParse.svg?style=flat)](https://www.nuget.org/packages/B2R2.FsOptParse/)

Example
-------

The src/OptTest.fsx file contains an example usage.


```fsharp
open B2R2.FsOptParse

(** defines a state to pass to the option parser *)
type opts =
  {
    optX : int;
    optY : bool;
    optZ : string;
  }

(** default option state *)
let defaultOpts =
  {
    optX = 0;
    optY = false;
    optZ = "";
  }

(*
  An example command line specification, which is a list of Options.
  Each Option describes a command line option (switch) that is specified with
  either a short (a single-dash option) or long option (a double-dash option).
*)
let spec =
  [
    (* This option can be specified with -x <NUM>. There is an extra argument to
       specify a value in integer. *)
    Option ((* description of the option *)
            descr="this is a testing param X",
            (* how many extra argument must be provided by a user? *)
            extra=1,
            (* callback sets up the option and returns it *)
            callback=(fun opts arg -> {opts with optX=(int) arg.[0]}),
            (* use a short option style -x *)
            short="-x"
           );

    (* This option can be specified with -y. There is no extra argument. This
       option just sets a flag, optY. *)
    Option ((* description of the option *)
            descr="this is a testing param Y",
            (* set the option to be true *)
            callback=(fun opts _ -> {opts with optY=true}),
            (* use a short option style (-y) *)
            short="-y",
            (* also use a long option style (--yoohoo) *)
            long="--yoohoo"
           );

    (* A dummy option to pretty-print the usage *)
    Option ((* description of the option *)
            descr="",
            dummy=true
           );
    Option ((* description of the option *)
            descr="[Required Options]",
            descrColor=System.ConsoleColor.DarkCyan,
            dummy=true
           );

    (* The third option is a required option. In other words, option parsing
       will raise an exception if this option is not given by a user. This
       option takes in an additional integer argument, and set it to the global
       variable z. *)
    Option ((* description of the option *)
            descr="required parameter <STRING> with an integer option",
            (* callback to set the optZ value *)
            callback=(fun opts arg -> {opts with optZ=arg.[0]}),
            (* specifying this is a required option *)
            required=true,
            (* one additional argument to specify an integer value *)
            extra=1,
            (* use only a long option style *)
            long="--req"
           );
  ]

[<EntryPoint>]
let main (args:string[]) =
  let prog = "opttest.fsx"
  let args = System.Environment.GetCommandLineArgs ()
  let usageGetter () = "[Usage]\n  %p %o"
  try
    let left, opts = optParse spec usageGetter prog args defaultOpts
    printfn "Rest args: %A, x: %d, y: %b, z: %s"
      left opts.optX opts.optY opts.optZ
    0
  with
    | SpecErr msg ->
        eprintfn "Invalid spec: %s" msg
        exit 1
    | RuntimeErr msg ->
        eprintfn "Invalid args given by user: %s" msg
        usagePrint spec prog usageGetter (fun () -> exit 1)
```

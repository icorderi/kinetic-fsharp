﻿namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Kinetic")>]
[<assembly: AssemblyProductAttribute("Kinetic")>]
[<assembly: AssemblyDescriptionAttribute("Kinetic Protocol .Net library         ")>]
[<assembly: AssemblyVersionAttribute("1.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0"

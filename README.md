# FO4 Base Library

## Licence

This project is licensed under the **GNU General Public License version 3**.
The full text is in `LICENSE`. Credits are in `LICENSE_CREDITS.txt`, and the
per-component copyright lines, licence texts and source-code offer are in
`THIRD-PARTY-NOTICES.md` and the `licenses/` folder.

## API stability — there is none, on purpose

**This assembly does not promise a stable public API to third parties.** It is an internal component of
the NPC Manager / Wardrobe Manager package: `Public` here means "reachable from the sibling projects in
this solution", not "supported contract". Types, members and whole namespaces are renamed, merged and
deleted whenever the engine-faithful model underneath changes, without a deprecation cycle.

This is a deliberate decision (2026-08-22), taken after an external audit flagged the removal of
`RecordDispatcher`, `NpcSubrecordWriter`, `NpcVmadScanner` and the `*Record` classes — about 244 public
declarations — as an API break. It is a break, and it is fine: the only consumers are in this repository
and they were migrated in the same commit. Keeping a compatibility facade over the canonical engine would
mean maintaining two surfaces for the same law, which is exactly what this codebase avoids.

If you link against this DLL from outside the package, pin a commit. Do not expect the next one to build.

## Requires the following libraries/packages

 - ManoloV02: BSA/BA2 Library - Licensed under the GPL-3.0 License (https://github.com/MANOLOV02/BSA_BA2_Library_DLL)
 - ManoloV02: DirectXTexWrapper - Licensed under the GPL-3.0 License (https://github.com/MANOLOV02/DirectXTexWrapper)
 - Ousnius: NiflySharp - Licensed under the GPL-3.0 License (https://github.com/MANOLOV02/NiflySharpFork)
     MODIFICADO. Fork de https://github.com/ousnius/NiflySharp con cambios propios; el fuente correspondiente es el del fork
 - Lukas Cone: HavokLib - Licensed under the GPL-3.0 License (https://github.com/PredatorCZ/HavokLib)
     hkaLosslessCompressedAnimation decoder ported into FO4_Base_Library/HkxLosslessAnimationGraphParser.vb
 - Microsoft: DirectXTex - Licensed under the MIT License (https://github.com/microsoft/DirectXTex)
     wrapped by DirectXTexWrapper
 - Ousnius: Material Editor - Licensed under the MIT License (https://github.com/ousnius/Material-Editor)
 - Stefanos Apostolopoulos: OpenTK (Core, Graphics, Mathematics, Windowing) - Licensed under the MIT License (https://github.com/opentk/opentk)
     Copyright (c) 2006-2018 Stefanos Apostolopoulos, for the Open Toolkit library
 - Team OpenTK: OpenTK.GLControl - Licensed under the MIT License (https://github.com/opentk/opentk)
     Copyright (c) 2025 Team OpenTK
 - Marcus Geelnard, Camilla Löwy: GLFW (glfw3.dll) - Licensed under the zlib/libpng License (https://www.glfw.org)
     redistributed through OpenTK.redist.glfw
 - SharpZipLib Contributors: SharpZipLib 1.4.2 - Licensed under the MIT License (https://github.com/icsharpcode/SharpZipLib)
 - Milosz Krajewski: K4os.Compression.LZ4 (+ .Streams) - Licensed under the MIT License (https://github.com/MiloszKrajewski/K4os.Compression.LZ4)
     Copyright (c) 2017 Milosz Krajewski
 - Milosz Krajewski: K4os.Hash.xxHash - Licensed under the MIT License (https://github.com/MiloszKrajewski/K4os.Hash.xxHash)
     Copyright (c) 2017 Milosz Krajewski
 - Lorenzo Delana: miniball - Licensed under the Apache-2.0 License (https://github.com/SearchAThing-forks/miniball)
     fork of https://github.com/hbf/miniball by Martin Kutz (FU Berlin), Kaspar Fischer (ETH Zurich) and Bernd Gaertner (ETH Zurich); reached through NiflySharp
 - Microsoft: System.IO.Pipelines - Licensed under the MIT License (https://github.com/dotnet/runtime)
 - Microsoft: Ijwhost (Ijwhost.dll) - Licensed under the MIT License (https://github.com/dotnet/runtime)
     C++/CLI host shim required by DirectXTexWrapper
 - ElminsterAU and the xEdit contributors: xEdit - Licensed under the MPL-2.0 License (https://github.com/TES5Edit/TES5Edit)
     the plugin format declarations are mechanically translated into the schema tables; see THIRD-PARTY-NOTICES.md

## Build

Build with MSBuild, configuration `Publish`:

```
msbuild FO4_Base_Library.vbproj -t:Restore,Build -p:Configuration=Publish -p:Platform=x64
```

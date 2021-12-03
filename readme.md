# Benchmark for prototyping msbuild with faster static-graph/caching

Simple benchmark to test hosting msbuild in a "server" like mode and compile lots of projects.

This is to measure the raw cost of msbuild when compiling a tree of deep projects:
- Clean, build all
- Touch 1 C# file in the root project, build all
- Change 1 C# file affecting ref assemblies in the leaf project, build all
- No changes, build all

This repository is using a fork of msbuild [here](https://github.com/xoofx/msbuild/commit/f5e04d8fcc519af8549bf2c16d0548535f3af33f) to achieve higher build performance by enabling parallelized builds + caching 
while today msbuild can do caching only sequential and single threaded (see this [issue](https://github.com/dotnet/msbuild/issues/7112))

> Disclaimer
>
> This is a playground, highly experimental, code super dirty, read it at your own risk!

## Building

- First, you need to be very brave 😅 
- The code assumes that you have installed [dotnet 6.0.100 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- Assuming that you checkout this project in a $root folder (so `$root\BenchBuild`)
- You need to checkout this msbuild [commit](https://github.com/xoofx/msbuild/commit/f5e04d8fcc519af8549bf2c16d0548535f3af33f) in `$root\dotnet\msbuild`
- Open the `$root\dotnet\msbuild\MSBuild.sln` solution and compile in `Release` (and/or `Debug`) the `MSBuild` project (other project might fail)
- Open the `$root\BenchBuild\BenchBuild.sln` and build the solution (in the same config than msbuild)
- Then you can run `BenchBuild` in the solution
- You should see an output similar to this:

```
============================================================================
Generate Projects
****************************************************************************
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj 1
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild1_0\LibChild1_0.csproj 4
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild1_1\LibChild1_1.csproj 4
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild1_2\LibChild1_2.csproj 4
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild1_3\LibChild1_3.csproj 4
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild1_4\LibChild1_4.csproj 4
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild1_5\LibChild1_5.csproj 4
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild1_6\LibChild1_6.csproj 4
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild1_7\LibChild1_7.csproj 4
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild1_8\LibChild1_8.csproj 4
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_0\LibChild2_0.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_1\LibChild2_1.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_2\LibChild2_2.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_3\LibChild2_3.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_4\LibChild2_4.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_5\LibChild2_5.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_6\LibChild2_6.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_7\LibChild2_7.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_8\LibChild2_8.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_9\LibChild2_9.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_10\LibChild2_10.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_11\LibChild2_11.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_12\LibChild2_12.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_13\LibChild2_13.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_14\LibChild2_14.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_15\LibChild2_15.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_16\LibChild2_16.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_17\LibChild2_17.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_18\LibChild2_18.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_19\LibChild2_19.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_20\LibChild2_20.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_21\LibChild2_21.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_22\LibChild2_22.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_23\LibChild2_23.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_24\LibChild2_24.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_25\LibChild2_25.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_26\LibChild2_26.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_27\LibChild2_27.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_28\LibChild2_28.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_29\LibChild2_29.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_30\LibChild2_30.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_31\LibChild2_31.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_32\LibChild2_32.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_33\LibChild2_33.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_34\LibChild2_34.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_35\LibChild2_35.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_36\LibChild2_36.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_37\LibChild2_37.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_38\LibChild2_38.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_39\LibChild2_39.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_40\LibChild2_40.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_41\LibChild2_41.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_42\LibChild2_42.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_43\LibChild2_43.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_44\LibChild2_44.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_45\LibChild2_45.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_46\LibChild2_46.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_47\LibChild2_47.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_48\LibChild2_48.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_49\LibChild2_49.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_50\LibChild2_50.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild2_51\LibChild2_51.csproj 16
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_0\LibChild3_0.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_1\LibChild3_1.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_2\LibChild3_2.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_3\LibChild3_3.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_4\LibChild3_4.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_5\LibChild3_5.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_6\LibChild3_6.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_7\LibChild3_7.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_8\LibChild3_8.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_9\LibChild3_9.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_10\LibChild3_10.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_11\LibChild3_11.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_12\LibChild3_12.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_13\LibChild3_13.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_14\LibChild3_14.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_15\LibChild3_15.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_16\LibChild3_16.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_17\LibChild3_17.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_18\LibChild3_18.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_19\LibChild3_19.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_20\LibChild3_20.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_21\LibChild3_21.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_22\LibChild3_22.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_23\LibChild3_23.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_24\LibChild3_24.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_25\LibChild3_25.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_26\LibChild3_26.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_27\LibChild3_27.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_28\LibChild3_28.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_29\LibChild3_29.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_30\LibChild3_30.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_31\LibChild3_31.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_32\LibChild3_32.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_33\LibChild3_33.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_34\LibChild3_34.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_35\LibChild3_35.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibChild3_36\LibChild3_36.csproj 64
Generating C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibLeaf\LibLeaf.csproj 256
RootProject C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
============================================================================
Load Projects
****************************************************************************
Time to load: 86.7894ms
============================================================================
Restore Projects
****************************************************************************
Static graph loaded in 0.748 seconds: 100 nodes, 323 edges
  Determining projects to restore...
  All projects are up-to-date for restore.
=== Time to Restore 0 projects: 1885.7324ms
============================================================================
Build caches
****************************************************************************
=== Time to Build Cache 1984.1595ms
============================================================================
Build All - No Changes
****************************************************************************
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[0] Time to build 0 projects: 176.1146ms
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[1] Time to build 0 projects: 140.113ms
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[2] Time to build 0 projects: 158.1421ms
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[3] Time to build 0 projects: 146.8229ms
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[4] Time to build 0 projects: 144.5599ms
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[5] Time to build 0 projects: 144.7248ms
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[6] Time to build 0 projects: 156.7329ms
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[7] Time to build 0 projects: 147.8645ms
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[8] Time to build 0 projects: 150.9445ms
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[9] Time to build 0 projects: 144.8878ms
============================================================================
Build All - 1 C# file changed in root
****************************************************************************
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[0] Time to build 0 projects: 177.8851ms
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[1] Time to build 0 projects: 175.109ms
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[2] Time to build 0 projects: 186.5913ms
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[3] Time to build 0 projects: 171.8463ms
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[4] Time to build 0 projects: 169.6732ms
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[5] Time to build 0 projects: 164.1012ms
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[6] Time to build 0 projects: 168.6143ms
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[7] Time to build 0 projects: 165.6153ms
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[8] Time to build 0 projects: 172.9465ms
Building C:\work\BenchBuild\BenchBuild\bin\Release\net6.0\projects\LibRoot\LibRoot.csproj
[9] Time to build 0 projects: 166.6576ms
```




## License

This software is released under the [BSD-Clause 2 license](https://opensource.org/licenses/BSD-2-Clause).

## Author

Alexandre Mutel aka [xoofx](http://xoofx.com).

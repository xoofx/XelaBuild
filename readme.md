# Benchmark for prototyping msbuild with faster static-graph/caching

Simple benchmark to test hosting msbuild in a "server" like mode, with improvements to msbuild to make it 
build a graph of projects a lot more faster.

This is to measure the raw cost of msbuild when compiling a tree of deep projects:
- Clean, build all
- Touch 1 C# file in the root project, build all
- Change 1 C# file affecting ref assemblies in the leaf project, build all
- No changes, build all

This repository is using a fork of msbuild [here](https://github.com/xoofx/msbuild/tree/XelaBuild) to achieve higher build performance by enabling parallelized builds + caching 
while today msbuild can do caching only sequential and single threaded (see this [issue](https://github.com/dotnet/msbuild/issues/7112))

> Disclaimer
>
> This is a playground, highly experimental, code super dirty, read it at your own risk!
> The changes to msbuild are not fullproof, I have experienced nodes shutting down in weird ways and the client blocking.

## Why?

Today, msbuild is used mostly dynamically to evaluate build graphs and execute targets.

Recently was introduced a new way to build projects with msbuild by using [static graphs](https://github.com/dotnet/msbuild/blob/main/documentation/specs/static-graph.md#static-graph)

Static graphs are great because they provide the knowledge up-front of the entire project graph to compile,
and this allows to schedule more efficiently the build of projects.

With the static-graph mode, msbuild has introduced an `isolate` project mode:

- Usually, when you compile a Project A that has many project dependencies (B1, B2...), building project A
with msbuild will require msbuild to issue lots of msbuild tasks to query build result metadatas from 
project dependencies (e.g `B1.GetTargetFrameworks`). The problem is that calling msbuild on each dependencies
can be very costly because it requires to load all the target files associated to a project, evaluate all the properties...etc.
- With the isolate mode, you can create a cache of a project that would contain the results of these metadatas.
 You could then build Project A by reusing caches for their dependencies (e.g B1.csproj.cache)

But the design of `isolate` was made in a specific case (see this [issue](https://github.com/dotnet/msbuild/issues/7112)) 
that was forcing the compilation of each projects sequentially and from a single thread. So you couldn't 
build an entire graph of projects in reverse order of topological sort in parallel.

This repo validates the idea by bringing to msbuild a way to use isolated builds on msbuild nodes, so 
that we can now build caches in // while building projects.

The performance improvements can be dramatic. For example, for a tree of 100 C# projects (a root project, several layers 
of child projects, and a single leaf project referenced by all children), the full build of these projects takes:

- 7s to 9s with Visual Studio/msbuild.
- 1.5 to 2s with the solution experimented in this repository.

When re-compiling a root project referencing the other 99 C# projects:

- 1.5s with Visual Studio/msbuild
- 150ms with the solution experimented.

So the solution in this experiment brings almost a **speedup factor of x3 to x10 in build time**.

## Building

- First, you need to be very brave 😅 
- The code assumes that you have installed [dotnet 6.0.100 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- Assuming that you checkout this project in a $root folder (so `$root\XelaBuild`)
- You need to checkout this msbuild [XelaBuild](https://github.com/xoofx/msbuild/tree/XelaBuild) branch in `$root\dotnet\msbuild`
- Open the `$root\dotnet\msbuild\MSBuild.sln` solution and compile in `Release` (and/or `Debug`) the `MSBuild` project (other project might fail)
- Open the `$root\XelaBuild\XelaBuild.sln` and build the solution (in the same config than msbuild)
- Then you can run `XelaBuild` in the solution
- You should see an output similar to this:

```
============================================================================
Load Projects and graph
****************************************************************************
Time to load and evaluate 100 projects: 1185.9211ms
============================================================================
Restore All
****************************************************************************
[0] Time to build 1 projects: 4098.4751ms
[1] Time to build 1 projects: 908.119ms
[2] Time to build 1 projects: 1324.8088ms
[3] Time to build 1 projects: 757.075ms
[4] Time to build 1 projects: 676.2929ms
============================================================================
Build All (Clean)
****************************************************************************
[0] Time to build 100 projects: 4682.1972ms
[1] Time to build 100 projects: 2274.4678ms
[2] Time to build 100 projects: 2157.9376ms
[3] Time to build 100 projects: 2202.1063ms
[4] Time to build 100 projects: 2170.6099ms
============================================================================
Build All - No changes
****************************************************************************
[0] Time to build 100 projects: 1598.5466ms
[1] Time to build 100 projects: 1558.85ms
[2] Time to build 100 projects: 1528.7022ms
[3] Time to build 100 projects: 1475.9761ms
[4] Time to build 100 projects: 1542.7226ms
============================================================================
Build Root - No Changes
****************************************************************************
[0] Time to build 1 projects: 170.2991ms
[1] Time to build 1 projects: 155.3358ms
[2] Time to build 1 projects: 143.5088ms
[3] Time to build 1 projects: 160.9169ms
[4] Time to build 1 projects: 146.1304ms
============================================================================
Build Root - 1 C# file changed in root
****************************************************************************
[0] Time to build 1 projects: 155.7713ms
[1] Time to build 1 projects: 141.6611ms
[2] Time to build 1 projects: 153.7697ms
[3] Time to build 1 projects: 168.6773ms
[4] Time to build 1 projects: 155.2992ms
============================================================================
Build All - 1 C# file changed in leaf
****************************************************************************
[0] Time to build 100 projects: 1533.7444ms
[1] Time to build 100 projects: 1557.8705ms
[2] Time to build 100 projects: 1588.6562ms
[3] Time to build 100 projects: 1557.4441ms
[4] Time to build 100 projects: 1575.8741ms
```

## License

This software is released under the [BSD-Clause 2 license](https://opensource.org/licenses/BSD-2-Clause).

## Author

Alexandre Mutel aka [xoofx](http://xoofx.com).

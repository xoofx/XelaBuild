# Benchmark for hosting msbuild

Simple benchmark to test hosting msbuild in a "server" like mode and compile lots of projects.

This is to measure the raw cost of msbuild when compiling a tree of deep projects:
- Clean, build all
- Touch 1 C# file in the root project, build all
- No changes, build all

> Disclaimer
>
> This is a playground. Usage of msbuild API might be not correct or not efficient.

## License

This software is released under the [BSD-Clause 2 license](https://opensource.org/licenses/BSD-2-Clause).

## Author

Alexandre Mutel aka [xoofx](http://xoofx.com).

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Execution;

namespace BenchBuild;

public static class BuildResultHelper
{
    // Microsoft.Build.BackEnd.BinaryTranslator 
    //    static internal ITranslator GetReadTranslator(Stream stream, SharedReadBuffer buffer)
    private static readonly MethodInfo _methodGetReadTranslator;
    //    static internal ITranslator GetWriteTranslator(Stream stream)
    private static readonly MethodInfo _methodGetWriteTranslator;

    // ITranslator
    //    void Translate<T>(ref T value) where T : ITranslatable, new ();
    private static readonly MethodInfo _methodTranslateBuildResult;

    static BuildResultHelper()
    {
        var binaryTranslatorType = typeof(BuildResult).Assembly.GetType("Microsoft.Build.BackEnd.BinaryTranslator");
        _methodGetReadTranslator = binaryTranslatorType.GetMethod("GetReadTranslator", BindingFlags.Static | BindingFlags.NonPublic);
        _methodGetWriteTranslator = binaryTranslatorType.GetMethod("GetWriteTranslator", BindingFlags.Static | BindingFlags.NonPublic);

        var translatorType = typeof(BuildResult).Assembly.GetType("Microsoft.Build.BackEnd.ITranslator");
        var methods = translatorType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(x => x.Name == "Translate");
        foreach(var method in methods)
        {
            var genericArguments = method.GetGenericArguments();
            if (genericArguments.Length != 1) continue;
            if (method.GetParameters().Length != 1) continue;

            var parameter = method.GetParameters()[0];
            if (parameter.ParameterType.IsByRef && !parameter.IsOut)
            {
                _methodTranslateBuildResult = method;
                break;
            }
        }

        _methodTranslateBuildResult = _methodTranslateBuildResult.MakeGenericMethod(new Type[] { typeof(BuildResult) });
    }

    public static void Serialize(BuildResult result, Stream stream)
    {
        using var translator = GetWriteTranslator(stream);
        Translate(translator, ref result);
    }

    public static BuildResult Deserialize(Stream stream)
    {
        using var translator = GetReadTranslator(stream);
        BuildResult result = null;
        Translate(translator, ref result);
        return result;
    }

    private static IDisposable GetReadTranslator(Stream stream)
    {
        return (IDisposable)_methodGetReadTranslator.Invoke(null, new object[] { stream, null });
    }

    private static IDisposable GetWriteTranslator(Stream stream)
    {
        return (IDisposable)_methodGetWriteTranslator.Invoke(null, new object[] { stream });
    }

    private static void Translate(object translator, ref BuildResult result)
    {
        var objects = new object[1];
        objects[0] = result;
        _methodTranslateBuildResult.Invoke(translator, objects);
        result = (BuildResult)objects[0];
    }
}
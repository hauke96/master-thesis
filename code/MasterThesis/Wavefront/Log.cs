using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wavefront;

public class Log
{
    private static int _normalCallDepth = 0;
    private static readonly string _indentString = "  ";

    public static readonly int DEBUG = 0;
    public static readonly int INFO = 1;

    public static int LogLevel = INFO;

    public static void Init()
    {
        _normalCallDepth = GetCallDepth();
    }

    public static void I(string message, string prefix = "", int manualIndentOffset = 0)
    {
        var indentation =
            string.Concat(Enumerable.Repeat(_indentString, manualIndentOffset));
        Console.WriteLine(indentation + prefix + message);
    }

    public static void D(string message, string prefix = "", int manualIndentOffset = 0)
    {
        if (LogLevel <= DEBUG)
        {
            I(message, prefix, GetCallDepth() - _normalCallDepth + (manualIndentOffset - 1));
        }
    }

    public static void Note(string message)
    {
        D(message, "- ", -1);
    }

    private static int GetCallDepth()
    {
        StackTrace stackTrace = new StackTrace();
        return stackTrace.GetFrames().Length;
    }
}
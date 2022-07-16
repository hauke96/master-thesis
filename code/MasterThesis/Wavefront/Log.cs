using System.Diagnostics;

namespace Wavefront;

public class Log
{
    private static int _normalCallDepth = 0;
    private static string _indentString = "  ";

    public static void Init()
    {
        _normalCallDepth = GetCallDepth();
    }

    public static void i(string message, string prefix = "", int manualIndentOffset = 0)
    {
        var indentation =
            string.Concat(Enumerable.Repeat(_indentString, GetCallDepth() - _normalCallDepth + manualIndentOffset));
        Console.WriteLine(indentation + prefix + message);
    }

    public static void note(string message)
    {
        i(message, "- ", -1);
    }

    private static int GetCallDepth()
    {
        StackTrace stackTrace = new StackTrace();
        return stackTrace.GetFrames().Length;
    }
}
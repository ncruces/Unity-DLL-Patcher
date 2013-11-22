using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PatchDlls
{
    class Program
    {
        static void Main(string[] args)
        {
            var ILDASM = Path.Combine(
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\Windows", "CurrentInstallFolder", "") as string,
                "Bin", "ildasm.exe");

            var ILASM = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Microsoft.NET", "Framework", "v2.0.50727", "ilasm.exe");

            var assemblyName = Path.GetFileNameWithoutExtension(args[0]);

            Run(ILDASM, "\"{0}\" /nobar /out=disasm.il", args[0]);
            var source = File.ReadAllText("disasm.il");
            var builder = new StringBuilder();

            source = PatchInterlockedExchange(source);
            source = PatchInterlockedCompareExchange(source);

            if (args.Length > 1)
            {
                foreach (var file in Directory.EnumerateFiles(args[1]))
                {
                    var patch = File.ReadAllLines(file);

                    var header = patch.First();
                    var footer = patch.Last();
                    var start = source.IndexOf(header);
                    var end = source.LastIndexOf(footer) + footer.Length;

                    builder.Clear();
                    builder.Append(source, 0, start);
                    foreach (var line in patch) builder.AppendLine(line);
                    builder.Append(source, end, source.Length - end);

                    source = builder.ToString();
                }
            }

            File.WriteAllText("disasm.il", source);
            Run(ILASM, "disasm.il /resource=disasm.res /dll /out=\"{0}\"", args[0]);

            File.Delete("disasm.il");
            File.Delete("disasm.res");
        }

        static void Run(string fileName, string format, params object[] args)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments =  string.Format(format, args),
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
            }).WaitForExit();
        }

        static string PatchInterlockedExchange(string source)
        {
            return Regex.Replace(source,
                @"!!0 \[mscorlib\]System\.Threading\.Interlocked::Exchange<.*?>\(.*?\)",
                "object [mscorlib]System.Threading.Interlocked::Exchange(object&, object)",
                RegexOptions.Singleline);
        }

        static string PatchInterlockedCompareExchange(string source)
        {
            return Regex.Replace(source,
                @"!!0 \[mscorlib\]System\.Threading\.Interlocked::CompareExchange<.*?>\(.*?\)",
                "object [mscorlib]System.Threading.Interlocked::CompareExchange(object&, object, object)",
                RegexOptions.Singleline);
        }
    }
}

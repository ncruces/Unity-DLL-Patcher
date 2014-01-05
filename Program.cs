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
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var res = Path.ChangeExtension(path, "res");
            var il = Path.ChangeExtension(path, "il");
            var assembly = args[0];

            Ildasm(assembly, il);
            var source = File.ReadAllText(il);
            var debug = GetDebuggableAttribute(source);
            var builder = new StringBuilder();

            source = PatchInterlockedExchange(source);
            source = PatchInterlockedCompareExchange(source);

            if (args.Length > 1)
            {
                foreach (var file in Directory.EnumerateFiles(args[1], "*.il", SearchOption.AllDirectories))
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

            File.WriteAllText(il, source);
            Ilasm(il, res, assembly, debug);

            File.Delete(il);
            File.Delete(res);
        }

        static void Ilasm(string il, string res, string assembly, DebuggableAttribute debug)
        {
            var ilasm = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Microsoft.NET", "Framework", "v2.0.50727", "ilasm.exe");

            // DLL or EXE
            var args = Path.GetExtension(assembly).Replace('.', '/');

            // Debugging
            if (debug.IsJITOptimizerDisabled)
            {
                args += debug.DebuggingFlags.HasFlag(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints) ?
                    " /debug=impl" :
                    " /debug";
            }
            else if (debug.DebuggingFlags.HasFlag(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints))
            {
                args += " /debug=opt";
            }

            // Optimization
            if (!debug.DebuggingFlags.HasFlag(DebuggableAttribute.DebuggingModes.DisableOptimizations))
            {
                args += " /optimize /fold";
            }

            Run(ilasm, "\"{0}\" /noautoinherit /resource=\"{1}\" /out=\"{2}\" {3}", il, res, assembly, args);
        }

        static void Ildasm(string assembly, string il)
        {
            var ildasm = Path.Combine(
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\Windows\v7.0A\WinSDK-NetFx35Tools", "InstallationFolder", "") as string,
                "ildasm.exe");

            Run(ildasm, "\"{0}\" /linenum /typelist /nobar /out=\"{1}\"", assembly, il);
        }

        static void Run(string fileName, string format, params object[] args)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = string.Format(format, args),
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
            }).WaitForExit();
        }

        static DebuggableAttribute GetDebuggableAttribute(string source)
        {
            var match = Regex.Match(source,
                @".custom instance void \[mscorlib\]System.Diagnostics.DebuggableAttribute::.ctor\(valuetype \[mscorlib\]System.Diagnostics.DebuggableAttribute/DebuggingModes\) = " +
                @"\( (..) (..) (..) (..) (..) (..) (..) (..) \)");
            var builder = match.Groups.Cast<Group>().Skip(1).Reverse().Aggregate(new StringBuilder(16), (s, g) => s.Append(g.Value));
            return new DebuggableAttribute((DebuggableAttribute.DebuggingModes)Convert.ToInt64(builder.ToString()));
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

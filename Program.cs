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
            var builder = new StringBuilder();

            source = PatchInterlockedExchange(source);
            source = PatchInterlockedCompareExchange(source);

            if (args.Length > 1)
            {
                foreach (var file in Directory.EnumerateFiles(args[1], "*.il"))
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
            Ilasm(il, res, assembly);

            File.Delete(il);
            File.Delete(res);
        }

        static void Ilasm(string il, string res, string assembly)
        {
            var ilasm = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Microsoft.NET", "Framework", "v2.0.50727", "ilasm.exe");

            Run(ilasm, "\"{0}\" /resource=\"{1}\" /out=\"{2}\" {3}", il, res, assembly, Path.GetExtension(assembly).Replace('.', '/'));
        }

        static void Ildasm(string assembly, string il)
        {
            var ildasm = Path.Combine(
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\Windows", "CurrentInstallFolder", "") as string,
                "Bin", "ildasm.exe");

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

using DiscUtils.Fat;
using DiscUtils.HfsPlus;
using DiscUtils.Iso9660;
using DiscUtils.Udf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DateFixer
{
    internal class Program
    {
        static string[] discImageFormats =
        {
            ".iso",
            ".img"
        };
        static string[] signedFilesFormat =
        {
            ".exe",
            ".dll",
            ".sys",
            ".efi",
            ".scr",
            ".msi",
            ".msu",
            ".appx",
            ".appxbundle",
            ".msix",
            ".msixbundle",
            ".cat",
            ".cab",
            ".js",
            ".vbs",
            ".wsf",
            ".ps1",
            ".xap"
        };

        static void Main(string[] args)
        {
            foreach (var path in args)
            {
                if (File.Exists(path))
                {
                    ProcessFile(path);
                }
                else if (Directory.Exists(path))
                {
                    ProcessDirectory(path);
                }
                else
                {
                    Console.WriteLine("Invalid path: " + path);
                }
            }

            if (System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine("DONE");
                Console.ReadKey();
            }
        }

        static void ProcessFile(string path)
        {
            string extension = Path.GetExtension(path).ToLower();
            DateTime? creationTime = null;

            if (discImageFormats.Contains(extension))
            {
                var fs = File.OpenRead(path);

                // ISO 9660
                try
                {
                    var reader = new CDReader(fs, true);
                    creationTime = reader.Root.CreationTimeUtc;
                    reader.Dispose();
                }
                catch (Exception) when (!System.Diagnostics.Debugger.IsAttached) { }

                // UDF
                try
                {
                    var reader = new UdfReader(fs);
                    creationTime = reader.Root.CreationTimeUtc;
                    reader.Dispose();
                }
                catch (Exception) when (!System.Diagnostics.Debugger.IsAttached) { }

                fs.Close();
            }

            if (signedFilesFormat.Contains(extension))
            {
                creationTime = SignatureManager.GetSignatureDate(path);
            }

            if (creationTime != null && creationTime != DateTime.MinValue)
            {
                try
                {
                    var attributes = File.GetAttributes(path);
                    File.SetAttributes(path, FileAttributes.Normal);

                    File.SetLastWriteTimeUtc(path, (DateTime)creationTime);
                    File.SetCreationTimeUtc(path, (DateTime)creationTime);

                    File.SetAttributes(path, attributes);

                    Console.WriteLine(Path.GetFileName(path) + " -> " + ((DateTime)creationTime).ToString());
                }
                catch (Exception) when (!System.Diagnostics.Debugger.IsAttached) { }
            }
        }

        static void ProcessDirectory(string path)
        {
            foreach (var file in Directory.GetFiles(path))
            {
                ProcessFile(file);
            }
            foreach(var folder in Directory.GetDirectories(path))
            {
                ProcessDirectory(folder);
            }
        }
    }
}

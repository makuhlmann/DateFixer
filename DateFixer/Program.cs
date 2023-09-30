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
            if (discImageFormats.Contains(extension))
            {
                DateTime? creationTime = null;
                var fs = File.OpenRead(path);

                // ISO 9660
                try
                {
                    var reader = new CDReader(fs, true);
                    creationTime = reader.Root.CreationTimeUtc;
                    reader.Dispose();
                }
                catch (Exception ex)
                {
                    //if (System.Diagnostics.Debugger.IsAttached)
                        //throw;
                }

                // UDF
                try
                {
                    var reader = new UdfReader(fs);
                    creationTime = reader.Root.CreationTimeUtc;
                    reader.Dispose();
                }
                catch (Exception ex)
                {
                    //if (System.Diagnostics.Debugger.IsAttached)
                    //throw;
                }

                fs.Close();

                if (creationTime != null && creationTime != DateTime.MinValue)
                {
                    File.SetLastWriteTimeUtc(path, (DateTime)creationTime);
                    File.SetCreationTimeUtc(path, (DateTime)creationTime);
                    Console.WriteLine(Path.GetFileName(path) + " -> " + ((DateTime)creationTime).ToString());
                    return;
                }
            }
            if (signedFilesFormat.Contains(extension))
            {
                var creationTime = SignatureManager.GetSignatureDate(path);
                if (creationTime != null)
                {
                    try
                    {
                        File.SetLastWriteTimeUtc(path, (DateTime)creationTime);
                        File.SetCreationTimeUtc(path, (DateTime)creationTime);
                        Console.WriteLine(Path.GetFileName(path) + " -> " + ((DateTime)creationTime).ToString());
                    } catch (Exception) { }
                }
                else
                {
                    //if (System.Diagnostics.Debugger.IsAttached)
                    //throw;
                }
            }
        }

        static void ProcessDirectory(string path)
        {
            var files = Directory.GetFiles(path);
            foreach (var file in files)
            {
                ProcessFile(file);
            }
            var folders = Directory.GetDirectories(path);
            foreach(var folder in folders)
            {
                ProcessDirectory(folder);
            }
        }
    }
}

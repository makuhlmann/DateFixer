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

        static DateTime minDate = new DateTime(1985, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        static bool scanIso = false;
        static bool scanSigned = false;
        static bool processedAtLeastOne = false;

        static void Main(string[] args)
        {
            foreach (var path in args)
            {
                if (path.StartsWith("/"))
                {
                    if (path.StartsWith("/i"))
                    {
                        scanIso = true;
                    }
                    else if (path.StartsWith("/s"))
                    {
                        scanSigned = true;
                    }
                    else if (path.StartsWith("/?"))
                    {
                        Console.WriteLine($"DateFixer Usage: datefixer [/i] [/s] path1 [path2] [path3] ...\n" +
                            $"/i - Process ISO files only\n" +
                            $"/s - Process signed files only\n" +
                            $"/? - Shows list of commands\n" +
                            $"Default: Processes both ISO and signed");
                        return;
                    }
                }
                else
                {
                    if (!scanIso && !scanSigned)
                        scanIso = scanSigned = true;

                    processedAtLeastOne = true;

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

            }

            if (!processedAtLeastOne)
                Console.WriteLine($"No valid path given as a parameter\n" +
                    $"See /? for more information");

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

            if (scanIso && discImageFormats.Contains(extension))
            {
                var fs = File.OpenRead(path);

                // ISO 9660
                try
                {
                    var reader = new CDReader(fs, true);
                    creationTime = reader.Root.CreationTimeUtc;

                    if (creationTime == null || creationTime < minDate)
                        creationTime = DeepScanIso(reader);

                    reader.Dispose();
                }
                catch (Exception) when (!System.Diagnostics.Debugger.IsAttached) { }

                // UDF
                if (creationTime == null || creationTime < minDate)
                {
                    try
                    {
                        var reader = new UdfReader(fs);
                        creationTime = reader.Root.CreationTimeUtc;

                        if (creationTime == null || creationTime < minDate)
                            creationTime = DeepScanIso(reader);

                        reader.Dispose();
                    }
                    catch (Exception) when (!System.Diagnostics.Debugger.IsAttached) { }
                }

                fs.Close();
            }

            if (scanSigned && signedFilesFormat.Contains(extension))
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

        static DateTime? DeepScanIso(CDReader reader)
        {
            DateTime result = DateTime.MinValue;
            foreach (var item in reader.GetFileSystemEntries(""))
            {
                DateTime itemDate = reader.GetFileInfo(item).CreationTimeUtc;
                if (itemDate > result)
                    result = itemDate;
            }
            return result;
        }
        static DateTime? DeepScanIso(UdfReader reader)
        {
            DateTime result = DateTime.MinValue;
            foreach (var item in reader.GetFileSystemEntries(""))
            {
                DateTime itemDate = reader.GetFileInfo(item).CreationTimeUtc;
                if (itemDate > result)
                    result = itemDate;
            }
            return result;
        }
    }
}

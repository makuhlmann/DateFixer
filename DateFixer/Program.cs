using DiscUtils.Fat;
using DiscUtils.HfsPlus;
using DiscUtils.Iso9660;
using DiscUtils.Udf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        static int filesProcessedCount = 0;

        static bool scanIso = false;
        static bool scanSigned = false;
        static bool processedAtLeastOne = false;
        static bool scanFileName = false;
        static bool quiet = false;
        static bool ignoreFormats = false;
        static bool recursive = false;

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
                    else if (path.StartsWith("/f"))
                    {
                        scanFileName = true;
                    }
                    else if (path.StartsWith("/q"))
                    {
                        quiet = true;
                    }
                    else if (path.StartsWith("/x"))
                    {
                        ignoreFormats = true;
                    }
                    else if (path.StartsWith("/r"))
                    {
                        recursive = true;
                    }
                    else if (path.StartsWith("/?"))
                    {
                        Console.WriteLine($"DateFixer Usage: datefixer [/i] [/s] path1 [path2] [path3] ...\n\n" +
                            $"/i - Process ISO files\n" +
                            $"/s - Process signed files\n" +
                            $"/f - Try to parse dates contained in the file name\n" +
                            $"/x - Ignore file extensions, try to process all files anyway\n" +
                            $"/r - Process folders recursively\n" +
                            $"/q - Do not print all files that were touched\n" +
                            $"/? - Shows list of commands\n\n" +
                            $"Default options: /i /s\n" +
                            $"The file name parser is looking for various formats in the order yyyyMMdd[HHmm[ss]]");
                        return;
                    }
                }
                else
                {
                    if (!scanIso && !scanSigned && !scanFileName)
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

            Console.WriteLine($"Done: {filesProcessedCount} file dates modified");

            if (true) //System.Diagnostics.Debugger.IsAttached)
            {
                Console.ReadKey();
            }
        }

        static void ProcessFile(string path)
        {
            string extension = Path.GetExtension(path).ToLower();
            DateTime? creationTime = null;

            if (scanIso && (discImageFormats.Contains(extension) || ignoreFormats))
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

            if (creationTime == null && scanSigned && (signedFilesFormat.Contains(extension) || ignoreFormats))
            {
                creationTime = SignatureManager.GetSignatureDate(path);
            }

            if (creationTime == null && scanFileName)
            {
                creationTime = ParseFileNameDate(Path.GetFileNameWithoutExtension(path));
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

                    filesProcessedCount++;

                    if (!quiet)
                        Console.WriteLine(Path.GetFileName(path) + " -> " + ((DateTime)creationTime).ToString());
                }
                catch (Exception) when (!System.Diagnostics.Debugger.IsAttached) { }
            }
        }

        static DateTime? ParseFileNameDate (string fileNameWoEx)
        {
            Match m = Regex.Match(fileNameWoEx, @"(\d\d\d\d)\D?(\d\d)\D?(\d\d)\D?(\d\d)?\D?(\d\d)?\D?(\d\d)?");
            if (m.Success && (m.Groups[0].Value.StartsWith("19") || m.Groups[0].Value.StartsWith("20") || m.Groups[0].Value.StartsWith("21")))
            {
                if (!string.IsNullOrEmpty(m.Groups[6].Value))
                    return new DateTime(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value),
                                        int.Parse(m.Groups[4].Value), int.Parse(m.Groups[5].Value), int.Parse(m.Groups[6].Value));

                if (!string.IsNullOrEmpty(m.Groups[5].Value))
                    return new DateTime(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value),
                                        int.Parse(m.Groups[4].Value), int.Parse(m.Groups[5].Value), 0);

                if (!string.IsNullOrEmpty(m.Groups[3].Value))
                    return new DateTime(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
            }
            return null;
        }

        static void ProcessDirectory(string path)
        {
            foreach (var file in Directory.GetFiles(path))
            {
                ProcessFile(file);
            }
            if (recursive)
            {
                foreach (var folder in Directory.GetDirectories(path))
                {
                    ProcessDirectory(folder);
                }
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

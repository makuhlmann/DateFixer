using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DiscUtils.Iso9660;
using DiscUtils.Udf;
using DiscUtils.Vfs;

namespace DateFixer {
    internal class Program {
        static readonly string[] discImageFormats = {
            ".iso",
            ".img"
        };

        static readonly string[] signedFilesFormat = {
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

        static readonly DateTime isoMinDate = new DateTime(1985, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        static int filesProcessedCount = 0;

        static bool scanIso = false;
        static bool scanSigned = false;
        static bool processedAtLeastOne = false;
        static bool scanFileName = false;
        static bool quiet = false;
        static bool ignoreFormats = false;
        static bool recursive = false;

        static void Main(string[] args) {
            foreach (var path in args) {
                if (path.StartsWith("/") && path.Length == 2) {
                    switch (path.Substring(1,1)) {
                        case "i":
                            scanIso = true;
                            break;
                        case "s":
                            scanSigned = true;
                            break;
                        case "f":
                            scanFileName = true;
                            break;
                        case "q":
                            quiet = true;
                            break;
                        case "x":
                            ignoreFormats = true;
                            break;
                        case "r":
                            recursive = true;
                            break;
                        case "?":
                            Console.WriteLine("DateFixer Usage: datefixer [/i] [/s] path1 [path2] [path3] ...\n\n" +
                            "/i - Process image files\n" +
                            "/s - Process signed files\n" +
                            "/f - Try to parse dates contained in the file name\n" +
                            "/x - Ignore file extensions, try to process all files anyway\n" +
                            "/r - Process folders recursively\n" +
                            "/q - Do not print all files that were touched\n" +
                            "/? - Shows list of commands\n\n" +
                            "Default options: /i /s\n" +
                            "The file name parser is looking for various formats in the order yyyyMMdd[HHmm[ss]]");
                            return;
                        default:
                            Console.WriteLine($"Unknown option {path}");
                            return;
                    }
                } else {
                    // Set defaults /i /s
                    if (!scanIso && !scanSigned && !scanFileName)
                        scanIso = scanSigned = true;

                    // If at least one path was processed here, no warning will be shown
                    processedAtLeastOne = true;

                    if (File.Exists(path)) {
                        ProcessFile(path);
                    } else if (Directory.Exists(path)) {
                        ProcessDirectory(path);
                    } else {
                        Console.WriteLine("Invalid path: " + path);
                    }
                }
            }

            if (!processedAtLeastOne) {
                Console.WriteLine("No valid path given as a parameter\n" +
                                  "See /? for more information");
            }

            Console.WriteLine($"Done: {filesProcessedCount} file dates modified");

            if (System.Diagnostics.Debugger.IsAttached) {
                Console.ReadKey();
            }
        }

        static void ProcessFile(string path) {
            string extension = Path.GetExtension(path).ToLower();
            DateTime? creationTime = null;

            if (scanIso && (discImageFormats.Contains(extension) || ignoreFormats)) {
                var fs = File.OpenRead(path);

                // ISO 9660
                try {
                    var reader = new CDReader(fs, true);
                    creationTime = reader.Root.CreationTimeUtc;

                    if (creationTime == null || creationTime < isoMinDate)
                        creationTime = DeepScanIso(reader);

                    reader.Dispose();
                } catch (Exception) when (!System.Diagnostics.Debugger.IsAttached) { }

                // UDF
                if (creationTime == null || creationTime < isoMinDate) {
                    try {
                        var reader = new UdfReader(fs);
                        creationTime = reader.Root.CreationTimeUtc;

                        if (creationTime == null || creationTime < isoMinDate)
                            creationTime = DeepScanIso(reader);

                        reader.Dispose();
                    } catch (Exception) when (!System.Diagnostics.Debugger.IsAttached) { }
                }

                fs.Close();
            }

            if (creationTime == null && scanSigned && (signedFilesFormat.Contains(extension) || ignoreFormats))
                creationTime = SignatureManager.GetSignatureDate(path);

            if (creationTime == null && scanFileName)
                creationTime = ParseFileNameDate(Path.GetFileNameWithoutExtension(path));

            if (creationTime != null && creationTime != DateTime.MinValue) {
                try {
                    var attributes = File.GetAttributes(path);
                    File.SetAttributes(path, FileAttributes.Normal);

                    File.SetLastWriteTimeUtc(path, (DateTime)creationTime);
                    File.SetCreationTimeUtc(path, (DateTime)creationTime);

                    File.SetAttributes(path, attributes);

                    filesProcessedCount++;

                    if (!quiet)
                        Console.WriteLine(Path.GetFileName(path) + " -> " + ((DateTime)creationTime).ToString());
                } catch (Exception) when (!System.Diagnostics.Debugger.IsAttached) { }
            }
        }

        static DateTime? ParseFileNameDate(string fileNameWoEx) {
            Match m = Regex.Match(fileNameWoEx, @"(\d\d\d\d)\D?(\d\d)\D?(\d\d)\D?(\d\d)?\D?(\d\d)?\D?(\d\d)?");
            if (m.Success && (m.Groups[0].Value.StartsWith("19") || m.Groups[0].Value.StartsWith("20") || m.Groups[0].Value.StartsWith("21"))) {
                if (!string.IsNullOrEmpty(m.Groups[6].Value)) {
                    return new DateTime(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value),
                                        int.Parse(m.Groups[4].Value), int.Parse(m.Groups[5].Value), int.Parse(m.Groups[6].Value));
                }

                if (!string.IsNullOrEmpty(m.Groups[5].Value)) {
                    return new DateTime(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value),
                                        int.Parse(m.Groups[4].Value), int.Parse(m.Groups[5].Value), 0);
                }

                if (!string.IsNullOrEmpty(m.Groups[3].Value)) {
                    return new DateTime(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
                }
            }
            return null;
        }

        static void ProcessDirectory(string path) {
            foreach (var file in Directory.GetFiles(path)) {
                ProcessFile(file);
            }
            if (recursive) {
                foreach (var folder in Directory.GetDirectories(path)) {
                    ProcessDirectory(folder);
                }
            }
        }

        // Deep scan is done when the date on the ISO is invalid (< 1985)
        // This will look for files within the ISO to determine the most likely creation date

        static DateTime? DeepScanIso(VfsFileSystemFacade reader) {
            DateTime result = DateTime.MinValue;
            foreach (var item in reader.GetFileSystemEntries("")) {
                DateTime itemDate = reader.GetFileInfo(item).CreationTimeUtc;
                if (itemDate > result)
                    result = itemDate;
            }
            if (result > isoMinDate)
                return result;
            else
                return null;
        }
    }
}

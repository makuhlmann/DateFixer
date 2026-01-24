using DiscUtils.Iso9660;
using DiscUtils.Udf;
using DiscUtils.Vfs;
using SevenZipExtractor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

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

        static List<string> processedFiles = new List<string>();

        static readonly DateTime isoMinDate = new DateTime(1985, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        static int filesProcessedCount = 0;

        static bool scanIso = false;
        static bool scanSigned = false;
        static bool scanContent = false;
        static bool processedAtLeastOne = false;
        static bool scanFileName = false;
        static bool quiet = false;
        static bool ignoreFormats = false;
        static bool dateRelated = false;
        static bool recursive = false;
        static bool dateFolders = false;

        static bool errorsHappened = false;

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
                        case "c":
                            scanContent = true;
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
                        case "l":
                            dateRelated = true;
                            break;
                        case "r":
                            recursive = true;
                            break;
                        case "d":
                            recursive = true;
                            dateFolders = true;
                            break;
                        case "?":
                            Console.WriteLine("DateFixer Usage: datefixer [/i] [/s] path1 [path2] [path3] ...\n\n" +
                            "/i - Process image files\n" +
                            "/s - Process signed files\n" +
                            "/c - Process archive content\n" +
                            "/f - Try to parse dates contained in the file name\n" +
                            "/x - Ignore file extensions, try to process all files anyway\n" +
                            "/l - Give files with the same name but a different extension the same date\n" +
                            "/r - Process folders recursively\n" +
                            "/d - Give folders the same date as the newest file contained within (implies /r)\n" +
                            "/q - Do not print all files that were touched\n" +
                            "/? - Shows list of commands\n\n" +
                            "Default options: /i /s /c\n" +
                            "The file name parser is looking for various formats in the order yyyyMMdd[HHmm[ss]]");
                            return;
                        default:
                            Console.WriteLine($"Unknown option {path}");
                            return;
                    }
                } else {
                    // Set defaults /i /s /c
                    if (!scanIso && !scanSigned && !scanFileName && !scanContent)
                        scanIso = scanSigned = scanContent = true;

                    // If at least one path was processed here, no warning will be shown
                    processedAtLeastOne = true;

                    if (File.Exists(path)) {
                        ProcessFile(path, new string[] { });
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

            if (errorsHappened || System.Diagnostics.Debugger.IsAttached) {
                Console.WriteLine(" == Files processed with errors, press any key to exit ==");
                Console.ReadKey();
            }
        }

        static DateTime? ProcessFile(string path, string[] otherFiles) {
            string extension = Path.GetExtension(path).ToLower();
            DateTime? creationTime = null;

            if (scanIso && (discImageFormats.Contains(extension) || ignoreFormats)) {
                FileStream fs;

                try {
                    fs = File.OpenRead(path);
                } catch (Exception ex) {
                    Console.WriteLine($"Error opening {path} - {ex.Message}");
                    return null;
                }

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

            if (creationTime == null && scanContent) {
                creationTime = ProcessArchive(path);
            }

            if (creationTime == null && scanFileName)
                creationTime = ParseFileNameDate(Path.GetFileNameWithoutExtension(path));

            if (creationTime != null && creationTime != DateTime.MinValue) {
                try {
                    var attributes = File.GetAttributes(path);
                    File.SetAttributes(path, FileAttributes.Normal);

                    File.SetLastWriteTimeUtc(path, (DateTime)creationTime);
                    //File.SetCreationTimeUtc(path, (DateTime)creationTime);

                    File.SetAttributes(path, attributes);

                    filesProcessedCount++;

                    processedFiles.Add(path);

                    if (!quiet)
                        Console.WriteLine(Path.GetFileName(path) + " -> " + ((DateTime)creationTime).ToString());

                    if (dateRelated) {
                        var files = otherFiles.Where(s => s != path && string.Equals(Path.GetFileNameWithoutExtension(s), Path.GetFileNameWithoutExtension(path), StringComparison.OrdinalIgnoreCase) && !processedFiles.Contains(s));
                        foreach (var file in files) {
                            attributes = File.GetAttributes(file);
                            File.SetAttributes(file, FileAttributes.Normal);

                            File.SetLastWriteTimeUtc(file, (DateTime)creationTime);
                            //File.SetCreationTimeUtc(file, (DateTime)creationTime);

                            File.SetAttributes(file, attributes);

                            filesProcessedCount++;

                            processedFiles.Add(file);

                            if (!quiet)
                                Console.WriteLine(Path.GetFileName(file) + " +> " + ((DateTime)creationTime).ToString());
                        }
                    }
                } catch (Exception) when (!System.Diagnostics.Debugger.IsAttached) { }
                return creationTime;
            }

            return File.GetLastWriteTime(path);
        }

        static DateTime? ParseFileNameDate(string fileNameWoEx) {
            Match m = Regex.Match(fileNameWoEx, @"(\d\d\d\d)\D?(\d\d)\D?(\d\d)\D?(\d\d)?\D?(\d\d)?\D?(\d\d)?");
            if (m.Success && (m.Groups[0].Value.StartsWith("19") || m.Groups[0].Value.StartsWith("20") || m.Groups[0].Value.StartsWith("21"))) {
                try {
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
                } catch (FormatException) {
                    return null;
                }
            }
            return null;
        }

        static DateTime? ProcessDirectory(string path) {
            string[] files = Directory.GetFiles(path);
            DateTime? creationTime = DateTime.MinValue;
            foreach (var file in files) {
                DateTime? res = ProcessFile(file, files);
                // If file was not processed, take the currently assigned date
                res = File.GetLastWriteTimeUtc(file);
                if (res != null && res > creationTime)
                    creationTime = res;
            }

            // Clear processed files when changing folder
            processedFiles = new List<string>();

            if (recursive) {
                foreach (var folder in Directory.GetDirectories(path)) {
                    DateTime? res = ProcessDirectory(folder);
                    // If folder was not processed, take the currently assigned date
                    res = File.GetLastWriteTimeUtc(folder);
                    if (res != null && res > creationTime)
                        creationTime = res;
                }
            }

            if (dateFolders && (creationTime != null && creationTime != DateTime.MinValue)) {
                try {
                    var attributes = File.GetAttributes(path);
                    File.SetAttributes(path, FileAttributes.Directory);

                    Directory.SetLastWriteTimeUtc(path, (DateTime)creationTime);
                    //Directory.SetCreationTimeUtc(path, (DateTime)creationTime);

                    File.SetAttributes(path, attributes);

                    if (!quiet)
                        Console.WriteLine(Path.GetFileName(path) + " D> " + ((DateTime)creationTime).ToString());
                } catch (Exception)when(!System.Diagnostics.Debugger.IsAttached) { }
                return creationTime;
            }

            return null;
        }

        static DateTime? ProcessArchive(string path, int? format = null) {
            DateTime? creationTime = null;
            try {
                using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    ArchiveFile archive;
                    archive = new ArchiveFile(fileStream, format: (SevenZipFormat?)format);

                    foreach (var entry in archive.Entries) {
                        if (!entry.IsFolder && entry.LastWriteTime.Year > 1980 && (creationTime == null || creationTime < entry.LastWriteTime)) {
                            creationTime = entry.LastWriteTime;
                        }
                    }
                }
            } catch (SevenZipException) {
                if (format == null) {
                    // It's bruteforce time!
                    for (int i = 1; i <= 47; i++) {
                        if (i == 33)
                            return ProcessArchiveDirect(path); // Unsupported format
                        creationTime = ProcessArchive(path, i);
                        if (creationTime != null)
                            break;
                    }
                }
            }
            return creationTime;
        }

        static DateTime? ScanFileDatesRecursive(string path) {
            DateTime? creationTime = DateTime.MinValue;
            foreach (var file in Directory.GetFiles(path)) {
                DateTime? res = File.GetLastWriteTimeUtc(file);
                if (res != null && res > creationTime)
                    creationTime = res;
            }
            foreach (var folder in Directory.GetDirectories(path)) {
                DateTime? res = ScanFileDatesRecursive(folder);
                if (res != null && res > creationTime)
                    creationTime = res;
            }
            return creationTime;
        }

        static DateTime? ProcessArchiveDirect(string path) {
            string sevenZipPath = Util.Get7zPath();
            if (sevenZipPath == null) {
                return null;
            }

            string tempPath = Path.Combine(Path.GetTempPath(), "DateFixerTemp");

            if (Directory.Exists(tempPath)) {
                Util.ForceDeleteDirectory(tempPath);
            }

            Directory.CreateDirectory(tempPath);

            string commandArgs = $"x \"{path}\" -o" + tempPath;
            Process.Start(sevenZipPath, commandArgs).WaitForExit();

            DateTime currentTime = DateTime.Now;

            DateTime? processDirectoryTime = ScanFileDatesRecursive(tempPath);

            Util.ForceDeleteDirectory(tempPath);

            if (processDirectoryTime != null && processDirectoryTime > currentTime) {
                return null;
            }

            return processDirectoryTime;
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DateFixer {
    public static class Util {
        public static void ForceDeleteDirectory(string path) {
            var directory = new DirectoryInfo(path) { Attributes = FileAttributes.Normal };

            foreach (var info in directory.GetFileSystemInfos("*", SearchOption.AllDirectories)) {
                info.Attributes = FileAttributes.Normal;
            }

            directory.Delete(true);
        }

        public static string Get7zPath() {
            string currentPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase).Replace("file:\\", "");
            if (File.Exists(Path.Combine(currentPath, "7z.exe")))
                return Path.Combine(currentPath, "7z.exe");
            if (File.Exists("7z.exe"))
                return "7z.exe";
            if (File.Exists("C:\\Program Files\\7-Zip\\7z.exe"))
                return "C:\\Program Files\\7-Zip\\7z.exe";
            if (File.Exists("C:\\Program Files (x86)\\7-Zip\\7z.exe"))
                return "C:\\Program Files (x86)\\7-Zip\\7z.exe";
            return null;
        }
    }
}

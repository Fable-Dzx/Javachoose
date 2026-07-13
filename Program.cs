using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.Win32;

namespace JavaChoose
{
    public class JavaInstallation
    {
        public string VersionName { get; set; }
        public string InstallPath { get; set; }
        public int MajorVersion { get; set; }
        public bool IsJre { get; set; }
        public string FormattedNumber => IsJre ? $"00{MajorVersion}" : MajorVersion.ToString();
    }

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                ShowUsage();
                return;
            }

            string option = args[0];
            switch (option)
            {
                case "-F":
                    ListJavaInstallations();
                    break;
                case "-f":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("错误：-f 选项需要指定版本号。");
                        ShowUsage();
                        return;
                    }
                    SetJavaPath(args[1]);
                    break;
                case "-i":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("错误：-i 选项需要指定要导入的 Java 安装路径。");
                        ShowUsage();
                        return;
                    }
                    ImportJava(args[1]);
                    break;
                case "-r":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("错误：-r 选项需要指定要移除的版本名（如 jdk-17）。");
                        ShowUsage();
                        return;
                    }
                    RemoveImported(args[1]);
                    break;
                default:
                    Console.WriteLine($"未知选项：{option}");
                    ShowUsage();
                    break;
            }
        }

        static void ShowUsage()
        {
            Console.WriteLine("用法：");
            Console.WriteLine("  javachoose -F                    列出所有 Java 安装");
            Console.WriteLine("  javachoose -f [number]          将指定版本的 bin 目录添加到系统 PATH 最前面（需管理员权限）");
            Console.WriteLine("  javachoose -i [path]            导入指定路径的 Java 到用户注册表");
            Console.WriteLine("  javachoose -r [versionName]     移除已导入的版本（如 jdk-17）");
            Console.WriteLine("示例：");
            Console.WriteLine("  javachoose -i D:\\jdk-17");
            Console.WriteLine("  javachoose -r jdk-17");
            Console.WriteLine("注意：-f 操作需要以管理员身份运行才能修改系统 PATH 和 JAVA_HOME。");
        }

        // ========== 导入与移除 ==========

        static void ImportJava(string path)
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"错误：路径不存在：{path}");
                return;
            }

            string binDir = Path.Combine(path, "bin");
            if (!Directory.Exists(binDir))
            {
                Console.WriteLine($"错误：找不到 bin 目录：{binDir}");
                return;
            }

            string javaExe = Path.Combine(binDir, "java.exe");
            string javacExe = Path.Combine(binDir, "javac.exe");
            bool hasJava = File.Exists(javaExe);
            bool hasJavac = File.Exists(javacExe);

            if (!hasJava)
            {
                Console.WriteLine($"错误：在 {binDir} 中找不到 java.exe，这不是有效的 Java 安装。");
                return;
            }

            bool isJre = !hasJavac;
            string dirName = new DirectoryInfo(path).Name;
            if (!TryParseVersionFromPath(dirName, out int majorVersion))
            {
                Console.WriteLine($"警告：无法从目录名 '{dirName}' 解析主版本号，请重命名目录为如 'jdk-17' 格式。");
                return;
            }

            string versionName = isJre ? $"jre-{majorVersion}" : $"jdk-{majorVersion}";
            string regPath = @"Software\JavaChoose\Imported";

            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(regPath, true))
                {
                    if (key == null)
                    {
                        Console.WriteLine("错误：无法创建注册表项，请检查权限。");
                        return;
                    }
                    key.SetValue(versionName, path);
                }
                Console.WriteLine($"已成功导入：{versionName} -> {path}");

                Console.Write("是否立即切换到该版本? (y/n): ");
                string response = Console.ReadLine()?.Trim().ToLower();
                if (response == "y" || response == "yes")
                {
                    SetJavaPath(majorVersion.ToString());
                }
                else
                {
                    Console.WriteLine("现在运行 javachoose -F 即可看到此版本。");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"写入注册表失败：{ex.Message}");
            }
        }

        static void RemoveImported(string versionName)
        {
            string regPath = @"Software\JavaChoose\Imported";
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(regPath, true))
                {
                    if (key == null)
                    {
                        Console.WriteLine("未发现任何已导入的版本。");
                        return;
                    }
                    if (key.GetValue(versionName) == null)
                    {
                        Console.WriteLine($"未找到已导入的版本：{versionName}");
                        return;
                    }
                    key.DeleteValue(versionName);
                    Console.WriteLine($"已移除：{versionName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"操作失败：{ex.Message}");
            }
        }

        // ========== 查找所有 Java ==========

        static List<JavaInstallation> FindJavaInstallations()
        {
            var list = new List<JavaInstallation>();

            list.AddRange(FindFromRegistry());
            list.AddRange(ScanDirectories());
            list.AddRange(FindImported());

            string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome) && Directory.Exists(javaHome))
            {
                var item = CreateFromPath(javaHome);
                if (item != null) list.Add(item);
            }

            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                var paths = pathEnv.Split(';');
                foreach (var p in paths)
                {
                    string trimmedP = p.Trim();
                    if (string.IsNullOrEmpty(trimmedP)) continue;

                    if (trimmedP.IndexOf("system32", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (trimmedP.IndexOf("windows", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                    string javaExe = Path.Combine(trimmedP, "java.exe");
                    if (File.Exists(javaExe))
                    {
                        string installRoot = Directory.GetParent(trimmedP)?.FullName;
                        if (!string.IsNullOrEmpty(installRoot) && Directory.Exists(installRoot))
                        {
                            var item = CreateFromPath(installRoot);
                            if (item != null) list.Add(item);
                        }
                    }
                }
            }

            // 去重前规范化路径（消除尾部反斜杠差异）
            list = list.GroupBy(x => NormalizePath(x.InstallPath))
                       .Select(g => g.First())
                       .ToList();

            list = list.OrderBy(x => x.MajorVersion).ThenBy(x => x.IsJre).ToList();
            return list;
        }

        // 路径规范化辅助
        static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            try
            {
                return Path.GetFullPath(path).TrimEnd('\\');
            }
            catch
            {
                return path.TrimEnd('\\');
            }
        }

        static List<JavaInstallation> FindFromRegistry()
        {
            var list = new List<JavaInstallation>();
            string[] registryRoots = {
                @"SOFTWARE\JavaSoft",
                @"SOFTWARE\WOW6432Node\JavaSoft"
            };

            foreach (var root in registryRoots)
            {
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey($@"{root}\Java Development Kit"))
                    {
                        if (key != null)
                        {
                            foreach (var subKeyName in key.GetSubKeyNames())
                            {
                                using (var subKey = key.OpenSubKey(subKeyName))
                                {
                                    if (subKey == null) continue;
                                    var installPath = subKey.GetValue("JavaHome")?.ToString();
                                    if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath)) continue;
                                    if (TryParseVersion(subKeyName, out int ver))
                                    {
                                        list.Add(new JavaInstallation
                                        {
                                            VersionName = $"jdk-{ver}",
                                            InstallPath = installPath,
                                            MajorVersion = ver,
                                            IsJre = false
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                catch (System.Security.SecurityException) { }
                catch (UnauthorizedAccessException) { }

                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey($@"{root}\Java Runtime Environment"))
                    {
                        if (key != null)
                        {
                            foreach (var subKeyName in key.GetSubKeyNames())
                            {
                                using (var subKey = key.OpenSubKey(subKeyName))
                                {
                                    if (subKey == null) continue;
                                    var installPath = subKey.GetValue("JavaHome")?.ToString();
                                    if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath)) continue;
                                    if (TryParseVersion(subKeyName, out int ver))
                                    {
                                        list.Add(new JavaInstallation
                                        {
                                            VersionName = $"jre-{ver}",
                                            InstallPath = installPath,
                                            MajorVersion = ver,
                                            IsJre = true
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                catch (System.Security.SecurityException) { }
                catch (UnauthorizedAccessException) { }
            }
            return list;
        }

        static List<JavaInstallation> ScanDirectories()
        {
            var list = new List<JavaInstallation>();
            var dirsToScan = new List<string>
            {
                @"C:\Program Files\Java",
                @"C:\Program Files (x86)\Java",
                @"C:\Java",
                @"D:\Java",
                @"E:\Java",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Java"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Java"),
                @"C:\Program Files\AdoptOpenJDK",
                @"C:\Program Files\Eclipse Adoptium",
                @"C:\Program Files\Amazon Corretto",
                @"C:\Program Files\Microsoft\jdk",
                @"C:\Program Files\BellSoft\Liberica",
                @"C:\Program Files\GraalVM",
            };

            string extra = Environment.GetEnvironmentVariable("JAVACHOOSE_EXTRA_PATHS");
            if (!string.IsNullOrEmpty(extra))
            {
                foreach (var p in extra.Split(';'))
                {
                    string trimmed = p.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        dirsToScan.Add(trimmed);
                }
            }

            foreach (var baseDir in dirsToScan)
            {
                if (!Directory.Exists(baseDir)) continue;
                foreach (var dir in Directory.GetDirectories(baseDir))
                {
                    var item = CreateFromPath(dir);
                    if (item != null) list.Add(item);
                }
            }
            return list;
        }

        static List<JavaInstallation> FindImported()
        {
            var list = new List<JavaInstallation>();
            string regPath = @"Software\JavaChoose\Imported";
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(regPath))
                {
                    if (key == null) return list;
                    foreach (var valueName in key.GetValueNames())
                    {
                        string path = key.GetValue(valueName)?.ToString();
                        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) continue;
                        if (TryParseVersionFromPath(valueName, out int ver))
                        {
                            bool isJre = valueName.StartsWith("jre-", StringComparison.OrdinalIgnoreCase);
                            list.Add(new JavaInstallation
                            {
                                VersionName = valueName,
                                InstallPath = path,
                                MajorVersion = ver,
                                IsJre = isJre
                            });
                        }
                        else
                        {
                            var item = CreateFromPath(path);
                            if (item != null) list.Add(item);
                        }
                    }
                }
            }
            catch (Exception) { }
            return list;
        }

        static JavaInstallation CreateFromPath(string installPath)
        {
            if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
                return null;

            string binDir = Path.Combine(installPath, "bin");
            if (!Directory.Exists(binDir)) return null;

            string dirName = new DirectoryInfo(installPath).Name;
            bool isJre = dirName.IndexOf("jre", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isJdk = dirName.IndexOf("jdk", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isJre && !isJdk)
            {
                bool hasJava = File.Exists(Path.Combine(binDir, "java.exe"));
                bool hasJavac = File.Exists(Path.Combine(binDir, "javac.exe"));
                if (hasJava && hasJavac)
                    isJdk = true;
                else if (hasJava && !hasJavac)
                    isJre = true;
                else
                    return null;
            }

            if (!TryParseVersionFromPath(dirName, out int version))
                return null;

            string versionName = isJdk ? $"jdk-{version}" : $"jre-{version}";
            return new JavaInstallation
            {
                VersionName = versionName,
                InstallPath = installPath,
                MajorVersion = version,
                IsJre = isJre
            };
        }

        // ========== 版本解析 ==========

        static bool TryParseVersionFromPath(string folderName, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(folderName)) return false;

            var match = System.Text.RegularExpressions.Regex.Match(folderName, @"\d+");
            if (match.Success && int.TryParse(match.Value, out version))
            {
                return true;
            }

            string cleaned = folderName.ToLower()
                .Replace("jdk-", "").Replace("jre-", "")
                .Replace("openjdk-", "").Replace("corretto-", "")
                .Replace("adoptopenjdk-", "").Replace("temurin-", "");
            if (int.TryParse(cleaned, out version))
                return true;

            return false;
        }

        static bool TryParseVersion(string subKeyName, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(subKeyName)) return false;

            if (subKeyName.StartsWith("1."))
            {
                var parts = subKeyName.Split('.');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int v))
                {
                    version = v;
                    return true;
                }
            }
            else
            {
                if (int.TryParse(subKeyName, out int v))
                {
                    version = v;
                    return true;
                }
                int dotIndex = subKeyName.IndexOf('.');
                if (dotIndex > 0)
                {
                    var firstPart = subKeyName.Substring(0, dotIndex);
                    if (int.TryParse(firstPart, out v))
                    {
                        version = v;
                        return true;
                    }
                }
            }
            return false;
        }

        static void ListJavaInstallations()
        {
            var list = FindJavaInstallations();
            if (list.Count == 0)
            {
                Console.WriteLine("未找到任何 Java 安装。");
                return;
            }

            int maxVersion = list.Max(x => x.VersionName.Length);
            int maxPath = list.Max(x => x.InstallPath.Length);
            int maxNumber = list.Max(x => x.FormattedNumber.Length);

            string header = $"{"version".PadRight(maxVersion)}    {"place".PadRight(maxPath)}    {"number"}";
            Console.WriteLine(header);
            Console.WriteLine(new string('-', header.Length));

            foreach (var item in list)
            {
                Console.WriteLine($"{item.VersionName.PadRight(maxVersion)}    {item.InstallPath.PadRight(maxPath)}    {item.FormattedNumber}");
            }
        }

        // ========== 核心切换功能：修改系统 PATH 和 JAVA_HOME ==========

        static void SetJavaPath(string numberArg)
        {
            if (!int.TryParse(numberArg, out int targetVersion))
            {
                Console.WriteLine("错误：无效的版本号格式。");
                return;
            }

            var all = FindJavaInstallations();
            bool exactJre = numberArg.StartsWith("00") && numberArg.Length > 2;

            IEnumerable<JavaInstallation> candidates;
            if (exactJre)
            {
                candidates = all.Where(x => x.IsJre && x.MajorVersion == targetVersion).ToList();
            }
            else
            {
                var matched = all.Where(x => x.MajorVersion == targetVersion).ToList();
                if (matched.Count == 0)
                {
                    Console.WriteLine($"未找到版本号为 {targetVersion} 的 Java 安装。");
                    return;
                }
                var jdk = matched.FirstOrDefault(x => !x.IsJre);
                if (jdk != null)
                    candidates = new List<JavaInstallation> { jdk };
                else
                    candidates = new List<JavaInstallation> { matched.First() };
            }

            if (!candidates.Any())
            {
                Console.WriteLine($"未找到匹配的 JRE（版本 {targetVersion}）。");
                return;
            }

            var selected = candidates.First();
            string binPath = Path.Combine(selected.InstallPath, "bin");
            if (!Directory.Exists(binPath))
            {
                Console.WriteLine($"错误：找不到 bin 目录：{binPath}");
                return;
            }

            // ---- 修改系统 PATH ----
            string systemPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
            var pathList = systemPath.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            // 移除已存在的相同路径（忽略大小写）
            pathList.RemoveAll(p => string.Equals(p.Trim(), binPath, StringComparison.OrdinalIgnoreCase));

            // 插入到最前面
            pathList.Insert(0, binPath);

            string newPath = string.Join(";", pathList);

            // ---- 修改系统 JAVA_HOME ----
            string javaHome = selected.InstallPath;

            try
            {
                // 写入系统 PATH
                Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.Machine);
                // 写入系统 JAVA_HOME
                Environment.SetEnvironmentVariable("JAVA_HOME", javaHome, EnvironmentVariableTarget.Machine);

                Console.WriteLine($"已成功切换到：{selected.VersionName}");
                Console.WriteLine($"  bin 目录：{binPath} 已置于系统 PATH 最前面");
                Console.WriteLine($"  JAVA_HOME 已设置为：{javaHome}");
                Console.WriteLine("注意：新打开的命令行窗口才会生效。");
            }
            catch (System.Security.SecurityException)
            {
                Console.WriteLine("错误：修改系统环境变量需要管理员权限。请以管理员身份重新运行此命令。");
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("错误：权限不足，无法修改系统环境变量。请以管理员身份运行。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"操作失败：{ex.Message}");
            }
        }
    }
}

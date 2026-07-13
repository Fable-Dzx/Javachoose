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

        // JRE 版本号前加 "00"，JDK 直接显示数字
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

            // 大小写敏感匹配：-F 列出，-f 设置 PATH
            if (option == "-F")
            {
                ListJavaInstallations();
            }
            else if (option == "-f")
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("错误：-f 选项需要指定版本号。");
                    ShowUsage();
                    return;
                }
                string numberArg = args[1];
                SetJavaPath(numberArg);
            }
            else
            {
                Console.WriteLine($"未知选项：{option}");
                ShowUsage();
            }
        }

        static void ShowUsage()
        {
            Console.WriteLine("用法：");
            Console.WriteLine("  javachoose -F                   列出所有 Java 安装（区分大小写）");
            Console.WriteLine("  javachoose -f [number]         将指定版本的 bin 目录添加到 PATH 最前面（区分大小写）");
            Console.WriteLine("示例：");
            Console.WriteLine("  javachoose -f 8                (匹配 JDK-8，若没有则匹配 JRE-8)");
            Console.WriteLine("  javachoose -f 008              (精确匹配 JRE-8，注意前导零)");
            Console.WriteLine("  javachoose -f 17               (匹配 JDK-17)");
            Console.WriteLine("  javachoose -f 0017             (精确匹配 JRE-17)");
        }

        static List<JavaInstallation> FindJavaInstallations()
        {
            var list = new List<JavaInstallation>();
            string[] registryRoots = {
                @"SOFTWARE\JavaSoft",
                @"SOFTWARE\WOW6432Node\JavaSoft"
            };

            foreach (var root in registryRoots)
            {
                // 查找 JDK
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
                                    if (string.IsNullOrEmpty(installPath)) continue;
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
                catch (System.Security.SecurityException) { /* 忽略权限问题 */ }
                catch (UnauthorizedAccessException) { /* 忽略 */ }

                // 查找 JRE
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
                                    if (string.IsNullOrEmpty(installPath)) continue;
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
                catch (System.Security.SecurityException) { /* 忽略 */ }
                catch (UnauthorizedAccessException) { /* 忽略 */ }
            }

            // 去重
            list = list.GroupBy(x => x.InstallPath).Select(g => g.First()).ToList();
            // 按版本号排序
            list = list.OrderBy(x => x.MajorVersion).ThenBy(x => x.IsJre).ToList();
            return list;
        }

        static bool TryParseVersion(string subKeyName, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(subKeyName)) return false;

            // 处理 1.8.0_331 格式 → 主版本号 8
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
                // 尝试直接解析 "17" 或 "17.0.2" → 17
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

        static void SetJavaPath(string numberArg)
        {
            // 解析数字（支持前导零，如 "008" → 8）
            if (!int.TryParse(numberArg, out int targetVersion))
            {
                Console.WriteLine("错误：无效的版本号格式。");
                return;
            }

            var all = FindJavaInstallations();

            // 若用户输入以 "00" 开头，则精确匹配 JRE
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
                // 优先选择 JDK
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

            // 修改用户环境变量 PATH
            string userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
            var pathList = userPath.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            pathList.RemoveAll(p => string.Equals(p.Trim(), binPath, StringComparison.OrdinalIgnoreCase));
            pathList.Insert(0, binPath);
            string newPath = string.Join(";", pathList);
            Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);

            Console.WriteLine($"已将 {binPath} 添加到用户 PATH 最前面。");
            Console.WriteLine("注意：新打开的命令行窗口才会生效。");
        }
    }
}

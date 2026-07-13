# JavaChoose

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

> 一个轻量级的 Windows Java 版本管理工具，让你轻松切换多个 JDK/JRE 版本。

---

## 📖 简介

`javachoose` 是一个专为 Windows 设计的命令行工具，帮助你：
- **发现**电脑上所有已安装的 Java（包括注册表、常见目录、解压版、便携版）
- **导入**自定义路径的 Java 并持久化记录
- **一键切换**系统默认 Java 版本（修改系统 `PATH` 和 `JAVA_HOME`）

无需手动编辑环境变量，告别繁琐的路径管理。

---

## ✨ 功能特性

- **多源扫描**：自动检测注册表、`C:\Program Files\Java` 等标准目录，以及 `JAVA_HOME` 和系统 `PATH` 中的 Java。
- **手动导入**：支持导入任意位置的 JDK/JRE（如解压版），存储在用户注册表中，重启后依然有效。
- **版本切换**：将指定版本的 `bin` 目录置于系统 `PATH` 最前面，同时更新 `JAVA_HOME`，让所有应用（包括 IDE）立即生效。
- **智能版本解析**：使用正则提取版本号，兼容 `jdk-17.0.1`、`openjdk-11`、`corretto-8` 等多种命名。
- **交互式切换**：导入后可选择立即切换，无需额外命令。
- **简洁美观**：表格化列出所有 Java 版本，清晰直观。

---

## 📦 安装

### 直接下载

1. 访问本仓库的 [Actions](https://github.com/Fable-Dzx/Javachoose/actions) 页面。
2. 选择最新的成功构建，在 **Artifacts** 区域下载 `javachoose-exe.zip`。
3. 解压得到 `javachoose.exe`，将其放入系统 `PATH` 目录（如 `C:\Windows`）或任意你喜欢的目录（并确保该目录在 `PATH` 中）。

### 从源码编译

```bash
git clone https://github.com/Fable-Dzx/Javachoose.git
cd Javachoose
dotnet build -c Release
dotnet publish -c Release -o ./dist

编译产物位于 `./dist/javachoose.exe`。

---

## 🚀 使用方法

### 命令总览

| 命令 | 说明 | 是否需要管理员权限 |
| :--- | :--- | :--- |
| `javachoose -F` | 列出所有可用的 Java 安装 | ❌ 否 |
| `javachoose -f [number]` | 切换到指定版本（修改系统 PATH 和 JAVA_HOME） | ✅ **是** |
| `javachoose -i [path]` | 导入指定路径的 Java | ❌ 否 |
| `javachoose -r [versionName]` | 移除已导入的版本（如 `jdk-17`） | ❌ 否 |

### 详细示例

#### 1. 列出所有 Java

```cmd
javachoose -F
```

输出示例：

```
version    place                                                       number
-----------------------------------------------------------------------------
jdk-8      C:\Program Files\Eclipse Adoptium\jdk-8.0.492.9-hotspot    8
jdk-11     C:\Program Files\Java\jdk-11.0.31+11                       11
jdk-17     C:\Program Files\Java\jdk-17                               17
jdk-21     C:\Program Files\Java\jdk-21                               21
jre-8      C:\Program Files\Java\jre1.8.0_331                         008
```

#### 2. 导入自定义路径的 Java（例如解压版 JDK 17）

```cmd
javachoose -i D:\dev\jdk-17.0.2
```

工具会自动识别版本并写入用户注册表。导入成功后，会询问是否立即切换。

#### 3. 切换到 JDK-17（系统默认）

**必须以管理员身份运行**：

```cmd
javachoose -f 17
```

执行后：
- 系统 `PATH` 中最前面会添加 `D:\dev\jdk-17.0.2\bin`
- 系统 `JAVA_HOME` 会设置为 `D:\dev\jdk-17.0.2`

**注意**：切换后需**新开终端**才能看到效果。

#### 4. 精确切换到 JRE-8

```cmd
javachoose -f 008
```

因为 `number` 列中 JRE 的版本号前有 `00`，输入 `008` 可以精确匹配 JRE-8，而不影响 JDK-8。

#### 5. 移除已导入的版本

```cmd
javachoose -r jdk-17
```

---

## ⚙️ 环境变量配置

### `JAVACHOOSE_EXTRA_PATHS`

如果你有额外的 Java 安装目录（不在默认扫描路径中），可以设置此环境变量（分号分隔）来让工具自动扫描：

```cmd
setx JAVACHOOSE_EXTRA_PATHS "D:\Java;E:\backup\jdk"
```

设置后，`javachoose -F` 会自动包含这些目录下的 Java。

---

## 🛠️ 构建与开发

### 项目结构

```
Javachoose/
├── .github/workflows/build.yml   # GitHub Actions CI
├── Program.cs                    # 主程序
├── javachoose.csproj             # 项目文件
└── README.md                     # 本文档
```

### 本地调试

使用 Visual Studio 或 VS Code 打开项目，或直接使用命令行：

```bash
dotnet run -- -F
```

---

## 📄 许可证

本项目采用 **MIT 许可证**。你可以自由使用、修改、分发，但需保留版权声明。详见 [LICENSE](LICENSE) 文件。

---

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！如果你有好的建议或发现了 Bug，请随时告知。

---

## 🙏 致谢

- 本工具在开发过程中由 **DeepSeek-R1** 辅助完成代码编写与问题修复。
- 感谢所有开源社区提供的灵感与支持。

---

## 📬 联系方式

- 作者：[Fable-Dzx](https://github.com/Fable-Dzx)
- 项目地址：[https://github.com/Fable-Dzx/Javachoose](https://github.com/Fable-Dzx/Javachoose)

---

**Happy Java Switching!** ☕

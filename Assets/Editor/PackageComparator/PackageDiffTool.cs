using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;
using UnityEditor;
using YooAsset;
using YooAsset.Editor;

namespace TEngine.Editor
{
    /// <summary>
    /// 热更资源包差异化打包工具
    /// </summary>
    public static class PackageDiffTool
    {
        
        /// <summary>
        /// 在BuildCurrentPlatformAB方法执行后进行差异化对比和打包
        /// </summary>
        [MenuItem("Tools/Quick Build/热更资源包差异化打包", priority = 92)]
        public static void BuildDiffPackage()
        {
            Debug.Log("开始进行热更资源包差异化打包...");
            
            // 获取资源包的路径
            string outputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
            string platformName = EditorUserBuildSettings.activeBuildTarget.ToString();
            string packageOutputDirectory = $"{outputRoot}/{platformName}/DefaultPackage";
            
            if (!Directory.Exists(packageOutputDirectory))
            {
                Debug.LogError($"输出目录不存在: {packageOutputDirectory}");
                return;
            }
            string currentVersion = GetCurrentVersion();
            Debug.Log($"当前版本: {currentVersion}");
            
            // // 创建版本子目录
            string currentVersionDir = Path.Combine(packageOutputDirectory, currentVersion);
            if (!Directory.Exists(currentVersionDir))
            {
                Directory.CreateDirectory(currentVersionDir);
            }
            
            // 查找当前版本的资源清单文件
            string manifestFileName = $"{currentVersion}/DefaultPackage_{currentVersion}.bytes";
            string manifestFilePath = Path.Combine(packageOutputDirectory, manifestFileName);
            
            if (!File.Exists(manifestFilePath))
            {
                Debug.LogError($"找不到当前版本的资源清单文件: {manifestFilePath}");
                return;
            }
            string manifestDestPath = manifestFilePath;
            
            // 将所有的AB文件移动到版本目录中
            List<string> abFiles = new List<string>();
            foreach (var file in Directory.GetFiles(packageOutputDirectory))
            {
                string fileName = Path.GetFileName(file);
                
                // 跳过清单文件和版本文件
                if (fileName == manifestFileName || 
                    fileName == "DefaultPackage.version" ||
                    fileName == "version.txt")
                    continue;
                
                // 其它文件应该是资源包文件，移动到版本目录中
                string destPath = Path.Combine(currentVersionDir, fileName);
                
                // 如果目标文件夹不存在，创建它
                string destDir = Path.GetDirectoryName(destPath);
                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);
                
                if (File.Exists(destPath))
                    File.Delete(destPath);
                    
                File.Copy(file, destPath);
                abFiles.Add(fileName);
            }
            
            // 查找上一个版本
            string previousVersion = FindPreviousVersion(packageOutputDirectory, currentVersion);
            List<string> diffFiles = new List<string>();
            
            if (!string.IsNullOrEmpty(previousVersion))
            {
                Debug.Log($"发现上一版本: {previousVersion}");
                string previousVersionDir = Path.Combine(packageOutputDirectory, previousVersion);
                
                if (Directory.Exists(previousVersionDir))
                {
                    // 对比两个版本的资源包
                    string previousManifestPath = Path.Combine(previousVersionDir, $"DefaultPackage_{previousVersion}.bytes");
                    diffFiles = CompareVersionPackages(previousManifestPath, manifestDestPath);
                }
                else
                {
                    Debug.LogWarning($"上一版本目录不存在: {previousVersionDir}, 将打包全部文件");
                    diffFiles.AddRange(abFiles);
                }
            }
            else
            {
                Debug.LogWarning("找不到上一版本，将打包全部文件");
                diffFiles.AddRange(abFiles);
            }
            
            // 创建差异包输出目录
            string diffOutputDir = $"{outputRoot}/{platformName}/DiffPackage";
            if (!Directory.Exists(diffOutputDir))
                Directory.CreateDirectory(diffOutputDir);
                
            string diffVersionDir = Path.Combine(diffOutputDir, currentVersion);
            if (Directory.Exists(diffVersionDir))
                Directory.Delete(diffVersionDir, true);
            Directory.CreateDirectory(diffVersionDir);
            
            // 复制版本文件到版本目录 - 先处理这个，确保即使没有差异文件也会有这个文件
            string versionFileName = "DefaultPackage.version";
            string versionFilePath = Path.Combine(packageOutputDirectory, $"{currentVersion}/DefaultPackage.version");
            string versionOutputPath = Path.Combine(diffVersionDir, versionFileName);
            File.Copy(versionFilePath, versionOutputPath, true);
            Debug.Log($"复制版本文件: {versionFileName}");
            
            // 复制清单文件到版本目录
            string manifestDestFileName = $"DefaultPackage_{currentVersion}.bytes";
            string manifestOutputPath = Path.Combine(diffVersionDir, manifestDestFileName);
            File.Copy(manifestDestPath, manifestOutputPath, true);
            Debug.Log($"复制清单文件: {manifestDestFileName}");
            
            // 只有当有差异文件时才创建zip包
            if (diffFiles.Count > 0)
            {
                // 创建用于zip压缩的临时目录
                string tempZipDir = Path.Combine(diffOutputDir, "temp_zip_" + currentVersion);
                if (Directory.Exists(tempZipDir))
                    Directory.Delete(tempZipDir, true);
                Directory.CreateDirectory(tempZipDir);
                
                // 复制差异文件到临时目录
                foreach (string file in diffFiles)
                {
                    string sourcePath = Path.Combine(currentVersionDir, file);
                    string destPath = Path.Combine(tempZipDir, file);
                    
                    // 创建目标文件的目录
                    string destDir = Path.GetDirectoryName(destPath);
                    if (!Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);
                    
                    File.Copy(sourcePath, destPath, true);
                    Debug.Log($"复制差异文件: {file}");
                }
                
                // 创建zip压缩包 - 放在版本目录中
                string zipFileName = $"DiffPackage_{currentVersion}.zip";
                string zipFilePath = Path.Combine(diffVersionDir, zipFileName);
                if (File.Exists(zipFilePath))
                    File.Delete(zipFilePath);
                
                ZipDirectory(tempZipDir, zipFilePath);
                
                // 清理临时目录
                Directory.Delete(tempZipDir, true);
                
                Debug.Log($"创建差异化包成功，包含 {diffFiles.Count} 个差异文件");
            }
            else
            {
                Debug.Log("没有发现差异文件，不创建zip包");
            }
            
            Debug.Log($"热更资源包差异化打包完成! 输出路径: {diffVersionDir}");
            EditorUtility.RevealInFinder(diffVersionDir);
        }
        
        /// <summary>
        /// 获取当前版本号
        /// </summary>
        private static string GetCurrentVersion()
        {
            string currentVersion;
            string versionFilePath = Path.Combine(Directory.GetCurrentDirectory(), "version.txt");
            Debug.Log($"GetCurrentVersion: {versionFilePath}");
            
            // 读取当前版本号
            if (File.Exists(versionFilePath))
            {
                currentVersion = File.ReadAllText(versionFilePath).Trim();
            }
            else
            {
                currentVersion = "1.0.1"; // 初始版本
                File.WriteAllText(versionFilePath, currentVersion);
            }
            
            return currentVersion;
        }
        
        /// <summary>
        /// 查找上一个版本号
        /// </summary>
        private static string FindPreviousVersion(string packageOutputDirectory, string currentVersion)
        {
            string[] versionDirs = Directory.GetDirectories(packageOutputDirectory)
                .Select(Path.GetFileName)
                .Where(dir => dir != currentVersion && IsValidVersion(dir))
                .ToArray();
                
            if (versionDirs.Length == 0)
                return null;
                
            // 按版本号从大到小排序
            Array.Sort(versionDirs, (a, b) => CompareVersions(b, a));
            
            return versionDirs.FirstOrDefault();
        }
        
        /// <summary>
        /// 判断是否为有效版本号
        /// </summary>
        private static bool IsValidVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return false;
                
            string[] parts = version.Split('.');
            if (parts.Length != 3)
                return false;
                
            foreach (string part in parts)
            {
                if (!int.TryParse(part, out _))
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 比较两个版本号
        /// </summary>
        private static int CompareVersions(string versionA, string versionB)
        {
            string[] partsA = versionA.Split('.');
            string[] partsB = versionB.Split('.');
            
            for (int i = 0; i < Math.Min(partsA.Length, partsB.Length); i++)
            {
                int numA = int.Parse(partsA[i]);
                int numB = int.Parse(partsB[i]);
                
                if (numA != numB)
                    return numA.CompareTo(numB);
            }
            
            return partsA.Length.CompareTo(partsB.Length);
        }
        
        /// <summary>
        /// 对比两个版本包的差异
        /// </summary>
        private static List<string> CompareVersionPackages(string previousManifestPath, string currentManifestPath)
        {
            List<string> diffFiles = new List<string>();
            
            try
            {
                // 加载旧版本清单
                byte[] bytesData1 = FileUtility.ReadAllBytes(previousManifestPath);
                PackageManifest manifest1 = ManifestTools.DeserializeFromBinary(bytesData1);
                
                // 加载新版本清单
                byte[] bytesData2 = FileUtility.ReadAllBytes(currentManifestPath);
                PackageManifest manifest2 = ManifestTools.DeserializeFromBinary(bytesData2);
                
                // 查找变化和新增的文件
                foreach (var bundle2 in manifest2.BundleList)
                {
                    if (manifest1.TryGetPackageBundleByBundleName(bundle2.BundleName, out PackageBundle bundle1))
                    {
                        if (bundle2.FileHash != bundle1.FileHash)
                        {
                            diffFiles.Add(bundle2.FileName);
                            Debug.Log($"变化的文件: {bundle2.BundleName}");
                        }
                    }
                    else
                    {
                        diffFiles.Add(bundle2.FileName);
                        Debug.Log($"新增的文件: {bundle2.BundleName}");
                    }
                }
                
                Debug.Log($"发现差异文件数量: {diffFiles.Count}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"比较资源包失败: {ex.Message}");
            }
            
            return diffFiles;
        }
        
        /// <summary>
        /// 压缩文件夹为zip文件
        /// </summary>
        private static void ZipDirectory(string sourceDirectory, string zipFilePath)
        {
            try
            {
                System.IO.Compression.ZipFile.CreateFromDirectory(sourceDirectory, zipFilePath, System.IO.Compression.CompressionLevel.Optimal, false);
                Debug.Log($"创建zip文件成功: {zipFilePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"创建zip文件失败: {ex.Message}");
            }
        }
    }
} 
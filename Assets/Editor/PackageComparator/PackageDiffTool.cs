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
        /// 在YooAsset资源包构建完成后进行热更资源包差异化打包
        /// </summary>
        public static void BuildDiffPackage()
        {
            Debug.Log("开始进行热更资源包差异化打包...");
            string packageOutputDirectory = GetPackageOutputDirectory();
            if (!Directory.Exists(packageOutputDirectory))
            {
                Debug.LogError($"输出目录不存在: {packageOutputDirectory}");
                return;
            }

            string currentVersion = GetCurrentVersion();
            Debug.Log($"当前版本: {currentVersion}");
            string currentVersionDir = Path.Combine(packageOutputDirectory, currentVersion);
            if (!Directory.Exists(currentVersionDir))
            {
                Debug.LogError($"当前版本目录不存在: {currentVersionDir}");
                return;
            }
            
            // 查找当前版本的资源清单文件
            string manifestFileName = $"{currentVersion}/DefaultPackage_{currentVersion}.bytes";
            string manifestFilePath = Path.Combine(packageOutputDirectory, manifestFileName);
            if (!File.Exists(manifestFilePath))
            {
                Debug.LogError($"找不到当前版本的资源清单文件: {manifestFilePath}");
                return;
            }

            //资源包中文件名列表（不包含清单文件和版本文件）
            string curPackageDirectory = Path.Combine(packageOutputDirectory, $"{currentVersion}");
            List<string> abFiles = CopyFileNamesToList(curPackageDirectory, manifestFileName);

            string previousVersion = FindPreviousVersion(packageOutputDirectory, currentVersion);
            Debug.Log($"上一版本: {previousVersion}");

            //获取差异文件列表
            List<string> diffFiles = GetDiffPackageFileName(packageOutputDirectory, previousVersion, manifestFilePath, abFiles);
            
            // 创建差异包输出目录
            string diffOutputDir = GetDiffPackageOutputDirectory();
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
            File.Copy(manifestFilePath, manifestOutputPath, true);
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
        /// 获取YooAsset资源包输出路径
        /// </summary>        
        private static string GetPackageOutputDirectory() 
        {
            string outputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
            string platformName = EditorUserBuildSettings.activeBuildTarget.ToString();
            return $"{outputRoot}/{platformName}/DefaultPackage";
        }

        /// <summary>
        /// 获取热更补丁包输出路径
        /// </summary>        
        private static string GetDiffPackageOutputDirectory() 
        {
            string outputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
            string platformName = EditorUserBuildSettings.activeBuildTarget.ToString();
            return $"{outputRoot}/{platformName}/DiffPackage";
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
                currentVersion = "1"; // 初始版本
                File.WriteAllText(versionFilePath, currentVersion);
            }
            
            return currentVersion;
        }
        
        /// <summary>
        /// 查找上一个版本号
        /// </summary>
        private static List<string> CopyFileNamesToList(string packageOutputDirectory, string manifestFileName)
        {
            // Debug.Log($"CopyFileNamesToList packageOutputDirectory: {packageOutputDirectory}");
            // Debug.Log($"CopyFileNamesToList manifestFileName: {manifestFileName}");
            List<string> abFiles = new List<string>();
            foreach (var file in Directory.GetFiles(packageOutputDirectory))
            {
                string fileName = Path.GetFileName(file);
                
                // 跳过清单文件和版本文件
                if (fileName == manifestFileName || 
                    fileName == "DefaultPackage.version" ||
                    fileName == "version.txt")
                    continue;
                // Debug.Log($"CopyFileNamesToList abFiles: {fileName}");
                abFiles.Add(fileName);
            }
            return abFiles;
        }

        /// <summary>
        /// 查找上一个版本号
        /// </summary>
        private static string FindPreviousVersion(string packageOutputDirectory, string currentVersion)
        {
            int curVersion = int.Parse(GetCurrentVersion());
            return (curVersion - 1).ToString();
        }

        /// <summary>
        /// 获取版本之间差异文件名
        /// </summary>
        private static List<string> GetDiffPackageFileName(string packageOutputDirectory, string previousVersion, string manifestFilePath, List<string> abFiles)
        {
            //差异文件列表
            List<string> diffFiles = new List<string>();
            if (!string.IsNullOrEmpty(previousVersion) && previousVersion != "0")
            {
                Debug.Log($"发现上一版本: {previousVersion}");
                string previousVersionDir = Path.Combine(packageOutputDirectory, previousVersion);
                
                if (Directory.Exists(previousVersionDir))
                {
                    // 对比两个版本的资源包
                    string previousManifestPath = Path.Combine(previousVersionDir, $"DefaultPackage_{previousVersion}.bytes");
                    diffFiles = CompareVersionPackages(previousManifestPath, manifestFilePath);
                }
                else
                {
                    Debug.LogWarning($"上一版本目录不存在: {previousVersionDir}, 将打包全部文件");
                    // diffFiles.AddRange(abFiles);
                    return abFiles;
                }
            }
            else
            {
                Debug.LogWarning("找不到上一版本，将打包全部文件");
                // diffFiles.AddRange(abFiles);
                return abFiles;
            }
            return diffFiles;
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
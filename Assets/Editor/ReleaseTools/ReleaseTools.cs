using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;
using BuildResult = UnityEditor.Build.Reporting.BuildResult;
using System.IO;

namespace TEngine.Editor
{
    /// <summary>
    /// 打包工具类。
    /// <remarks>通过CommandLineReader可以不前台开启Unity实现静默打包以及CLI工作流，详见CommandLineReader.cs example1</remarks>
    /// </summary>
    public static class ReleaseTools
    {
        private static string versionFilePath = Path.Combine(Directory.GetCurrentDirectory(), "version.txt");
        private static string currentVersion;

        public static void BuildDll()
        {
            string platform = CommandLineReader.GetCustomArgument("platform");
            if (string.IsNullOrEmpty(platform))
            {
                Debug.LogError($"Build Asset Bundle Error！platform is null");
                return;
            }

            BuildTarget target = GetBuildTarget(platform);
            
            BuildDLLCommand.BuildAndCopyDlls(target);
        }

        public static void BuildAssetBundle()
        {
            string outputRoot = CommandLineReader.GetCustomArgument("outputRoot");
            if (string.IsNullOrEmpty(outputRoot))
            {
                Debug.LogError($"Build Asset Bundle Error！outputRoot is null");
                return;
            }

            string packageVersion = CommandLineReader.GetCustomArgument("packageVersion");
            if (string.IsNullOrEmpty(packageVersion))
            {
                Debug.LogError($"Build Asset Bundle Error！packageVersion is null");
                return;
            }

            string platform = CommandLineReader.GetCustomArgument("platform");
            if (string.IsNullOrEmpty(platform))
            {
                Debug.LogError($"Build Asset Bundle Error！platform is null");
                return;
            }

            BuildTarget target = GetBuildTarget(platform);
            BuildInternal(target, outputRoot);
            Debug.LogWarning($"Start BuildPackage BuildTarget:{target} outputPath:{outputRoot}");
        }
        
        // [MenuItem("Tools/Quick Build/一键打包AssetBundle", priority = 89)]
        public static void BuildCurrentPlatformAB()
        {
            //默认先构建DLL
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            BuildDLLCommand.BuildAndCopyDlls(target);
            AssetDatabase.Refresh();

            BuildInternal(target, Application.dataPath + "/../Builds/", packageVersion: GetBuildPackageVersion());
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Quick Build/一键生成增量补丁包", priority = 90)]
        public static void BuildCurrentPlatformABAndDiff()
        {
            BuildCurrentPlatformAB();
            PackageDiffTool.BuildDiffPackage();
        }

        private static BuildTarget GetBuildTarget(string platform)
        {
            BuildTarget target = BuildTarget.NoTarget;
            switch (platform)
            {
                case "Android":
                    target = BuildTarget.Android;
                    break;
                case "IOS":
                    target = BuildTarget.iOS;
                    break;
                case "Windows":
                    target = BuildTarget.StandaloneWindows64;
                    break;
                case "MacOS":
                    target = BuildTarget.StandaloneOSX;
                    break;
                case "Linux":
                    target = BuildTarget.StandaloneLinux64;
                    break;
                case "WebGL":
                    target = BuildTarget.WebGL;
                    break;
                case "Switch":
                    target = BuildTarget.Switch;
                    break;
                case "PS4":
                    target = BuildTarget.PS4;
                    break;
                case "PS5":
                    target = BuildTarget.PS5;
                    break;
            }

            return target;
        }

        private static void BuildInternal(BuildTarget buildTarget, string outputRoot, string packageVersion = "1",
            EBuildPipeline buildPipeline = EBuildPipeline.ScriptableBuildPipeline)
        {
            Debug.Log($"开始构建 : {buildTarget}");

            IBuildPipeline pipeline = null;
            BuildParameters buildParameters = null;
            
            if (buildPipeline == EBuildPipeline.BuiltinBuildPipeline)
            {
                // 构建参数
                BuiltinBuildParameters builtinBuildParameters = new BuiltinBuildParameters();
                
                // 执行构建
                pipeline = new BuiltinBuildPipeline();
                buildParameters = builtinBuildParameters;
                
                builtinBuildParameters.CompressOption = ECompressOption.LZ4;
            }
            else
            {
                ScriptableBuildParameters scriptableBuildParameters = new ScriptableBuildParameters();
                
                // 执行构建
                pipeline = new ScriptableBuildPipeline();
                
                scriptableBuildParameters.CompressOption = ECompressOption.LZ4;
                //务必设置内置着色器资源包名称，且和自动收集的着色器资源包名保持一致！
                scriptableBuildParameters.BuiltinShadersBundleName = GetBuiltinShaderBundleName("DefaultPackage");

                buildParameters = scriptableBuildParameters;
            }
            
            buildParameters.BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
            buildParameters.BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();
            buildParameters.BuildPipeline = buildPipeline.ToString();
            buildParameters.BuildTarget = buildTarget;
            buildParameters.PackageName = "DefaultPackage";
            buildParameters.PackageVersion = packageVersion;
            buildParameters.VerifyBuildingResult = true;
            buildParameters.FileNameStyle =  EFileNameStyle.BundleName_HashName;
            buildParameters.BuildinFileCopyOption = EBuildinFileCopyOption.None;
            buildParameters.BuildinFileCopyParams = string.Empty;
            // buildParameters.EncryptionServices = CreateEncryptionInstance("DefaultPackage",buildPipeline);
            buildParameters.EnableSharePackRule = true; // 启用共享资源打包
            buildParameters.BuildBundleType = (int)EBuildBundleType.AssetBundle; //必须指定资源包类型
            buildParameters.ClearBuildCacheFiles = false; //不清理构建缓存，启用增量构建，可以提高打包速度！
            buildParameters.UseAssetDependencyDB = true; //使用资源依赖关系数据库，可以提高打包速度！

            var buildResult = pipeline.Run(buildParameters, true);
            if (buildResult.Success)
            {
                Debug.Log($"构建成功 : {buildResult.OutputPackageDirectory}");
            }
            else
            {
                Debug.LogError($"构建失败 : {buildResult.ErrorInfo}");
            }
            
        }

        /// <summary>
        /// 内置着色器资源包名称
        /// 注意：和自动收集的着色器资源包名保持一致！
        /// </summary>
        private static string GetBuiltinShaderBundleName(string packageName)
        {
            var uniqueBundleName = AssetBundleCollectorSettingData.Setting.UniqueBundleName;
            var packRuleResult = DefaultPackRule.CreateShadersPackRuleResult();
            return packRuleResult.GetBundleName(packageName, uniqueBundleName);
        }
        
        /// <summary>
        /// 创建加密类实例
        /// </summary>
        private static IEncryptionServices CreateEncryptionInstance(string packageName, EBuildPipeline buildPipeline)
        {
            var encryptionClassName = AssetBundleBuilderSetting.GetPackageEncyptionClassName(packageName, buildPipeline);
            var encryptionClassTypes = EditorTools.GetAssignableTypes(typeof(IEncryptionServices));
            var classType = encryptionClassTypes.Find(x => x.FullName != null && x.FullName.Equals(encryptionClassName));
            if (classType != null)
            {
                Debug.Log($"Use Encryption {classType}");
                return (IEncryptionServices)Activator.CreateInstance(classType);
            }
            else
            {
                return null;
            }
        }

        // [MenuItem("Tools/Quick Build/一键打包Window", false, 90)]
        // public static void AutomationBuild()
        // {
        //     BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        //     BuildDLLCommand.BuildAndCopyDlls(target);
        //     AssetDatabase.Refresh();
        //     BuildInternal(target, Application.dataPath + "/../Builds/Windows", packageVersion: GetBuildPackageVersion());
        //     AssetDatabase.Refresh();
        //     BuildImp(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64, $"{Application.dataPath}/../Builds/Windows/Release_Windows.exe");
        // }

        // [MenuItem("Tools/Quick Build/一键打包Android", false, 90)]
        // public static void AutomationBuildAndroid()
        // {
        //     BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        //     BuildDLLCommand.BuildAndCopyDlls(target);
        //     AssetDatabase.Refresh();
        //     BuildInternal(target, outputRoot: Application.dataPath + "/../Bundles", packageVersion: GetBuildPackageVersion());
        //     AssetDatabase.Refresh();
        //     BuildImp(BuildTargetGroup.Android, BuildTarget.Android, $"{Application.dataPath}/../Build/Android/{GetBuildPackageVersion()}Android.apk");
        //     // BuildImp(BuildTargetGroup.Android, BuildTarget.Android, $"{Application.dataPath}/../Build/Android/Android.apk");
        // }

        // [MenuItem("Tools/Quick Build/一键打包IOS", false, 90)]
        // public static void AutomationBuildIOS()
        // {
        //     BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        //     BuildDLLCommand.BuildAndCopyDlls(target);
        //     AssetDatabase.Refresh();
        //     BuildInternal(target, outputRoot: Application.dataPath + "/../Bundles", packageVersion: GetBuildPackageVersion());
        //     AssetDatabase.Refresh();
        //     BuildImp(BuildTargetGroup.iOS, BuildTarget.iOS, $"{Application.dataPath}/../Build/IOS/XCode_Project");
        // }
        
        // [MenuItem("Tools/Quick Build/一键打包WebGL", false, 91)]
        // public static void AutomationBuildWebGL()
        // {
        //     BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        //     BuildDLLCommand.BuildAndCopyDlls(target);
        //     AssetDatabase.Refresh();
        //     BuildInternal(target, Application.dataPath + "/../Builds/WebGL", packageVersion: GetBuildPackageVersion());
        //     AssetDatabase.Refresh();
        //     BuildImp(BuildTargetGroup.WebGL, BuildTarget.WebGL, $"{Application.dataPath}/../Builds/WebGL");
        // }

        public static void BuildImp(BuildTargetGroup buildTargetGroup, BuildTarget buildTarget, string locationPathName)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(buildTargetGroup, BuildTarget.StandaloneWindows64);
            AssetDatabase.Refresh();

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Select(scene => scene.path).ToArray(),
                locationPathName = locationPathName,
                targetGroup = buildTargetGroup,
                target = buildTarget,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Build success: {summary.totalSize / 1024 / 1024} MB");
            }
            else
            {
                Debug.Log($"Build Failed" + summary.result);
            }
        }

        /// <summary>
        /// 获取当前补丁包版本
        /// </summary>
        private static string GetBuildPackageVersion()
        {
            // 读取当前版本号
            if (File.Exists(versionFilePath))
            {
                currentVersion = File.ReadAllText(versionFilePath).Trim();
            }
            else
            {
                currentVersion = "0"; // 初始版本
            }

            int patchVersion = int.Parse(currentVersion);
            patchVersion++;
            currentVersion = patchVersion.ToString();

            // 将新版本号写入文件
            File.WriteAllText(versionFilePath, currentVersion);

            return currentVersion;
        }
    }
}
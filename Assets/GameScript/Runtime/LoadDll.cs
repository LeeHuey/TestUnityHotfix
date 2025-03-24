using HybridCLR;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using YooAsset;
using System.IO.Compression;
using UnityEngine.Networking;

/// <summary>
/// 脚本工作流程：
/// 1.获取服务器热更信息
/// 2.下载资源，用yooAsset资源框架进行下载
///    1.资源文件，ab包
///    2.热更新dll
/// 3.给AOT DLL补充元素据，通过RuntimeApi.LoadMetadataForAOTAssembly
/// 4.通过实例化prefab，运行热更代码
/// </summary>
public class LoadDll : MonoBehaviour
{
    /// <summary>
    /// 资源系统运行模式
    /// </summary>
    public EPlayMode PlayMode = EPlayMode.OfflinePlayMode;

    private ResourcePackage _defaultPackage;
    
    // 本地版本号
    private string _localResVersion = "1";
    
    // 热更信息
    private HotUpdateInfo _hotUpdateInfo;
    
    // 获取版本请求URL
    private string GetVersionRequestURL()
    {
        // 引用Boot类中的相同逻辑
        string hostServerIP = "http://218.108.40.13:30080/";
        string updateServerUrl = hostServerIP + "updaty2/zs/";
        string packageName = "com.game.tennis";
        var verReqUrl = string.Format("{0}?game={1}&coreversion={2}&cppversion={3}&resversion={4}&client_version={5}&device_id={6}&channel_code={7}", 
            updateServerUrl + "resource2.php", packageName, 1, 1, 1, 1, SystemInfo.deviceUniqueIdentifier, 1);
        return verReqUrl;
    }

    void Start()
    {
        // 初始化本地版本号
        if (PlayerPrefs.HasKey("LocalResVersion"))
        {
            _localResVersion = PlayerPrefs.GetString("LocalResVersion");
        }
        
        StartCoroutine(LaunchGame());
    }

    IEnumerator LaunchGame()
    {
        // 1.获取热更信息
        yield return GetHotUpdateInfo();
        
        // 2.检查是否需要热更
        if (NeedHotUpdate())
        {
            // 下载并解压热更包
            yield return DownloadAndExtractHotUpdatePackage();
        }
        
        // 3.初始化资源系统
        yield return InitYooAssets();
        
        // 4.运行热更代码
        StartCoroutine(Run_InstantiateComponentByAsset());
    }
    
    IEnumerator GetHotUpdateInfo()
    {
        // 使用本地方法获取版本请求URL
        string versionRequestUrl = GetVersionRequestURL();
        Debug.Log($"开始获取热更信息，URL: {versionRequestUrl}");
        
        // 使用UnityWebRequest获取服务器返回的JSON信息
        UnityWebRequest webRequest = new UnityWebRequest(versionRequestUrl);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.timeout = 10;
        
        yield return webRequest.SendWebRequest();
        
        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"获取热更信息失败: {webRequest.error}");
            yield break;
        }
        
        string jsonText = webRequest.downloadHandler.text;
        Debug.Log($"获取热更信息成功: {jsonText}");
        
        // 解析JSON
        try
        {
            _hotUpdateInfo = JsonUtility.FromJson<HotUpdateInfo>(jsonText);
            Debug.Log($"解析热更信息成功, 服务器版本: {_hotUpdateInfo.current.resversion}");
        }
        catch (Exception e)
        {
            Debug.LogError($"解析热更信息失败: {e.Message}");
        }
    }
    
    bool NeedHotUpdate()
    {
        if (_hotUpdateInfo == null || string.IsNullOrEmpty(_hotUpdateInfo.current.resversion))
        {
            Debug.LogWarning("热更信息无效，跳过热更新");
            return false;
        }
        
        // 比较版本号
        int serverVersion = int.Parse(_hotUpdateInfo.current.resversion);
        int localVersion = int.Parse(_localResVersion);
        
        bool needUpdate = serverVersion > localVersion;
        Debug.Log($"本地版本: {localVersion}, 服务器版本: {serverVersion}, 需要热更: {needUpdate}");
        
        return needUpdate;
    }
    
    IEnumerator DownloadAndExtractHotUpdatePackage()
    {
        if (_hotUpdateInfo == null || _hotUpdateInfo.resources == null || _hotUpdateInfo.resources.Length == 0)
        {
            Debug.LogError("热更资源信息为空，无法下载热更包");
            yield break;
        }
        
        // 获取热更包信息
        var resource = _hotUpdateInfo.resources[0];
        string zipFileName = resource.file;
        string zipMd5 = resource.md5;
        long zipSize = long.Parse(resource.size);
        string downloadUrl = _hotUpdateInfo.host + zipFileName;
        
        Debug.Log($"开始下载热更包: {downloadUrl}, MD5: {zipMd5}, 大小: {zipSize}字节");
        
        // 下载热更zip包
        string tempDownloadPath = Path.Combine(Application.persistentDataPath, zipFileName);
        
        // 使用UnityWebRequest下载热更包
        UnityWebRequest request = new UnityWebRequest(downloadUrl);
        request.downloadHandler = new DownloadHandlerFile(tempDownloadPath);
        request.timeout = 60;
        
        // 开始下载
        var operation = request.SendWebRequest();
        
        // 监控下载进度
        while (!operation.isDone)
        {
            float progress = request.downloadProgress;
            Debug.Log($"下载热更包进度: {progress:P2}");
            yield return null;
        }
        
        // 检查下载结果
        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"下载热更包失败: {request.error}");
            yield break;
        }
        
        Debug.Log($"热更包下载完成: {tempDownloadPath}");
        
        // 验证MD5
        byte[] zipData = File.ReadAllBytes(tempDownloadPath);
        string downloadedMd5 = CalculateMD5(zipData);
        
        if (downloadedMd5 != zipMd5.ToLower())
        {
            Debug.LogError($"热更包MD5验证失败, 期望: {zipMd5}, 实际: {downloadedMd5}");
            yield break;
        }
        
        // 复制到标准路径
        string zipPath = Path.Combine(Application.persistentDataPath, "hotupdate.zip");
        File.Copy(tempDownloadPath, zipPath, true);
        
        // 解压热更包
        yield return ExtractHotUpdatePackage(zipPath);
        
        // 更新本地版本号
        _localResVersion = _hotUpdateInfo.current.resversion;
        PlayerPrefs.SetString("LocalResVersion", _localResVersion);
        PlayerPrefs.Save();
        
        Debug.Log($"热更新完成，当前版本: {_localResVersion}");
    }
    
    IEnumerator ExtractHotUpdatePackage(string zipPath)
    {
        Debug.Log($"开始解压热更包: {zipPath}");
        
        // 解压目标目录 - 确保与YooAssets的缓存目录一致
        string extractPath = GetYooAssetsSandboxPath();
        
        // 确保目录存在
        if (!Directory.Exists(extractPath))
        {
            Directory.CreateDirectory(extractPath);
        }
        
        // 在后台线程中解压
        yield return new WaitForThreadedTask(() =>
        {
            try
            {
                // 解压ZIP文件
                ZipFile.ExtractToDirectory(zipPath, extractPath, true);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"解压热更包失败: {e.Message}");
                return false;
            }
        });
        
        // 删除下载的ZIP文件
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }
        
        Debug.Log($"热更包解压完成，解压路径: {extractPath}");
    }
    
    // 获取YooAssets的沙盒路径
    private string GetYooAssetsSandboxPath()
    {
        // YooAssets默认的沙盒根路径
        return Path.Combine(Application.persistentDataPath, "YooAssets");
    }
    
    // 计算MD5
    private string CalculateMD5(byte[] data)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            var hash = md5.ComputeHash(data);
            var sb = new StringBuilder();
            
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }
            
            return sb.ToString();
        }
    }

    IEnumerator InitYooAssets()
    {
        // 创建初始化参数
        OfflinePlayModeParameters createParameters = null;
        
        try
        {
            // 初始化资源系统
            YooAssets.Initialize();
            Debug.Log("YooAssets初始化成功");

            // 创建默认包
            string packageName = "DefaultPackage";
            var package = YooAssets.TryGetPackage(packageName);
            if (package == null)
                package = YooAssets.CreatePackage(packageName);
            YooAssets.SetDefaultPackage(package);
            Debug.Log($"创建包 {packageName} 成功");
            
            // 获取热更解压的沙盒路径
            string sandboxPath = GetYooAssetsSandboxPath();
            Debug.Log($"初始化YooAsset，沙盒路径: {sandboxPath}");
            
            // 检查是否需要手动寻找DLL (直接工作保障方案)
            PrepareHotUpdateDlls();
            
            // 检查当前热更版本号
            string manifestVersion = "1"; // 从沙盒目录结构看，清单版本号是1
            Debug.Log($"热更清单版本号: {manifestVersion}");
            
            // 配置离线模式参数
            createParameters = new OfflinePlayModeParameters();
            // 设置PackageRoot为热更资源的沙盒路径，确保路径不要以/结尾防止URL格式问题
            string formattedSandboxPath = sandboxPath.TrimEnd('/');
            createParameters.BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(null, formattedSandboxPath);
            
            // 保存package引用
            _defaultPackage = package;
        }
        catch (Exception ex)
        {
            Debug.LogError($"YooAssets初始化异常: {ex.Message}\n{ex.StackTrace}");
            yield break;
        }
        
        // 开始异步初始化包
        var initOperation = _defaultPackage.InitializeAsync(createParameters);
        yield return initOperation;
        
        if (initOperation.Status != EOperationStatus.Succeed)
        {
            Debug.LogError($"资源包初始化失败：{initOperation.Error}");
            yield break;
        }
        
        Debug.Log("资源包初始化成功，开始加载资源...");
        
        // 检查是否有清单文件
        string sandboxRoot = GetYooAssetsSandboxPath();
        string manifestPath = Path.Combine(sandboxRoot, "DefaultPackage_1.bytes");
        string hashPath = Path.Combine(sandboxRoot, "DefaultPackage_1.hash");
        string versionPath = Path.Combine(sandboxRoot, "DefaultPackage.version");
        
        Debug.Log($"检查清单文件是否存在:\n" +
                 $"清单文件: {manifestPath} 存在={File.Exists(manifestPath)}\n" +
                 $"Hash文件: {hashPath} 存在={File.Exists(hashPath)}\n" +
                 $"版本文件: {versionPath} 存在={File.Exists(versionPath)}");
        
        // 如果清单文件不存在，创建一个简单的版本文件
        if (!File.Exists(versionPath))
        {
            try
            {
                File.WriteAllText(versionPath, "1");
                Debug.Log($"创建了版本文件: {versionPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"创建版本文件失败: {ex.Message}");
            }
        }
        
        // 加载所需的DLL资源
        var assets = new List<string> { "HotUpdate.dll" }.Concat(AOTMetaAssemblyFiles).ToList();
        bool allSuccess = true;
        
        foreach (var asset in assets)
        {
            Debug.Log($"尝试加载资源: {asset}");
            var handle = _defaultPackage.LoadAssetAsync<TextAsset>(asset);
            yield return handle;
            
            if (!handle.IsValid)
            {
                Debug.LogError($"通过YooAsset加载资源失败: {asset}，错误: {handle.LastError}，尝试从文件系统直接加载");
                
                // 尝试从文件系统直接加载
                if (TryLoadAssetFromFileSystem(asset))
                {
                    Debug.Log($"从文件系统成功加载资源: {asset}");
                    continue;
                }
                
                allSuccess = false;
                continue;
            }
            
            var assetObj = handle.AssetObject as TextAsset;
            if (assetObj == null)
            {
                Debug.LogError($"资源加载成功但类型错误: {asset}");
                allSuccess = false;
                continue;
            }
            
            s_assetDatas[asset] = assetObj;
            Debug.Log($"资源加载成功: {asset}，大小: {assetObj.bytes.Length} 字节");
        }
        
        if (!allSuccess)
        {
            Debug.LogWarning("部分资源加载失败，热更新可能无法正常工作");
        }
        
        // 处理加载好的DLL
        LoadMetadataForAOTAssemblies();
        LoadHotUpdateDll();
    }
    
    private string GetHostServerURL()
    {
        // 如果已经获取到热更信息，使用热更信息中的host
        if (_hotUpdateInfo != null && !string.IsNullOrEmpty(_hotUpdateInfo.host))
        {
            return _hotUpdateInfo.host;
        }
        
        // 否则返回默认值
        return "http://218.108.40.13:30080/updaty2/zs/";
    }
    
    //补充元数据dll的列表
    //通过RuntimeApi.LoadMetadataForAOTAssembly()函数来补充AOT泛型的原始元数据
    private static List<string> AOTMetaAssemblyFiles { get; } = new() { "mscorlib.dll", "System.dll", "System.Core.dll", };
    private static Dictionary<string, TextAsset> s_assetDatas = new Dictionary<string, TextAsset>();
    private static Dictionary<string, byte[]> s_dllBytes = new Dictionary<string, byte[]>(); // 存储直接从文件读取的DLL字节
    private static Assembly _hotUpdateAss;
    
    public static byte[] ReadBytesFromStreamingAssets(string dllName)
    {
        if (s_assetDatas.ContainsKey(dllName))
        {
            return s_assetDatas[dllName].bytes;
        }
        
        if (s_dllBytes.ContainsKey(dllName))
        {
            return s_dllBytes[dllName];
        }

        return Array.Empty<byte>();
    }

    /// <summary>
    /// 为aot assembly加载原始metadata， 这个代码放aot或者热更新都行。
    /// 一旦加载后，如果AOT泛型函数对应native实现不存在，则自动替换为解释模式执行
    /// </summary>
    private static void LoadMetadataForAOTAssemblies()
    {
        Debug.Log("开始加载AOT元数据");
        /// 注意，补充元数据是给AOT dll补充元数据，而不是给热更新dll补充元数据。
        /// 热更新dll不缺元数据，不需要补充，如果调用LoadMetadataForAOTAssembly会返回错误
        HomologousImageMode mode = HomologousImageMode.SuperSet;
        foreach (var aotDllName in AOTMetaAssemblyFiles)
        {
            byte[] dllBytes = ReadBytesFromStreamingAssets(aotDllName);
            // 加载assembly对应的dll，会自动为它hook。一旦aot泛型函数的native函数不存在，用解释器版本代码
            LoadImageErrorCode err = RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, mode);
            Debug.Log($"LoadMetadataForAOTAssembly:{aotDllName}. mode:{mode} ret:{err}");
        }
    }

    private void LoadHotUpdateDll()
    {
        Debug.Log("开始加载热更新DLL");
        // 加载热更dll
#if !UNITY_EDITOR
        _hotUpdateAss = Assembly.Load(ReadBytesFromStreamingAssets("HotUpdate.dll"));
#else
        _hotUpdateAss = System.AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "HotUpdate");
#endif
        Debug.Log("热更新DLL加载完成");
    }

    IEnumerator Run_InstantiateComponentByAsset()
    {
        Debug.Log("运行热更代码");
        // 通过实例化assetbundle中的资源，还原资源上的热更新脚本
        var package = YooAssets.GetPackage("DefaultPackage");
        var handle = package.LoadAssetAsync<GameObject>("Cube");
        yield return handle;
        handle.Completed += Handle_Completed;
    }

    private void Handle_Completed(AssetHandle obj)
    {
        Debug.Log("准备实例化");
        GameObject go = obj.InstantiateSync();
        Debug.Log($"Prefab name is {go.name}");
    }

    // 尝试从文件系统直接加载资源
    private bool TryLoadAssetFromFileSystem(string assetName)
    {
        try
        {
            // 尝试从几个可能的位置加载资源
            string[] possiblePaths = new string[]
            {
                Path.Combine(GetYooAssetsSandboxPath(), assetName),
                Path.Combine(GetYooAssetsSandboxPath(), "Resources", assetName),
                Path.Combine(GetYooAssetsSandboxPath(), "DefaultPackage", assetName),
                Path.Combine(Application.streamingAssetsPath, assetName),
                Path.Combine(Application.streamingAssetsPath, "Resources", assetName),
                Path.Combine(Application.streamingAssetsPath, "DefaultPackage", assetName)
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    Debug.Log($"找到资源文件: {path}");
                    var bytes = File.ReadAllBytes(path);
                    s_dllBytes[assetName] = bytes;
                    Debug.Log($"成功读取文件，大小: {bytes.Length} 字节");
                    return true;
                }
            }

            // 递归搜索热更目录
            string hotUpdateDir = GetYooAssetsSandboxPath();
            if (Directory.Exists(hotUpdateDir))
            {
                var foundFiles = Directory.GetFiles(hotUpdateDir, assetName, SearchOption.AllDirectories);
                if (foundFiles.Length > 0)
                {
                    Debug.Log($"通过递归搜索找到资源: {foundFiles[0]}");
                    var bytes = File.ReadAllBytes(foundFiles[0]);
                    s_dllBytes[assetName] = bytes;
                    Debug.Log($"成功读取文件，大小: {bytes.Length} 字节");
                    return true;
                }
            }

            Debug.LogError($"无法在任何位置找到资源: {assetName}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"从文件系统加载资源时出错: {ex.Message}");
            return false;
        }
    }

    // 预先准备热更DLL文件，直接从可能的位置查找
    private void PrepareHotUpdateDlls()
    {
        // 首先检查是否已经有了热更DLL数据
        if (s_assetDatas.ContainsKey("HotUpdate.dll") || s_dllBytes.ContainsKey("HotUpdate.dll"))
        {
            return;
        }
        
        string[] possiblePaths = new string[]
        {
            // HybridCLR最常见输出路径
            Path.Combine(Application.dataPath, "../HybridCLRData/HotUpdateDlls/Generated", "HotUpdate.dll"),
            Path.Combine(Application.dataPath, "../HybridCLRData/HotUpdateDlls/Main", "HotUpdate.dll"),
            Path.Combine(Application.dataPath, "../HybridCLRData/AssembliesPostIl2CppStrip", "HotUpdate.dll"),
            
            // 其他可能的路径
            Path.Combine(Application.dataPath, "HotUpdate.dll"),
            Path.Combine(Application.streamingAssetsPath, "HotUpdate.dll"),
            Path.Combine(Application.persistentDataPath, "HotUpdate.dll"),
            
            // 从bundle中可能解压出来的位置
            Path.Combine(GetYooAssetsSandboxPath(), "HotUpdate.dll")
        };
        
        // 尝试从剪切板中获取路径(开发环境可能会复制路径)
        if (GUIUtility.systemCopyBuffer != null && GUIUtility.systemCopyBuffer.EndsWith(".dll") && 
            File.Exists(GUIUtility.systemCopyBuffer))
        {
            Debug.Log($"尝试从剪切板加载DLL: {GUIUtility.systemCopyBuffer}");
            possiblePaths = possiblePaths.Append(GUIUtility.systemCopyBuffer).ToArray();
        }
        
        foreach (var path in possiblePaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    Debug.Log($"找到热更DLL: {path}");
                    var bytes = File.ReadAllBytes(path);
                    s_dllBytes["HotUpdate.dll"] = bytes;
                    
                    // 同时尝试加载AOT DLL
                    var aotDllPaths = new List<string>();
                    foreach (var aotDll in AOTMetaAssemblyFiles)
                    {
                        var aotPath = Path.Combine(Path.GetDirectoryName(path), aotDll);
                        if (File.Exists(aotPath))
                        {
                            aotDllPaths.Add(aotPath);
                            s_dllBytes[aotDll] = File.ReadAllBytes(aotPath);
                            Debug.Log($"找到AOT DLL: {aotPath}");
                        }
                    }
                    
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"尝试加载DLL失败 {path}: {ex.Message}");
            }
        }
        
        Debug.LogWarning("未能找到任何热更DLL，可能需要手动处理或使用其他方法。");
    }
}

// 热更新信息类
[System.Serializable]
public class HotUpdateInfo
{
    public Current current;
    public Resource[] resources;
    public string host;
    public string updateurl;
    public CfgData cfg_data;
    public int apk_version;
    public string voice_url_pre;
    public string voice_upload_url;
    public string tcp_cfg;
    public Rate rate;
    public string battle_url_pre;
    public Maintain maintain;
    public int isinreview;
    public string alipay_url;
    public string wechatpay_url;
    public string quickpay_callback_url;
    public int pay_test;
    public int is_999;
}

[System.Serializable]
public class Current
{
    public string coreversion;
    public string cppversion;
    public string resversion;
}

[System.Serializable]
public class Resource
{
    public string file;
    public string md5;
    public string size;
}

[System.Serializable]
public class CfgData
{
    public int version;
    public string md5;
    public string size;
    public string url;
}

[System.Serializable]
public class Rate
{
    public int show;
}

[System.Serializable]
public class Maintain
{
    public int state;
    public int end;
}

// 后台线程任务帮助类
public class WaitForThreadedTask : CustomYieldInstruction
{
    private bool _isDone;
    private System.Func<bool> _action;

    public WaitForThreadedTask(System.Func<bool> action)
    {
        _action = action;
        _isDone = false;
        System.Threading.ThreadPool.QueueUserWorkItem(new System.Threading.WaitCallback(RunAction));
    }

    private void RunAction(object state)
    {
        _action();
        _isDone = true;
    }

    public override bool keepWaiting => !_isDone;
}
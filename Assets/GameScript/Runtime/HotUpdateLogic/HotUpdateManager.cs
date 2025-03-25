using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;
using System.Reflection;
using System.IO;
using System;
using System.Linq;

public class HotUpdateManager : MonoBehaviour
{
    // 单例实例
    public static HotUpdateManager Instance { get; private set; }
    
    // 资源系统运行模式
    public EPlayMode PlayMode = EPlayMode.OfflinePlayMode;
    
    // 热更信息属性
    public HotUpdateInfo HotUpdateInfo { get; set; }
    
    // 本地版本号属性
    public string LocalResVersion { get; set; } = "1";
    
    // 热更包路径
    public string HotUpdateZipPath { get; set; }
    
    // 默认包引用
    public ResourcePackage DefaultPackage { get; set; }
    
    // 热更Assembly
    public Assembly HotUpdateAssembly { get; set; }
    
    // 存储加载的资源数据
    private Dictionary<string, TextAsset> _assetDatas = new Dictionary<string, TextAsset>();
    
    // 存储直接从文件读取的DLL字节
    private Dictionary<string, byte[]> _dllBytes = new Dictionary<string, byte[]>();
    
    // AOT元数据文件列表
    public List<string> AOTMetaAssemblyFiles { get; } = new List<string> { "mscorlib.dll", "System.dll", "System.Core.dll" };
    
    private void Awake()
    {
        // 确保单例
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
            
            // 初始化本地版本号
            if (PlayerPrefs.HasKey("LocalResVersion"))
            {
                LocalResVersion = PlayerPrefs.GetString("LocalResVersion");
            }
        }
        else
        {
            Destroy(this.gameObject);
        }
    }
    
    // 获取版本请求URL
    public string GetVersionRequestURL()
    {
        string hostServerIP = "http://218.108.40.13:30080/";
        string updateServerUrl = hostServerIP + "updaty2/zs/";
        string packageName = "com.game.tennis";
        var verReqUrl = string.Format("{0}?game={1}&coreversion={2}&cppversion={3}&resversion={4}&client_version={5}&device_id={6}&channel_code={7}", 
            updateServerUrl + "resource2.php", packageName, 1, 1, 1, 1, SystemInfo.deviceUniqueIdentifier, 1);
        return verReqUrl;
    }
    
    // 获取YooAssets的沙盒路径
    public string GetYooAssetsSandboxPath()
    {
        // YooAssets默认的沙盒根路径
        // return Path.Combine(Application.persistentDataPath, "YooAssets", "DefaultPackage");
        return Path.Combine("/Users/lixu/Desktop/YooAssets", "DefaultPackage");
    }
    
    // 启动热更新流程
    public IEnumerator LaunchHotUpdate()
    {
        var operation = new HotUpdateOperation();
        YooAssets.StartOperation(operation);
        yield return operation;
    }
    
    // 添加资源数据
    public void AddAssetData(string name, TextAsset asset)
    {
        _assetDatas[name] = asset;
    }
    
    // 添加DLL字节数据
    public void AddDllBytes(string name, byte[] bytes)
    {
        _dllBytes[name] = bytes;
    }
    
    // 从StreamingAssets读取字节
    public byte[] ReadBytesFromStreamingAssets(string dllName)
    {
        if (_assetDatas.ContainsKey(dllName))
        {
            return _assetDatas[dllName].bytes;
        }
        
        if (_dllBytes.ContainsKey(dllName))
        {
            return _dllBytes[dllName];
        }

        return Array.Empty<byte>();
    }
    
    // 尝试从文件系统直接加载资源
    public bool TryLoadAssetFromFileSystem(string assetName)
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
                    _dllBytes[assetName] = bytes;
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
                    _dllBytes[assetName] = bytes;
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
} 
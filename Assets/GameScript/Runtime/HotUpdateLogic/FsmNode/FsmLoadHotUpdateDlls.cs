using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniFramework.Machine;
using System.Linq;
using HybridCLR;
using System;
using System.IO;
using YooAsset;
using System.Reflection;

internal class FsmLoadHotUpdateDlls : IStateNode
{
    private StateMachine _machine;
    private HotUpdateManager _hotUpdateManager;

    void IStateNode.OnCreate(StateMachine machine)
    {
        _machine = machine;
        _hotUpdateManager = HotUpdateManager.Instance;
    }
    
    void IStateNode.OnEnter()
    {
        HotUpdateEventDefine.HotUpdateStepsChange.SendEventMessage("正在加载热更DLL...");
        _hotUpdateManager.StartCoroutine(LoadHotUpdateDlls());
    }
    
    void IStateNode.OnUpdate()
    {
    }
    
    void IStateNode.OnExit()
    {
    }
    
    private IEnumerator LoadHotUpdateDlls()
    {
        // 加载所需的DLL资源
        var assets = new List<string> { "TestHotFix.dll" }.Concat(_hotUpdateManager.AOTMetaAssemblyFiles).ToList();
        bool allSuccess = true;
        
        foreach (var asset in assets)
        {
            Debug.Log($"尝试加载资源: {asset}");
            var handle = _hotUpdateManager.DefaultPackage.LoadAssetAsync<TextAsset>(asset);
            yield return handle;
            
            if (!handle.IsValid)
            {
                Debug.LogError($"通过YooAsset加载资源失败: {asset}，错误: {handle.LastError}，尝试从文件系统直接加载");
                
                // 尝试从文件系统直接加载
                if (_hotUpdateManager.TryLoadAssetFromFileSystem(asset))
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
            
            _hotUpdateManager.AddAssetData(asset, assetObj);
            Debug.Log($"资源加载成功: {asset}，大小: {assetObj.bytes.Length} 字节");
        }
        
        if (!allSuccess)
        {
            Debug.LogWarning("部分资源加载失败，热更新可能无法正常工作");
        }
        
        // 处理加载好的DLL
        LoadMetadataForAOTAssemblies();
        LoadHotUpdateDll();
        
        // 切换到运行热更代码状态
        _machine.ChangeState<FsmRunHotUpdateCode>();
    }
    
    /// <summary>
    /// 为aot assembly加载原始metadata， 这个代码放aot或者热更新都行。
    /// 一旦加载后，如果AOT泛型函数对应native实现不存在，则自动替换为解释模式执行
    /// </summary>
    private void LoadMetadataForAOTAssemblies()
    {
        Debug.Log("开始加载AOT元数据");
        /// 注意，补充元数据是给AOT dll补充元数据，而不是给热更新dll补充元数据。
        /// 热更新dll不缺元数据，不需要补充，如果调用LoadMetadataForAOTAssembly会返回错误
        HomologousImageMode mode = HomologousImageMode.SuperSet;
        foreach (var aotDllName in _hotUpdateManager.AOTMetaAssemblyFiles)
        {
            byte[] dllBytes = _hotUpdateManager.ReadBytesFromStreamingAssets(aotDllName);
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
        _hotUpdateManager.HotUpdateAssembly = Assembly.Load(_hotUpdateManager.ReadBytesFromStreamingAssets("TestHotFix.dll"));
#else
        _hotUpdateManager.HotUpdateAssembly = System.AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "TestHotFix");
#endif
        Debug.Log("热更新DLL加载完成");
    }
} 
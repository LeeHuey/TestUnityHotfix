using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniFramework.Machine;
using YooAsset;
using System;
using System.IO;
using System.Linq;

internal class FsmInitYooAssets : IStateNode
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
        HotUpdateEventDefine.HotUpdateStepsChange.SendEventMessage("正在初始化资源系统...");
        _hotUpdateManager.StartCoroutine(InitYooAssets());
    }
    
    void IStateNode.OnUpdate()
    {
    }
    
    void IStateNode.OnExit()
    {
    }
    
    private IEnumerator InitYooAssets()
    {
        // 创建初始化参数
        OfflinePlayModeParameters createParameters = null;
        
        try
        {
            // // 初始化资源系统
            // YooAssets.Initialize();
            // Debug.Log("YooAssets初始化成功");

            // 创建默认包
            string packageName = "DefaultPackage";
            var package = YooAssets.TryGetPackage(packageName);
            if (package == null)
                package = YooAssets.CreatePackage(packageName);
            YooAssets.SetDefaultPackage(package);
            Debug.Log($"创建包 {packageName} 成功");
            
            // 获取热更解压的沙盒路径
            string sandboxPath = _hotUpdateManager.GetYooAssetsSandboxPath();
            Debug.Log($"初始化YooAsset，沙盒路径: {sandboxPath}");
            
            // 配置离线模式参数
            createParameters = new OfflinePlayModeParameters();
            
            // 配置文件系统参数
            createParameters.BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(null, sandboxPath);
            
            // 保存package引用
            _hotUpdateManager.DefaultPackage = package;
        }
        catch (Exception ex)
        {
            Debug.LogError($"YooAssets初始化异常: {ex.Message}\n{ex.StackTrace}");
            yield break;
        }
        
        // 开始异步初始化包
        Debug.Log("开始初始化资源包...");
        var initOperation = _hotUpdateManager.DefaultPackage.InitializeAsync(createParameters);
        yield return initOperation;
        
        if (initOperation.Status != EOperationStatus.Succeed)
        {
            Debug.LogError($"资源包初始化失败：{initOperation.Error}");
            yield break;
        }
        
        Debug.Log("资源包初始化成功，开始更新清单...");
        
        // 开始异步更新清单文件
        var manifestOperation = _hotUpdateManager.DefaultPackage.UpdatePackageManifestAsync(_hotUpdateManager.LocalResVersion);
        yield return manifestOperation;
        
        if (manifestOperation.Status != EOperationStatus.Succeed)
        {
            Debug.LogError($"资源清单加载失败：{manifestOperation.Error}");
            yield break;
        }

        Debug.Log("资源清单加载成功");
        
        // 切换到加载热更DLL状态
        _machine.ChangeState<FsmLoadHotUpdateDlls>();
    }
} 
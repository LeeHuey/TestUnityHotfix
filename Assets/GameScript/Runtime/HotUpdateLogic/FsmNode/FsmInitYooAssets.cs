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
        // 创建默认包
        string packageName = "DefaultPackage";
        var package = YooAssets.TryGetPackage(packageName);
        if (package == null)
            package = YooAssets.CreatePackage(packageName);
        YooAssets.SetDefaultPackage(package);
        Debug.Log($"创建包 {packageName} 成功");
        
        // 保存package引用
        _hotUpdateManager.DefaultPackage = package;
        
        // 根据运行模式初始化资源系统
        InitializationOperation initOperation = null;
        
        try
        {
            // 获取当前的运行模式
            EPlayMode playMode = _hotUpdateManager.PlayMode;
            Debug.Log($"当前运行模式: {playMode}");
            
            if (playMode == EPlayMode.EditorSimulateMode)
            {
                #if UNITY_EDITOR
                // 编辑器模拟模式
                Debug.Log("使用编辑器模拟模式");
                
                // 创建编辑器模拟模式的参数
                var buildResult = EditorSimulateModeHelper.SimulateBuild("DefaultPackage");    
                var packageRoot = buildResult.PackageRootDirectory;
                Debug.Log($"编辑器模拟模式资源包根目录: {packageRoot}");
                var initParameters = new EditorSimulateModeParameters();
                initParameters.EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(packageRoot);
                initOperation = package.InitializeAsync(initParameters);
                #else
                Debug.LogError("EditorSimulateMode只能在编辑器下使用，已切换为离线模式");
                playMode = EPlayMode.OfflinePlayMode;
                _hotUpdateManager.PlayMode = playMode;
                #endif
            }
            
            // 离线模式
            if (playMode == EPlayMode.OfflinePlayMode)
            {
                var initParameters = new OfflinePlayModeParameters();
                // 获取热更解压的沙盒路径
                string sandboxPath = _hotUpdateManager.GetYooAssetsSandboxPath();
                Debug.Log($"使用离线模式，沙盒路径: {sandboxPath}");
                initParameters.BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(null, sandboxPath);
                initOperation = package.InitializeAsync(initParameters);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"YooAssets初始化异常: {ex.Message}\n{ex.StackTrace}");
            _machine.ChangeState<FsmLoadHotUpdateDlls>();
            yield break;
        }
            
        // 开始异步初始化包
        Debug.Log($"开始初始化资源包...");
        yield return initOperation;
        
        if (initOperation == null || initOperation.Status != EOperationStatus.Succeed)
        {
            Debug.LogError($"资源包初始化失败：{(initOperation != null ? initOperation.Error : "未知错误")}");
            _machine.ChangeState<FsmLoadHotUpdateDlls>();
            yield break;
        }
        
        Debug.Log("资源包初始化成功，开始更新清单...");
        
        // 更新资源清单
        bool manifestUpdateSuccess = false;
        // YooAsset清单更新操作
        AsyncOperationBase manifestOperation = null;
        
        if (_hotUpdateManager.PlayMode == EPlayMode.EditorSimulateMode)
        {
            #if UNITY_EDITOR
            Debug.Log("编辑器模拟模式下更新清单");
            // 在编辑器模拟模式下，使用编辑器模拟资源清单
            manifestOperation = package.UpdatePackageManifestAsync("Simulate");
            #endif
        }
        else
        {
            // 离线模式下更新清单
            Debug.Log($"离线模式下更新清单，版本号: {_hotUpdateManager.LocalResVersion}");
            manifestOperation = package.UpdatePackageManifestAsync(_hotUpdateManager.LocalResVersion);
        }
        
        // 等待清单更新操作完成
        if (manifestOperation != null)
        {
            yield return manifestOperation;
            manifestUpdateSuccess = manifestOperation.Status == EOperationStatus.Succeed;
            
            if (!manifestUpdateSuccess)
            {
                Debug.LogError($"资源清单更新失败: {manifestOperation.Error}");
            }
        }
        
        if (!manifestUpdateSuccess)
        {
            Debug.LogError("资源清单更新失败，热更新可能无法正常工作");
            _machine.ChangeState<FsmLoadHotUpdateDlls>();
            yield break;
        }
        
        Debug.Log("资源清单更新成功，即将进入下一阶段");
        
        // 切换到加载热更DLL状态
        _machine.ChangeState<FsmLoadHotUpdateDlls>();
    }
} 
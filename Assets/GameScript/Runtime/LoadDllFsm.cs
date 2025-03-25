using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;
using UniFramework.Event;
using UniFramework.Machine;

/// <summary>
/// 使用状态机版本的热更DLL加载器
/// </summary>
public class LoadDllFsm : MonoBehaviour
{
    /// <summary>
    /// 资源系统运行模式
    /// </summary>
    public EPlayMode PlayMode = EPlayMode.OfflinePlayMode;
    
    void Start()
    {
        PlayMode = EPlayMode.EditorSimulateMode;

        // 初始化事件系统
        UniEvent.Initalize();

        YooAssets.Initialize();
        
        // 确保有HotUpdateManager
        var hotUpdateManagerObj = new GameObject("HotUpdateManager");
        var hotUpdateManager = hotUpdateManagerObj.AddComponent<HotUpdateManager>();
        hotUpdateManager.PlayMode = PlayMode;
        
        // 启动热更新流程
        StartCoroutine(LaunchGame());
    }
    
    IEnumerator LaunchGame()
    {
        Debug.Log("启动热更新流程");
        yield return HotUpdateManager.Instance.LaunchHotUpdate();
        Debug.Log("热更新流程完成");
    }
    
    void OnDestroy()
    {
        
    }
} 
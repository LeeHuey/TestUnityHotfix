using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniFramework.Machine;
using YooAsset;

internal class FsmRunHotUpdateCode : IStateNode
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
        HotUpdateEventDefine.HotUpdateStepsChange.SendEventMessage("正在运行热更代码...");
        _hotUpdateManager.StartCoroutine(RunHotUpdateCode());
    }
    
    void IStateNode.OnUpdate()
    {
    }
    
    void IStateNode.OnExit()
    {
    }
    
    private IEnumerator RunHotUpdateCode()
    {
        Debug.Log("运行热更代码");
        // 通过实例化assetbundle中的资源，还原资源上的热更新脚本
        var package = YooAssets.GetPackage("DefaultPackage");
        string assetName = "UILoading"; // 要加载的预制体名称
        
        Debug.Log($"尝试加载热更资源: {assetName}");
        
        if (_hotUpdateManager.PlayMode == EPlayMode.EditorSimulateMode)
        {
            #if UNITY_EDITOR
            // 在编辑器模拟模式下，可能需要特别处理资源加载
            Debug.Log("编辑器模拟模式下加载资源");
            #endif
        }
        
        // 无论哪种模式，都尝试加载资源
        var handle = package.LoadAssetAsync<GameObject>(assetName);
        yield return handle;
        
        if (handle.Status != EOperationStatus.Succeed)
        {
            // Debug.LogError($"加载预制体失败：{handle.Error}");
            
            // 即使加载失败，也继续流程，完成热更新操作
            FinishHotUpdateOperation();
            yield break;
        }
        
        // 资源加载成功，进行实例化
        Debug.Log($"资源 {assetName} 加载成功，准备实例化");
        GameObject go = handle.InstantiateSync();
        Debug.Log($"实例化预制体成功: {go.name}");
        
        // 完成热更新操作
        FinishHotUpdateOperation();
    }
    
    private void FinishHotUpdateOperation()
    {
        // 完成热更新操作
        HotUpdateOperation hotUpdateOperation = _machine.Owner as HotUpdateOperation;
        if (hotUpdateOperation != null)
        {
            Debug.Log("热更新流程全部完成");
            hotUpdateOperation.SetFinish();
        }
    }
} 
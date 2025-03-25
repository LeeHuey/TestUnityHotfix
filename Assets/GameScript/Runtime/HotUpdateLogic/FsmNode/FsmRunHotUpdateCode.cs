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
        var handle = package.LoadAssetAsync<GameObject>("Cube");
        yield return handle;
        
        if (handle.Status != EOperationStatus.Succeed)
        {
            // Debug.LogError($"加载Cube预制体失败：{handle.Error}");
            yield break;
        }
        
        handle.Completed += Handle_Completed;
        
        // 完成热更新操作
        HotUpdateOperation hotUpdateOperation = _machine.Owner as HotUpdateOperation;
        if (hotUpdateOperation != null)
        {
            hotUpdateOperation.SetFinish();
        }
    }

    private void Handle_Completed(AssetHandle obj)
    {
        Debug.Log("准备实例化");
        GameObject go = obj.InstantiateSync();
        Debug.Log($"Prefab name is {go.name}");
    }
} 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniFramework.Machine;
using YooAsset;

internal class FsmCheckNeedHotUpdate : IStateNode
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
        HotUpdateEventDefine.HotUpdateStepsChange.SendEventMessage("检查是否需要热更...");
        
        if (NeedHotUpdate())
        {
            Debug.Log("需要热更新，切换到下载热更包状态");
            _machine.ChangeState<FsmDownloadHotUpdatePackage>();
        }
        else
        {
            Debug.Log("不需要热更新，直接初始化资源系统");
            _machine.ChangeState<FsmInitYooAssets>();
        }
    }
    
    void IStateNode.OnUpdate()
    {
    }
    
    void IStateNode.OnExit()
    {
    }
    
    private bool NeedHotUpdate()
    {
        if (_hotUpdateManager.PlayMode == EPlayMode.EditorSimulateMode) {
            return false;
        }

        if (_hotUpdateManager.HotUpdateInfo == null || string.IsNullOrEmpty(_hotUpdateManager.HotUpdateInfo.current.resversion))
        {
            Debug.LogWarning("热更信息无效，跳过热更新");
            return false;
        }
        
        // 比较版本号
        int serverVersion = int.Parse(_hotUpdateManager.HotUpdateInfo.current.resversion);
        int localVersion = int.Parse(_hotUpdateManager.LocalResVersion);
        
        bool needUpdate = serverVersion > localVersion;
        Debug.Log($"本地版本: {localVersion}, 服务器版本: {serverVersion}, 需要热更: {needUpdate}");
        
        return needUpdate;
    }
} 
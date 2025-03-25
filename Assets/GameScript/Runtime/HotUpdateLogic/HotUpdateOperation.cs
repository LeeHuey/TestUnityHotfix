using UnityEngine;
using UniFramework.Machine;
using UniFramework.Event;
using YooAsset;

public class HotUpdateOperation : GameAsyncOperation
{
    private enum ESteps
    {
        None,
        Update,
        Done,
    }

    private readonly EventGroup _eventGroup = new EventGroup();
    private readonly StateMachine _machine;
    private ESteps _steps = ESteps.None;

    public HotUpdateOperation()
    {
        // 注册监听事件
        _eventGroup.AddListener<HotUpdateEventDefine.UserTryGetHotUpdateInfo>(OnHandleEventMessage);
        _eventGroup.AddListener<HotUpdateEventDefine.UserTryCheckNeedHotUpdate>(OnHandleEventMessage);
        _eventGroup.AddListener<HotUpdateEventDefine.UserTryDownloadHotUpdatePackage>(OnHandleEventMessage);
        _eventGroup.AddListener<HotUpdateEventDefine.UserTryExtractHotUpdatePackage>(OnHandleEventMessage);
        _eventGroup.AddListener<HotUpdateEventDefine.UserTryInitYooAssets>(OnHandleEventMessage);
        _eventGroup.AddListener<HotUpdateEventDefine.UserTryLoadHotUpdateDlls>(OnHandleEventMessage);
        _eventGroup.AddListener<HotUpdateEventDefine.UserTryRunHotUpdateCode>(OnHandleEventMessage);

        // 创建状态机
        _machine = new StateMachine(this);
        _machine.AddNode<FsmGetHotUpdateInfo>();
        _machine.AddNode<FsmCheckNeedHotUpdate>();
        _machine.AddNode<FsmDownloadHotUpdatePackage>();
        _machine.AddNode<FsmExtractHotUpdatePackage>();
        _machine.AddNode<FsmInitYooAssets>();
        _machine.AddNode<FsmLoadHotUpdateDlls>();
        _machine.AddNode<FsmRunHotUpdateCode>();
    }
    
    protected override void OnStart()
    {
        _steps = ESteps.Update;
        _machine.Run<FsmGetHotUpdateInfo>();
    }
    
    protected override void OnUpdate()
    {
        if (_steps == ESteps.None || _steps == ESteps.Done)
            return;

        if (_steps == ESteps.Update)
        {
            _machine.Update();
        }
    }
    
    protected override void OnAbort()
    {
    }

    public void SetFinish()
    {
        _steps = ESteps.Done;
        _eventGroup.RemoveAllListener();
        Status = EOperationStatus.Succeed;
        Debug.Log("Hot update operation done!");
    }

    /// <summary>
    /// 接收事件
    /// </summary>
    private void OnHandleEventMessage(IEventMessage message)
    {
        if (message is HotUpdateEventDefine.UserTryGetHotUpdateInfo)
        {
            _machine.ChangeState<FsmGetHotUpdateInfo>();
        }
        else if (message is HotUpdateEventDefine.UserTryCheckNeedHotUpdate)
        {
            _machine.ChangeState<FsmCheckNeedHotUpdate>();
        }
        else if (message is HotUpdateEventDefine.UserTryDownloadHotUpdatePackage)
        {
            _machine.ChangeState<FsmDownloadHotUpdatePackage>();
        }
        else if (message is HotUpdateEventDefine.UserTryExtractHotUpdatePackage)
        {
            _machine.ChangeState<FsmExtractHotUpdatePackage>();
        }
        else if (message is HotUpdateEventDefine.UserTryInitYooAssets)
        {
            _machine.ChangeState<FsmInitYooAssets>();
        }
        else if (message is HotUpdateEventDefine.UserTryLoadHotUpdateDlls)
        {
            _machine.ChangeState<FsmLoadHotUpdateDlls>();
        }
        else if (message is HotUpdateEventDefine.UserTryRunHotUpdateCode)
        {
            _machine.ChangeState<FsmRunHotUpdateCode>();
        }
        else
        {
            throw new System.NotImplementedException($"{message.GetType()}");
        }
    }
} 
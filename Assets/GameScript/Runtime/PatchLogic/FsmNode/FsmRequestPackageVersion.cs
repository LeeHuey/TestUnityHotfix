using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniFramework.Machine;
using YooAsset;

internal class FsmRequestPackageVersion : IStateNode
{
    private StateMachine _machine;

    void IStateNode.OnCreate(StateMachine machine)
    {
        _machine = machine;
    }
    void IStateNode.OnEnter()
    {
        PatchEventDefine.PatchStepsChange.SendEventMessage("请求资源版本 !");
        GameManager.Instance.StartCoroutine(UpdatePackageVersion());
    }
    void IStateNode.OnUpdate()
    {
    }
    void IStateNode.OnExit()
    {
    }

    private IEnumerator UpdatePackageVersion()
    {
        var packageName = (string)_machine.GetBlackboardValue("PackageName");
        var package = YooAssets.GetPackage(packageName);
        
        // 先获取本地版本号
        string localPackageVersion = package.GetPackageVersion();
        
        var operation = package.RequestPackageVersionAsync();
        yield return operation;

        if (operation.Status != EOperationStatus.Succeed)
        {
            Debug.LogWarning(operation.Error);
            PatchEventDefine.PackageVersionRequestFailed.SendEventMessage();
        }
        else
        {
            string remotePackageVersion = operation.PackageVersion;
            Debug.Log($"Request package version : {remotePackageVersion}, Local version: {localPackageVersion}");
            _machine.SetBlackboardValue("PackageVersion", remotePackageVersion);
            
            // 比较版本号，确定是否需要更新
            if (localPackageVersion == remotePackageVersion)
            {
                Debug.Log("已经是最新版本，无需更新");
                // 直接进入下一个状态，跳过更新manifest的步骤
                _machine.ChangeState<FsmStartGame>(); // 假设有这样一个状态表示不需要更新
            }
            else
            {
                // 版本不同，需要更新
                _machine.ChangeState<FsmUpdatePackageManifest>();
            }
        }
    }
}
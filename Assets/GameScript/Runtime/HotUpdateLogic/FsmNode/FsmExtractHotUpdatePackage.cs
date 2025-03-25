using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniFramework.Machine;
using System.IO;
using System.IO.Compression;
using System;

internal class FsmExtractHotUpdatePackage : IStateNode
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
        HotUpdateEventDefine.HotUpdateStepsChange.SendEventMessage("正在解压热更包...");
        _hotUpdateManager.StartCoroutine(ExtractHotUpdatePackage());
    }
    
    void IStateNode.OnUpdate()
    {
    }
    
    void IStateNode.OnExit()
    {
    }
    
    private IEnumerator ExtractHotUpdatePackage()
    {
        string zipPath = _hotUpdateManager.HotUpdateZipPath;
        Debug.Log($"开始解压热更包: {zipPath}");
        
        // 解压目标目录 - 确保与YooAssets的缓存目录一致
        string extractPath = _hotUpdateManager.GetYooAssetsSandboxPath();
        
        // 确保目录存在
        if (!Directory.Exists(extractPath))
        {
            Directory.CreateDirectory(extractPath);
        }
        
        // 在后台线程中解压
        yield return new WaitForThreadedTask(() =>
        {
            try
            {
                // 解压ZIP文件
                ZipFile.ExtractToDirectory(zipPath, extractPath, true);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"解压热更包失败: {e.Message}");
                return false;
            }
        });
        
        // 删除下载的ZIP文件
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }
        
        Debug.Log($"热更包解压完成，解压路径: {extractPath}");
        
        // 更新本地版本号
        _hotUpdateManager.LocalResVersion = _hotUpdateManager.HotUpdateInfo.current.resversion;
        PlayerPrefs.SetString("LocalResVersion", _hotUpdateManager.LocalResVersion);
        PlayerPrefs.Save();
        
        Debug.Log($"热更新完成，当前版本: {_hotUpdateManager.LocalResVersion}");
        
        // 进入初始化YooAssets状态
        _machine.ChangeState<FsmInitYooAssets>();
    }
}

// 后台线程任务帮助类
public class WaitForThreadedTask : CustomYieldInstruction
{
    private bool _isDone;
    private System.Func<bool> _action;

    public WaitForThreadedTask(System.Func<bool> action)
    {
        _action = action;
        _isDone = false;
        System.Threading.ThreadPool.QueueUserWorkItem(new System.Threading.WaitCallback(RunAction));
    }

    private void RunAction(object state)
    {
        _action();
        _isDone = true;
    }

    public override bool keepWaiting => !_isDone;
} 
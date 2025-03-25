using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniFramework.Machine;
using UnityEngine.Networking;
using System;

internal class FsmGetHotUpdateInfo : IStateNode
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
        HotUpdateEventDefine.HotUpdateStepsChange.SendEventMessage("正在获取热更信息...");
        _hotUpdateManager.StartCoroutine(GetHotUpdateInfo());
    }
    
    void IStateNode.OnUpdate()
    {
    }
    
    void IStateNode.OnExit()
    {
    }

    private IEnumerator GetHotUpdateInfo()
    {
        // 使用本地方法获取版本请求URL
        string versionRequestUrl = _hotUpdateManager.GetVersionRequestURL();
        Debug.Log($"开始获取热更信息，URL: {versionRequestUrl}");
        
        // 使用UnityWebRequest获取服务器返回的JSON信息
        UnityWebRequest webRequest = new UnityWebRequest(versionRequestUrl);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.timeout = 10;
        
        yield return webRequest.SendWebRequest();
        
        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"获取热更信息失败: {webRequest.error}");
            HotUpdateEventDefine.HotUpdateInfoRequestFailed.SendEventMessage();
            yield break;
        }
        
        string jsonText = webRequest.downloadHandler.text;
        Debug.Log($"获取热更信息成功: {jsonText}");
        
        // 解析JSON
        try
        {
            HotUpdateInfo hotUpdateInfo = JsonUtility.FromJson<HotUpdateInfo>(jsonText);
            Debug.Log($"解析热更信息成功, 服务器版本: {hotUpdateInfo.current.resversion}");
            
            // 保存热更信息到管理器
            _hotUpdateManager.HotUpdateInfo = hotUpdateInfo;
            
            // 切换到检查是否需要热更状态
            _machine.ChangeState<FsmCheckNeedHotUpdate>();
        }
        catch (Exception e)
        {
            Debug.LogError($"解析热更信息失败: {e.Message}");
            HotUpdateEventDefine.HotUpdateInfoRequestFailed.SendEventMessage();
        }
    }
} 
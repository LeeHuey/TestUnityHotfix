using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniFramework.Machine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Text;

internal class FsmDownloadHotUpdatePackage : IStateNode
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
        HotUpdateEventDefine.HotUpdateStepsChange.SendEventMessage("正在下载热更包...");
        _hotUpdateManager.StartCoroutine(DownloadHotUpdatePackage());
    }
    
    void IStateNode.OnUpdate()
    {
    }
    
    void IStateNode.OnExit()
    {
    }
    
    private IEnumerator DownloadHotUpdatePackage()
    {
        if (_hotUpdateManager.HotUpdateInfo == null || _hotUpdateManager.HotUpdateInfo.resources == null 
            || _hotUpdateManager.HotUpdateInfo.resources.Length == 0)
        {
            Debug.LogError("热更资源信息为空，无法下载热更包");
            HotUpdateEventDefine.HotUpdateInfoRequestFailed.SendEventMessage();
            yield break;
        }
        
        // 获取热更包信息
        var resource = _hotUpdateManager.HotUpdateInfo.resources[0];
        string zipFileName = resource.file;
        string zipMd5 = resource.md5;
        long zipSize = long.Parse(resource.size);
        string downloadUrl = _hotUpdateManager.HotUpdateInfo.host + zipFileName;
        
        Debug.Log($"开始下载热更包: {downloadUrl}, MD5: {zipMd5}, 大小: {zipSize}字节");
        
        // 下载热更zip包
        string tempDownloadPath = Path.Combine(Application.persistentDataPath, zipFileName);
        
        // 使用UnityWebRequest下载热更包
        UnityWebRequest request = new UnityWebRequest(downloadUrl);
        request.downloadHandler = new DownloadHandlerFile(tempDownloadPath);
        request.timeout = 60;
        
        // 开始下载
        var operation = request.SendWebRequest();
        
        // 监控下载进度
        while (!operation.isDone)
        {
            float progress = request.downloadProgress;
            string progressText = $"下载热更包进度: {progress:P2}";
            HotUpdateEventDefine.HotUpdateStepsChange.SendEventMessage(progressText);
            Debug.Log(progressText);
            yield return null;
        }
        
        // 检查下载结果
        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"下载热更包失败: {request.error}");
            HotUpdateEventDefine.HotUpdateInfoRequestFailed.SendEventMessage();
            yield break;
        }
        
        Debug.Log($"热更包下载完成: {tempDownloadPath}");
        
        // 验证MD5
        byte[] zipData = File.ReadAllBytes(tempDownloadPath);
        string downloadedMd5 = CalculateMD5(zipData);
        
        if (downloadedMd5 != zipMd5.ToLower())
        {
            Debug.LogError($"热更包MD5验证失败, 期望: {zipMd5}, 实际: {downloadedMd5}");
            HotUpdateEventDefine.HotUpdateInfoRequestFailed.SendEventMessage();
            yield break;
        }
        
        // 复制到标准路径
        string zipPath = Path.Combine(Application.persistentDataPath, "hotupdate.zip");
        File.Copy(tempDownloadPath, zipPath, true);
        
        // 保存热更包路径到管理器
        _hotUpdateManager.HotUpdateZipPath = zipPath;
        
        // 进入解压热更包状态
        _machine.ChangeState<FsmExtractHotUpdatePackage>();
    }
    
    // 计算MD5
    private string CalculateMD5(byte[] data)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            var hash = md5.ComputeHash(data);
            var sb = new StringBuilder();
            
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }
            
            return sb.ToString();
        }
    }
} 
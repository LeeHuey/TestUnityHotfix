using UniFramework.Event;

public static class HotUpdateEventDefine
{
    /// <summary>
    /// 热更流程步骤改变
    /// </summary>
    public class HotUpdateStepsChange : IEventMessage
    {
        public static void SendEventMessage(string step)
        {
            var msg = new HotUpdateStepsChange();
            msg.Step = step;
            UniEvent.SendMessage(msg);
        }
        public string Step;
    }
    
    /// <summary>
    /// 热更信息获取失败
    /// </summary>
    public class HotUpdateInfoRequestFailed : IEventMessage
    {
        public static void SendEventMessage()
        {
            var msg = new HotUpdateInfoRequestFailed();
            UniEvent.SendMessage(msg);
        }
    }
    
    /// <summary>
    /// 获取热更信息
    /// </summary>
    public class UserTryGetHotUpdateInfo : IEventMessage
    {
        public static void SendEventMessage()
        {
            var msg = new UserTryGetHotUpdateInfo();
            UniEvent.SendMessage(msg);
        }
    }
    
    /// <summary>
    /// 检查是否需要热更
    /// </summary>
    public class UserTryCheckNeedHotUpdate : IEventMessage
    {
        public static void SendEventMessage()
        {
            var msg = new UserTryCheckNeedHotUpdate();
            UniEvent.SendMessage(msg);
        }
    }
    
    /// <summary>
    /// 下载热更包
    /// </summary>
    public class UserTryDownloadHotUpdatePackage : IEventMessage
    {
        public static void SendEventMessage()
        {
            var msg = new UserTryDownloadHotUpdatePackage();
            UniEvent.SendMessage(msg);
        }
    }
    
    /// <summary>
    /// 解压热更包
    /// </summary>
    public class UserTryExtractHotUpdatePackage : IEventMessage
    {
        public static void SendEventMessage()
        {
            var msg = new UserTryExtractHotUpdatePackage();
            UniEvent.SendMessage(msg);
        }
    }
    
    /// <summary>
    /// 初始化YooAssets
    /// </summary>
    public class UserTryInitYooAssets : IEventMessage
    {
        public static void SendEventMessage()
        {
            var msg = new UserTryInitYooAssets();
            UniEvent.SendMessage(msg);
        }
    }
    
    /// <summary>
    /// 加载热更DLL
    /// </summary>
    public class UserTryLoadHotUpdateDlls : IEventMessage
    {
        public static void SendEventMessage()
        {
            var msg = new UserTryLoadHotUpdateDlls();
            UniEvent.SendMessage(msg);
        }
    }
    
    /// <summary>
    /// 运行热更代码
    /// </summary>
    public class UserTryRunHotUpdateCode : IEventMessage
    {
        public static void SendEventMessage()
        {
            var msg = new UserTryRunHotUpdateCode();
            UniEvent.SendMessage(msg);
        }
    }
} 
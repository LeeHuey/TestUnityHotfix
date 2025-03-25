[System.Serializable]
public class HotUpdateInfo
{
    public Current current;
    public Resource[] resources;
    public string host;
    public string updateurl;
    public CfgData cfg_data;
    public int apk_version;
    public string voice_url_pre;
    public string voice_upload_url;
    public string tcp_cfg;
    public Rate rate;
    public string battle_url_pre;
    public Maintain maintain;
    public int isinreview;
    public string alipay_url;
    public string wechatpay_url;
    public string quickpay_callback_url;
    public int pay_test;
    public int is_999;
}

[System.Serializable]
public class Current
{
    public string coreversion;
    public string cppversion;
    public string resversion;
}

[System.Serializable]
public class Resource
{
    public string file;
    public string md5;
    public string size;
}

[System.Serializable]
public class CfgData
{
    public int version;
    public string md5;
    public string size;
    public string url;
}

[System.Serializable]
public class Rate
{
    public int show;
}

[System.Serializable]
public class Maintain
{
    public int state;
    public int end;
}
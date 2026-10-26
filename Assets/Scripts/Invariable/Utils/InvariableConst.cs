namespace Invariable
{
    public static class InvariableConst
    {
        #region 事件
        public const string Event_Launcher_ShowTips = "Launcher_ShowTips";
        public const string Event_Launcher_ShowProgress = "Launcher_ShowProgress";
        public const string Event_Launcher_StartGame = "Launcher_StartGame";
        #endregion

        #region 计时器
        public const string Timer_Config_TickEvict = "Config_TickEvict";
        public const string Timer_YooAsset_TickEvict = "YooAsset_TickEvict";
        public const string Timer_CloudManager_UploadDebounce = "CloudManager_UploadDebounce";
        #endregion

        #region 游戏资源
        public static readonly string[] AotDllNames =
        {
            "mscorlib",
            "System",
            "System.Core",
            "Newtonsoft.Json",
        };
        public const string YooAssetPackageName = "MyPackage";
        public const string CDNPathDouYin = "https://a.unity.cn/client_api/v1/buckets/b24fd050-49f0-4fa8-9c6a-6d4cf4fc99c6/release_by_badge/latest/content";
        public const string CDNPathWeChat = "https://a.unity.cn/client_api/v1/buckets/ec8d502c-f400-4941-b4f1-aac2d1fccf23/release_by_badge/latest/content";
        public const string EncryptKey = "95gbt368426hyb13";
        public const string EncryptIv = "i8g3451h5cxmj6rf";
        public const string UIRootPath = "UI_Root";
        public const string UICameraPath_0 = "UI_Root/Canvas_0/UI_Camera";
        public const string UICameraPath_1 = "UI_Root/Canvas_1/UI_Camera";
        public const string UICameraPath_2 = "UI_Root/Canvas_2/UI_Camera";
        public const string UICameraPath_3 = "UI_Root/Canvas_3/UI_Camera";
        public const string UIPanelPath_0 = "UI_Root/Canvas_0/Ts_Panel";
        public const string UIPanelPath_1 = "UI_Root/Canvas_1/Ts_Panel";
        public const string UIPanelPath_2 = "UI_Root/Canvas_2/Ts_Panel";
        public const string UIPanelPath_3 = "UI_Root/Canvas_3/Ts_Panel";
        public const string HotUpdatePanelPath = "UI_Root/Canvas_0/Ts_Panel/HotUpdatePanel";
        public const string PoolParentName = "PoolParent";
        #endregion

        #region 游戏配置
        public const string RewardedVideoAdUnitId = "";
        public const string ShareGameTitle = "MeteorWish(游戏名称——流星之愿)";
        #endregion

        #region 音频
        public const string LocalKey_AudioMasterVolume = "Audio_MasterVolume";
        public const string LocalKey_AudioBgmVolume = "Audio_BgmVolume";
        public const string LocalKey_AudioSfxVolume = "Audio_SfxVolume";
        public const string LocalKey_AudioMute = "Audio_Mute";
        #endregion
    }
}
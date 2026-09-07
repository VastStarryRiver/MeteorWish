using CloudService;
using DG.Tweening;
using Invariable;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;



namespace HotUpdate
{
    public class MainPanel : UIPanel
    {
        public RectTransform m_tsTest;
        public TextMeshProUGUI m_textTest;
        public Image m_imgTest;
        public UIButton m_btnAuth;
        public AudioClip m_audioBGM;

        private static bool m_hasReportedRank;



        private void Awake()
        {
            GameManager.Instance.InvokeEventCallBack<object>(InvariableConst.Event_Launcher_StartGame, null); // 销毁热更新面板
        }

        private void OnEnable()
        {
            m_btnAuth.gameObject.SetActive(false);
            ShowAuthButton();
        }

        private void Start()
        {
            PlayBGM();
            PlayBtnAnim();
        }

        private void OnDisable()
        {
            HideAuthButton();
        }



        /// <summary>
        /// 播放背景音乐
        /// </summary>
        private void PlayBGM()
        {
            AudioManager.Instance.PlayBGM(m_audioBGM);
        }

        /// <summary>
        /// 播放开始游戏按钮的动画
        /// </summary>
        private void PlayBtnAnim()
        {
            m_tsTest.DOAnchorPos(Vector2.zero, 1f).SetTarget(m_tsTest).SetEase(Ease.InSine).OnComplete(() =>
            {
                m_tsTest.DOAnchorPos(new Vector2(0, -500), 1f).SetTarget(m_tsTest).SetEase(Ease.OutSine);
            });
        }

        /// <summary>
        /// 同步平台资料，由状态回调控制授权按钮显隐
        /// </summary>
        private void ShowAuthButton()
        {
            SdkManager.Instance.SyncPlatformUserInfo((RectTransform)m_btnAuth.transform, OnAuthState);
        }

        /// <summary>
        /// 隐藏授权锚点并销毁平台原生授权按钮
        /// </summary>
        private void HideAuthButton()
        {
            m_btnAuth.gameObject.SetActive(false);
            SdkManager.Instance.DestroyPlatformUserInfoButton();
        }

        /// <summary>
        /// 本局仅上报一次排行榜数据，之后不再上报
        /// </summary>
        private void ReportRankScore()
        {
            if (m_hasReportedRank)
            {
                return;
            }

            m_hasReportedRank = true;

            string scoreText = SdkManager.Instance.GetCloudData("Score", "0");

            if (!double.TryParse(scoreText, out double score))
            {
                return;
            }

            CloudManager.Instance.ReportRankScore("Score", score);
        }



        /// <summary>
        /// 测试功能1
        /// </summary>
        public void OnTestClick1()
        {
            string str = SdkManager.Instance.GetCloudData("Test1", "");

            if (string.IsNullOrEmpty(str))
            {
                SdkManager.Instance.SetCloudData("Test1", "测试数据1");
                m_textTest.text = "写入测试数据1";
            }
            else
            {
                m_textTest.text = str;
            }
        }

        /// <summary>
        /// 测试功能2
        /// </summary>
        public void OnTestClick2()
        {
            SdkManager.Instance.SetCloudData("Score", "100");
            ReportRankScore();
            m_textTest.text = "上传排行榜积分";
        }

        /// <summary>
        /// 测试功能3
        /// </summary>
        public void OnTestClick3()
        {
            ConfigManager.GetRoleRuneByID(11, (config) =>
            {
                m_textTest.text = config.Param.ToString();
            });
        }

        /// <summary>
        /// 测试功能4
        /// </summary>
        public void OnTestClick4()
        {
            CloudManager.Instance.GetRankList("Score", CloudRankTypes.World, (list) =>
            {
                if (this == null || m_textTest == null)
                {
                    return;
                }

                StringBuilder str = new StringBuilder();
                str.Append("排行榜数据如下：");

                if (list == null)
                {
                    m_textTest.text = str.ToString();

                    return;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    string score = "";
                    string nickName = "";
                    string avatarUrl = "";

                    if (list[i] != null)
                    {
                        nickName = list[i].NickName ?? "";
                        avatarUrl = list[i].AvatarUrl ?? "";

                        if (list[i].Data != null)
                        {
                            list[i].Data.TryGetValue("Score", out score);
                        }
                    }

                    str.Append($"\n序号：{i}\n积分：{score}\n昵称：{nickName}");

                    if (i == 0 && !string.IsNullOrEmpty(avatarUrl))
                    {
                        Utils.SetRemoteImage(m_imgTest, "", avatarUrl, false, null);
                    }
                }
                m_textTest.text = str.ToString();
            });
        }

        /// <summary>
        /// 授权按钮点击，发起平台授权
        /// </summary>
        public void OnAuthClick()
        {
            SdkManager.Instance.RequestPlatformUserInfoAuth((RectTransform)m_btnAuth.transform, OnAuthState);
        }

        /// <summary>
        /// 平台授权状态回调，仅未授权时显示按钮，已授权时上报一次排行榜，拒绝时销毁原生按钮
        /// </summary>
        private void OnAuthState(PlatformAuthStates state)
        {
            m_btnAuth.gameObject.SetActive(state == PlatformAuthStates.NotAuthorized);

            if (state == PlatformAuthStates.Authorized)
            {
                ReportRankScore();
            }
            else if (state == PlatformAuthStates.Denied)
            {
                SdkManager.Instance.DestroyPlatformUserInfoButton();
                // TODO 拒绝后的其它业务逻辑
            }
        }
    }
}
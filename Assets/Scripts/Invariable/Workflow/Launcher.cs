using UnityEngine;
using YooAsset;



namespace Invariable
{
    public class Launcher : MonoBehaviour
    {
        private EPlayMode m_playMode;
        private GameLoadingPanel m_hotUpdatePanel = null;



        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

#if UNITY_EDITOR
            m_playMode = EPlayMode.EditorSimulateMode;
#else
            m_playMode = EPlayMode.WebPlayMode;
#endif

            InitPoolParent();

            Utils.CreateManagerInstance<GameManager>();
            Utils.CreateManagerInstance<AudioManager>(typeof(AudioListener));
        }

        private void OnEnable()
        {
            GameManager.Instance.AddEventListener<string>(InvariableConst.Event_Launcher_ShowTips, ShowTips);
            GameManager.Instance.AddEventListener<DownloadProgressInfo>(InvariableConst.Event_Launcher_ShowProgress, ShowProgress);
            GameManager.Instance.AddEventListener<object>(InvariableConst.Event_Launcher_StartGame, StartGame);
        }

        private void Start()
        {
            StateMachine stateMachine = new StateMachine(this);

            stateMachine.AddNode<InitializeYooAsset>();
            stateMachine.AddNode<CheckCatalogUpdate>();
            stateMachine.AddNode<CheckResourceUpdates>();
            stateMachine.AddNode<HotUpdateOver>();

            stateMachine.SetBlackboardValue("EPlayMode", m_playMode);

            SetDontDestroyOnLoad(InvariableConst.UIRootPath);

            ShowHotUpdatePanel();

            stateMachine.Play<InitializeYooAsset>();
        }

        private void OnDisable()
        {
            GameManager.Instance.RemoveEventListener<string>(InvariableConst.Event_Launcher_ShowTips, ShowTips);
            GameManager.Instance.RemoveEventListener<DownloadProgressInfo>(InvariableConst.Event_Launcher_ShowProgress, ShowProgress);
            GameManager.Instance.RemoveEventListener<object>(InvariableConst.Event_Launcher_StartGame, StartGame);
        }



        /// <summary>
        /// 初始化对象池根节点
        /// </summary>
        private void InitPoolParent()
        {
            GameObject go = GameObject.Find(InvariableConst.PoolParentName);

            if (go == null)
            {
                go = new GameObject(InvariableConst.PoolParentName);
                SetDontDestroyOnLoad(InvariableConst.PoolParentName);
            }

            PoolUtils.SetPoolParent(go.transform);
        }

        /// <summary>
        /// 展示热更新面板
        /// </summary>
        private void ShowHotUpdatePanel()
        {
            GameObject go = GameObject.Find(InvariableConst.HotUpdatePanelPath);

            if (go == null)
            {
                Transform parent = GameObject.Find(InvariableConst.UIPanelPath_0).transform;
                GameObject asset = Resources.Load<GameObject>("LocalAssets/HotUpdatePanel");
                go = GameObject.Instantiate<GameObject>(asset, Vector3.zero, Quaternion.identity, parent);
                go.name = "HotUpdatePanel";
            }

            m_hotUpdatePanel = go.GetComponent<GameLoadingPanel>();
        }

        /// <summary>
        /// 设置跨场景常驻
        /// </summary>
        private void SetDontDestroyOnLoad(string name)
        {
            GameObject go = GameObject.Find(name);

            if (go != null)
            {
                DontDestroyOnLoad(go);
            }
        }

        /// <summary>
        /// 显示热更提示文本
        /// </summary>
        private void ShowTips(string tips)
        {
            m_hotUpdatePanel.SetDes(tips);
        }

        /// <summary>
        /// 显示热更进度
        /// </summary>
        private void ShowProgress(DownloadProgressInfo progress)
        {
            m_hotUpdatePanel.SetProgress(progress.CurrentBytes, progress.TotalBytes);
        }

        /// <summary>
        /// 热更完成并进入游戏
        /// </summary>
        private void StartGame(object arg)
        {
            GameObject.Destroy(m_hotUpdatePanel.gameObject);
            GameObject.Destroy(this);
        }
    }
}
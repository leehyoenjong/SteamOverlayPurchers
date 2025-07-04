using UnityEngine;
using System.Threading.Tasks;

namespace SteamPaymentSystem
{
#if STEAMWORKS_NET

    /// <summary>
    /// Steam 결제 시스템 자동 초기화 클래스 (독립 버전)
    /// 게임 시작 시 필요한 시스템들을 자동으로 초기화
    /// </summary>
    public class SteamPaymentInitializer : MonoBehaviour
    {
        [Header("설정")]
        [Tooltip("Steam 결제 시스템 설정 파일")]
        public SteamPaymentSettings paymentSettings;
        
        [Tooltip("커스텀 데이터 제공자 (선택사항)")]
        [SerializeField] private MonoBehaviour customDataProviderComponent;
        
        private ISteamPaymentDataProvider customDataProvider;

        [Header("초기화 옵션")]
        public bool autoInitializeOnStart = true;
        public bool loadInAppDataOnStart = true;
        public float initializationDelay = 1.0f;

        public static SteamPaymentInitializer instance;
        private static bool isInitialized = false;

        void Awake()
        {
            instance = this;
            
            // 커스텀 데이터 제공자 확인
            if (customDataProviderComponent is ISteamPaymentDataProvider provider)
            {
                customDataProvider = provider;
            }
        }

        async void Start()
        {
            if (autoInitializeOnStart)
            {
                await InitializeAsync();
            }
        }        /// <summary>
        /// Steam 결제 시스템 초기화
        /// </summary>
        public async Task InitializeAsync()
        {
            if (isInitialized)
            {
                if (paymentSettings?.enableDebugLogs == true)
                {
                    Debug.Log("Steam 결제 시스템이 이미 초기화되었습니다.");
                }
                return;
            }

            if (paymentSettings == null)
            {
                Debug.LogError("SteamPaymentSettings가 설정되지 않았습니다!");
                return;
            }

            if (paymentSettings.enableDebugLogs)
            {
                Debug.Log("Steam 결제 시스템 초기화 시작...");
            }

            // Steam 초기화 대기
            await WaitForSteamInitializationAsync();

            // 초기화 지연 시간 대기
            if (initializationDelay > 0)
            {
                await Task.Delay((int)(initializationDelay * 1000));
            }

            try
            {
                // Steam Inventory Manager 초기화
                SteamInventoryManager.Initialize(paymentSettings);

                // Steam Payment Manager 초기화
                SteamPaymentManager.Initialize(paymentSettings, customDataProvider);

                // 인앱 아이템 데이터 로드
                if (loadInAppDataOnStart)
                {
                    bool loadSuccess = await SteamPaymentManager.LoadInAppItemDataAsync();
                    if (!loadSuccess && paymentSettings.enableDebugLogs)
                    {
                        Debug.LogWarning("인앱 아이템 데이터 로드에 실패했습니다.");
                    }
                }

                isInitialized = true;
                
                if (paymentSettings.enableDebugLogs)
                {
                    Debug.Log("✅ Steam 결제 시스템 초기화 완료!");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Steam 결제 시스템 초기화 실패: {e.Message}");
            }
        }        /// <summary>
        /// Steam 초기화 대기
        /// </summary>
        private async Task WaitForSteamInitializationAsync()
        {
            int maxWaitTime = 30000; // 30초 최대 대기
            int elapsedTime = 0;
            int checkInterval = 100; // 100ms마다 체크

            while (!SteamManager.Initialized && elapsedTime < maxWaitTime)
            {
                await Task.Delay(checkInterval);
                elapsedTime += checkInterval;
            }

            if (!SteamManager.Initialized)
            {
                Debug.LogWarning("Steam 초기화가 완료되지 않았습니다. Steam 기능이 제한될 수 있습니다.");
            }
            else if (paymentSettings?.enableDebugLogs == true)
            {
                Debug.Log("Steam 초기화 완료 확인됨");
            }
        }

        /// <summary>
        /// 수동 초기화 (외부에서 호출 가능)
        /// </summary>
        public static async Task<bool> ManualInitializeAsync()
        {
            var initializer = FindObjectOfType<SteamPaymentInitializer>();
            if (initializer == null)
            {
                Debug.LogError("SteamPaymentInitializer 컴포넌트를 찾을 수 없습니다.");
                return false;
            }

            try
            {
                await initializer.InitializeAsync();
                return isInitialized;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"수동 초기화 실패: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 초기화 상태 확인
        /// </summary>
        public static bool IsInitialized()
        {
            return isInitialized;
        }        /// <summary>
        /// 시스템 상태 확인
        /// </summary>
        public void CheckSystemStatus()
        {
            Debug.Log("=== Steam Payment System Status ===");
            Debug.Log($"Steam 초기화: {SteamManager.Initialized}");
            Debug.Log($"결제 시스템 초기화: {isInitialized}");
            Debug.Log($"설정 파일: {(paymentSettings != null ? "설정됨" : "미설정")}");
            Debug.Log($"커스텀 데이터 제공자: {(customDataProvider != null ? "설정됨" : "미설정")}");

            var availableItems = SteamPaymentManager.GetAvailableItems();
            Debug.Log($"사용 가능한 인앱 아이템: {availableItems.Count}개");
        }

        /// <summary>
        /// 시스템 재초기화
        /// </summary>
        public async void ReinitializeSystem()
        {
            Debug.Log("Steam 결제 시스템 재초기화 시작...");
            isInitialized = false;
            await InitializeAsync();
        }

        /// <summary>
        /// 커스텀 데이터 제공자 설정
        /// </summary>
        public void SetCustomDataProvider(ISteamPaymentDataProvider provider)
        {
            customDataProvider = provider;
            
            if (isInitialized)
            {
                SteamPaymentManager.SetDataProvider(provider);
            }
        }
    }

#else

    /// <summary>
    /// Steam 결제 시스템 자동 초기화 클래스 (더미 클래스)
    /// </summary>
    public class SteamPaymentInitializer : MonoBehaviour
    {
        [Header("설정")]
        public SteamPaymentSettings paymentSettings;
        
        [Header("초기화 옵션")]
        public bool autoInitializeOnStart = true;
        public bool loadInAppDataOnStart = true;
        public float initializationDelay = 1.0f;

        void Start()
        {
            if (autoInitializeOnStart)
            {
                Debug.LogWarning("Steamworks.NET이 설치되지 않았습니다. Steam 결제 시스템을 사용할 수 없습니다.");
            }
        }

        public static bool IsInitialized() { return false; }
        
        public void CheckSystemStatus()
        {
            Debug.Log("=== Steam Payment System Status (더미) ===");
            Debug.Log("Steamworks.NET이 설치되지 않았습니다.");
        }

        public static System.Threading.Tasks.Task<bool> ManualInitializeAsync()
        {
            return System.Threading.Tasks.Task.FromResult(false);
        }
    }

#endif
}
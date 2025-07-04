using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace SteamPaymentSystem
{
#if STEAMWORKS_NET
    using Steamworks;

    /// <summary>
    /// Steam 결제 시스템 메인 매니저 (독립 버전)
    /// Steam Inventory Service를 이용한 인앱 결제 처리
    /// </summary>
    public class SteamPaymentManager : MonoBehaviour
    {
        // 결제 상태 열거형
        public enum PaymentState
        {
            Idle,
            LoadingItemData,
            ProcessingPayment,
            ProcessingReward,
            Completed,
            Failed
        }

        // 결제 이벤트
        public static event Action<int, bool, string> OnPaymentCompleted; // steamItemDefId, success, message
        public static event Action<PaymentState> OnPaymentStateChanged;

        // 현재 결제 상태
        public static PaymentState CurrentState { get; private set; } = PaymentState.Idle;

        // 인앱 아이템 데이터 캐시
        private static Dictionary<int, SteamInAppItem> inAppItemDataCache = new Dictionary<int, SteamInAppItem>();
        private static bool isItemDataLoaded = false;

        // 중복 지급 방지를 위한 처리 중인 아이템 ID 추적
        private static HashSet<int> processingItemIds = new HashSet<int>();

        // Steam 관련 콜백
        private static Callback<MicroTxnAuthorizationResponse_t> m_MicroTxnAuthorizationResponse;

        // 데이터 제공자 및 설정
        private static ISteamPaymentDataProvider dataProvider;
        private static SteamPaymentSettings settings;
        private static SteamPaymentManager instance;

        // 결제 완료 대기용 TaskCompletionSource
        private static Dictionary<int, TaskCompletionSource<bool>> pendingPurchases = new Dictionary<int, TaskCompletionSource<bool>>();

        /// <summary>
        /// 초기화 (외부에서 호출)
        /// </summary>
        public static void Initialize(SteamPaymentSettings paymentSettings, ISteamPaymentDataProvider provider = null)
        {
            settings = paymentSettings;
            dataProvider = provider ?? new LocalTestDataProvider(paymentSettings);
            
            if (settings.enableDebugLogs)
            {
                Debug.Log("SteamPaymentManager 초기화 완료");
            }
        }        void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializePaymentSystem();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void OnDestroy()
        {
            CleanupPaymentSystem();
        }

        /// <summary>
        /// 결제 시스템 초기화
        /// </summary>
        private static void InitializePaymentSystem()
        {
            try
            {
                // Steam 콜백 등록
                if (SteamManager.Initialized)
                {
                    m_MicroTxnAuthorizationResponse = Callback<MicroTxnAuthorizationResponse_t>.Create(OnMicroTxnAuthorizationResponse);
                    
                    if (settings?.enableDebugLogs == true)
                    {
                        Debug.Log("Steam 결제 시스템 초기화 완료");
                    }
                }

                // Steam Inventory Manager 이벤트 구독
                SteamInventoryManager.OnInventoryLoaded += OnInventoryLoaded;
                SteamInventoryManager.OnInventoryUpdated += OnInventoryUpdated;
            }
            catch (Exception e)
            {
                Debug.LogError($"Steam 결제 시스템 초기화 실패: {e.Message}");
            }
        }        /// <summary>
        /// 결제 시스템 정리
        /// </summary>
        private static void CleanupPaymentSystem()
        {
            m_MicroTxnAuthorizationResponse?.Dispose();
            SteamInventoryManager.OnInventoryLoaded -= OnInventoryLoaded;
            SteamInventoryManager.OnInventoryUpdated -= OnInventoryUpdated;

            inAppItemDataCache.Clear();
            isItemDataLoaded = false;

            // 처리 중인 아이템 목록도 초기화
            processingItemIds.Clear();

            SetPaymentState(PaymentState.Idle);
            
            if (settings?.enableDebugLogs == true)
            {
                Debug.Log("Steam 결제 시스템 정리 완료");
            }
        }

        /// <summary>
        /// 인앱 아이템 데이터 로드
        /// </summary>
        public static async Task<bool> LoadInAppItemDataAsync()
        {
            if (isItemDataLoaded)
            {
                if (settings?.enableDebugLogs == true)
                {
                    Debug.Log("인앱 아이템 데이터가 이미 로드되어 있습니다.");
                }
                return true;
            }

            if (dataProvider == null)
            {
                Debug.LogError("DataProvider가 설정되지 않았습니다.");
                return false;
            }            SetPaymentState(PaymentState.LoadingItemData);

            try
            {
                var result = await dataProvider.LoadInAppItemDataAsync();
                
                if (result.success)
                {
                    inAppItemDataCache = result.items;
                    isItemDataLoaded = true;
                    SetPaymentState(PaymentState.Idle);
                    
                    if (settings?.enableDebugLogs == true)
                    {
                        Debug.Log($"인앱 아이템 데이터 로드 완료: {inAppItemDataCache.Count}개 아이템");
                    }
                    return true;
                }
                else
                {
                    Debug.LogError("인앱 아이템 데이터 로드 실패");
                    SetPaymentState(PaymentState.Failed);
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"인앱 아이템 데이터 로드 오류: {e.Message}");
                SetPaymentState(PaymentState.Failed);
                return false;
            }
        }

        /// <summary>
        /// 아이템 구매
        /// </summary>
        public static async Task<bool> PurchaseItemAsync(int steamItemDefId)
        {
            if (!isItemDataLoaded)
            {
                Debug.LogError("인앱 아이템 데이터가 로드되지 않았습니다. LoadInAppItemDataAsync()를 먼저 호출해주세요.");
                return false;
            }            if (!inAppItemDataCache.ContainsKey(steamItemDefId))
            {
                Debug.LogError($"존재하지 않는 아이템 ID: {steamItemDefId}");
                return false;
            }

            if (processingItemIds.Contains(steamItemDefId))
            {
                Debug.LogWarning($"이미 처리 중인 아이템: {steamItemDefId}");
                return false;
            }

            var item = inAppItemDataCache[steamItemDefId];
            
            // 중복 구매 방지 체크
            if (item.preventDuplicatePurchase && dataProvider != null)
            {
                bool hasHistory = await dataProvider.HasPurchaseHistoryAsync(steamItemDefId);
                if (hasHistory)
                {
                    Debug.LogWarning($"이미 구매한 아이템입니다: {steamItemDefId}");
                    return false;
                }
            }

            processingItemIds.Add(steamItemDefId);
            SetPaymentState(PaymentState.ProcessingPayment);

            try
            {
                // Steam 결제 시작
                var purchaseResult = await StartSteamPurchaseAsync(steamItemDefId);
                
                if (purchaseResult)
                {
                    // 보상 지급
                    await ProcessPurchaseRewardAsync(steamItemDefId);
                    return true;
                }
                else
                {
                    Debug.LogError($"Steam 결제 실패: {steamItemDefId}");
                    return false;
                }
            }            catch (Exception e)
            {
                Debug.LogError($"결제 처리 중 오류 발생: {e.Message}");
                return false;
            }
            finally
            {
                processingItemIds.Remove(steamItemDefId);
            }
        }

        /// <summary>
        /// Steam 구매 시작
        /// </summary>
        private static async Task<bool> StartSteamPurchaseAsync(int steamItemDefId)
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogError("Steam이 초기화되지 않았습니다.");
                return false;
            }

            try
            {
                var tcs = new TaskCompletionSource<bool>();
                pendingPurchases[steamItemDefId] = tcs;

                // Steam Inventory의 StartPurchase 사용
                var itemDef = new SteamItemDef_t((uint)steamItemDefId);
                var result = SteamInventory.StartPurchase(new SteamItemDef_t[] { itemDef }, new uint[] { 1 }, 1);

                if (result != SteamAPICall_t.Invalid)
                {
                    if (settings?.enableDebugLogs == true)
                    {
                        Debug.Log($"Steam 구매 요청 전송: {steamItemDefId}");
                    }

                    // 결제 완료 대기 (최대 60초)
                    var timeoutTask = Task.Delay(60000);
                    var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                    if (completedTask == tcs.Task)
                    {
                        return await tcs.Task;
                    }                    else
                    {
                        Debug.LogError("Steam 결제 타임아웃");
                        return false;
                    }
                }
                else
                {
                    Debug.LogError("Steam 구매 요청 실패");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Steam 구매 시작 오류: {e.Message}");
                return false;
            }
            finally
            {
                pendingPurchases.Remove(steamItemDefId);
            }
        }

        /// <summary>
        /// 구매 보상 처리
        /// </summary>
        private static async Task ProcessPurchaseRewardAsync(int steamItemDefId)
        {
            SetPaymentState(PaymentState.ProcessingReward);

            try
            {
                var item = inAppItemDataCache[steamItemDefId];
                
                // 보상 지급
                if (dataProvider != null && item.rewards.Count > 0)
                {
                    bool rewardSuccess = await dataProvider.GrantRewardsAsync(item.rewards);
                    
                    if (rewardSuccess)
                    {
                        // 구매 이력 저장
                        var purchaseData = new PurchaseData(
                            steamItemDefId,
                            DateTime.Now,
                            Guid.NewGuid().ToString(),
                            item.rewards
                        );                        await dataProvider.SavePurchaseHistoryAsync(steamItemDefId, purchaseData);
                        
                        if (settings?.enableDebugLogs == true)
                        {
                            Debug.Log($"보상 지급 및 구매 이력 저장 완료: {steamItemDefId}");
                        }
                    }
                }

                SetPaymentState(PaymentState.Completed);
                OnPaymentCompleted?.Invoke(steamItemDefId, true, "구매 완료");
            }
            catch (Exception e)
            {
                Debug.LogError($"보상 처리 중 오류: {e.Message}");
                SetPaymentState(PaymentState.Failed);
                OnPaymentCompleted?.Invoke(steamItemDefId, false, $"보상 처리 실패: {e.Message}");
            }
        }

        /// <summary>
        /// Steam 결제 응답 콜백
        /// </summary>
        private static void OnMicroTxnAuthorizationResponse(MicroTxnAuthorizationResponse_t pCallback)
        {
            if (settings?.enableDebugLogs == true)
            {
                Debug.Log($"Steam 결제 응답: OrderID={pCallback.m_ulOrderID}, Authorized={pCallback.m_bAuthorized}");
            }

            // 대기 중인 구매 요청 처리
            foreach (var kvp in pendingPurchases.ToList())
            {
                kvp.Value.SetResult(pCallback.m_bAuthorized == 1);
            }
        }

        /// <summary>
        /// 인벤토리 로드 완료 콜백
        /// </summary>
        private static void OnInventoryLoaded(bool success)
        {
            if (settings?.enableDebugLogs == true)
            {
                Debug.Log($"Steam 인벤토리 로드 완료: {success}");
            }
        }        /// <summary>
        /// 인벤토리 업데이트 콜백
        /// </summary>
        private static void OnInventoryUpdated(SteamItemDef_t[] itemDefs, uint[] quantities)
        {
            if (settings?.enableDebugLogs == true)
            {
                Debug.Log($"Steam 인벤토리 업데이트: {itemDefs.Length}개 아이템");
            }
        }

        /// <summary>
        /// 결제 상태 설정
        /// </summary>
        private static void SetPaymentState(PaymentState newState)
        {
            if (CurrentState != newState)
            {
                CurrentState = newState;
                OnPaymentStateChanged?.Invoke(newState);
            }
        }

        /// <summary>
        /// 사용 가능한 아이템 목록 반환
        /// </summary>
        public static Dictionary<int, SteamInAppItem> GetAvailableItems()
        {
            return new Dictionary<int, SteamInAppItem>(inAppItemDataCache);
        }

        /// <summary>
        /// 구매 가능 여부 확인
        /// </summary>
        public static async Task<bool> CanPurchaseItemAsync(int steamItemDefId)
        {
            if (!isItemDataLoaded || !inAppItemDataCache.ContainsKey(steamItemDefId))
            {
                return false;
            }

            if (processingItemIds.Contains(steamItemDefId))
            {
                return false;
            }

            var item = inAppItemDataCache[steamItemDefId];
            
            // 중복 구매 방지 확인
            if (item.preventDuplicatePurchase && dataProvider != null)
            {
                bool hasHistory = await dataProvider.HasPurchaseHistoryAsync(steamItemDefId);
                if (hasHistory)
                {
                    return false;
                }
            }

            return true;
        }        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        public static void PrintDebugInfo()
        {
            Debug.Log("=== Steam Payment Manager Debug Info ===");
            Debug.Log($"초기화 상태: {instance != null}");
            Debug.Log($"Steam 연결 상태: {SteamManager.Initialized}");
            Debug.Log($"현재 결제 상태: {CurrentState}");
            Debug.Log($"데이터 로드 상태: {isItemDataLoaded}");
            Debug.Log($"캐시된 아이템 수: {inAppItemDataCache.Count}");
            Debug.Log($"처리 중인 아이템 수: {processingItemIds.Count}");
            Debug.Log($"데이터 제공자: {dataProvider?.GetType().Name ?? "null"}");
        }

        /// <summary>
        /// Steam 스토어 페이지 열기 (외부 브라우저)
        /// </summary>
        public static void OpenSteamStorePage(int steamItemDefId)
        {
            if (SteamManager.Initialized)
            {
                var url = $"https://store.steampowered.com/itemstore/{SteamUtils.GetAppID()}/detail/{steamItemDefId}";
                Application.OpenURL(url);
                
                if (settings?.enableDebugLogs == true)
                {
                    Debug.Log($"Steam 스토어 페이지 열기: {url}");
                }
            }
        }

        /// <summary>
        /// Steam 스토어 오버레이 열기 (권장)
        /// </summary>
        public static void OpenSteamStoreOverlay(int steamItemDefId)
        {
            if (SteamManager.Initialized)
            {
                var url = $"https://store.steampowered.com/itemstore/{SteamUtils.GetAppID()}/detail/{steamItemDefId}";
                SteamFriends.ActivateGameOverlayToWebPage(url);
                
                if (settings?.enableDebugLogs == true)
                {
                    Debug.Log($"Steam 스토어 오버레이 열기: {url}");
                }
            }
        }        /// <summary>
        /// 구매 이력 삭제 (테스트용)
        /// </summary>
        public static async Task<bool> RemovePurchaseHistoryAsync(int steamItemDefId)
        {
            if (dataProvider != null)
            {
                return await dataProvider.RemovePurchaseHistoryAsync(steamItemDefId);
            }
            return false;
        }

        /// <summary>
        /// 모든 구매 이력 삭제 (테스트용)
        /// </summary>
        public static async Task<bool> ClearAllPurchaseHistoryAsync()
        {
            if (dataProvider != null)
            {
                return await dataProvider.ClearAllPurchaseHistoryAsync();
            }
            return false;
        }

        /// <summary>
        /// 데이터 제공자 설정
        /// </summary>
        public static void SetDataProvider(ISteamPaymentDataProvider provider)
        {
            dataProvider = provider;
        }

        /// <summary>
        /// 설정 정보 반환
        /// </summary>
        public static SteamPaymentSettings GetSettings()
        {
            return settings;
        }
    }#else

    /// <summary>
    /// Steam 결제 시스템 메인 매니저 (더미 클래스)
    /// </summary>
    public class SteamPaymentManager : MonoBehaviour
    {
        public enum PaymentState { Idle, LoadingItemData, ProcessingPayment, ProcessingReward, Completed, Failed }
        
        public static event Action<int, bool, string> OnPaymentCompleted;
        public static event Action<PaymentState> OnPaymentStateChanged;
        public static PaymentState CurrentState { get; private set; } = PaymentState.Idle;

        public static void Initialize(SteamPaymentSettings settings, ISteamPaymentDataProvider provider = null)
        {
            Debug.LogWarning("Steamworks.NET이 설치되지 않았습니다. Steam 결제 시스템을 사용할 수 없습니다.");
        }

        public static Task<bool> LoadInAppItemDataAsync()
        {
            return Task.FromResult(false);
        }

        public static Task<bool> PurchaseItemAsync(int steamItemDefId)
        {
            return Task.FromResult(false);
        }

        public static Dictionary<int, SteamInAppItem> GetAvailableItems()
        {
            return new Dictionary<int, SteamInAppItem>();
        }

        public static Task<bool> CanPurchaseItemAsync(int steamItemDefId) { return Task.FromResult(false); }

        public static void PrintDebugInfo()
        {
            Debug.Log("=== Steam Payment Manager Debug Info (더미) ===");
            Debug.Log("Steamworks.NET이 설치되지 않았습니다.");
        }

        public static void OpenSteamStorePage(int steamItemDefId) { }
        public static void OpenSteamStoreOverlay(int steamItemDefId) { }
        public static Task<bool> RemovePurchaseHistoryAsync(int steamItemDefId) { return Task.FromResult(false); }
        public static Task<bool> ClearAllPurchaseHistoryAsync() { return Task.FromResult(false); }
        public static void SetDataProvider(ISteamPaymentDataProvider provider) { }
        public static SteamPaymentSettings GetSettings() { return null; }
    }

#endif
}
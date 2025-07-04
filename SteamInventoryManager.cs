using System;
using System.Collections.Generic;
using UnityEngine;

namespace SteamPaymentSystem
{
#if STEAMWORKS_NET
    using Steamworks;
    using System.Threading.Tasks;

    /// <summary>
    /// Steam Inventory 관리 클래스 (독립 버전)
    /// Steam의 ISteamInventory API를 사용하여 인벤토리 관리
    /// </summary>
    public class SteamInventoryManager : MonoBehaviour
    {
        // Steam Inventory 콜백들
        private static Callback<SteamInventoryResultReady_t> m_SteamInventoryResultReady;
        private static Callback<SteamInventoryFullUpdate_t> m_SteamInventoryFullUpdate;
        private static Callback<SteamInventoryDefinitionUpdate_t> m_SteamInventoryDefinitionUpdate;

        // 현재 인벤토리 상태
        private static SteamInventoryResult_t currentResult = SteamInventoryResult_t.Invalid;
        private static Dictionary<SteamItemDef_t, uint> inventoryItems = new Dictionary<SteamItemDef_t, uint>();

        // 이벤트
        public static event Action<bool> OnInventoryLoaded;
        public static event Action<SteamItemDef_t[], uint[]> OnInventoryUpdated;

        // 설정
        private static SteamPaymentSettings settings;
        private static bool isInitialized = false;

        /// <summary>
        /// 설정 초기화
        /// </summary>
        public static void Initialize(SteamPaymentSettings paymentSettings)
        {
            settings = paymentSettings;
            InitializeSteamInventory();
        }

        void Awake()
        {
            if (!isInitialized)
            {
                InitializeSteamInventory();
            }
        }

        void OnDestroy()
        {
            CleanupSteamInventory();
        }

        /// <summary>
        /// Steam Inventory 초기화
        /// </summary>
        public static void InitializeSteamInventory()
        {
            if (isInitialized || !SteamManager.Initialized)
                return;

            try
            {
                // Steam Inventory 콜백 등록
                m_SteamInventoryResultReady = Callback<SteamInventoryResultReady_t>.Create(OnSteamInventoryResultReady);
                m_SteamInventoryFullUpdate = Callback<SteamInventoryFullUpdate_t>.Create(OnSteamInventoryFullUpdate);
                m_SteamInventoryDefinitionUpdate = Callback<SteamInventoryDefinitionUpdate_t>.Create(OnSteamInventoryDefinitionUpdate);

                // 인벤토리 로드
                LoadInventory();

                isInitialized = true;
                
                if (settings?.enableDebugLogs == true)
                {
                    Debug.Log("Steam Inventory 초기화 완료");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Steam Inventory 초기화 실패: {e.Message}");
            }
        }

        /// <summary>
        /// Steam Inventory 정리
        /// </summary>
        public static void CleanupSteamInventory()
        {
            if (currentResult != SteamInventoryResult_t.Invalid)
            {
                SteamInventory.DestroyResult(currentResult);
                currentResult = SteamInventoryResult_t.Invalid;
            }

            m_SteamInventoryResultReady?.Dispose();
            m_SteamInventoryFullUpdate?.Dispose();
            m_SteamInventoryDefinitionUpdate?.Dispose();

            inventoryItems.Clear();
            isInitialized = false;
            
            if (settings?.enableDebugLogs == true)
            {
                Debug.Log("Steam Inventory 정리 완료");
            }
        }

        /// <summary>
        /// 인벤토리 로드
        /// </summary>
        public static void LoadInventory()
        {
            if (!SteamManager.Initialized)
                return;

            SteamInventoryResult_t result;
            if (SteamInventory.GetAllItems(out result))
            {
                currentResult = result;
                
                if (settings?.enableDebugLogs == true)
                {
                    Debug.Log("Steam 인벤토리 로드 요청 완료");
                }
            }
            else
            {
                Debug.LogError("Steam 인벤토리 로드 요청 실패");
            }
        }

        /// <summary>
        /// 특정 아이템의 수량 확인
        /// </summary>
        public static uint GetItemQuantity(SteamItemDef_t itemDef)
        {
            return inventoryItems.ContainsKey(itemDef) ? inventoryItems[itemDef] : 0;
        }

        /// <summary>
        /// 아이템 교환 (결제 처리)
        /// </summary>
        public static bool ExchangeItems(SteamItemDef_t[] consumeItems, uint[] consumeQuantity,
                                       SteamItemDef_t[] generateItems, uint[] generateQuantity)
        {
            if (!SteamManager.Initialized)
                return false;

            SteamInventoryResult_t result;
            bool success = SteamInventory.ExchangeItems(
                out result, 
                consumeItems, consumeQuantity, (uint)consumeItems.Length, 
                generateItems, generateQuantity, (uint)generateItems.Length
            );

            if (settings?.enableDebugLogs == true)
            {
                Debug.Log($"Steam 아이템 교환 요청: {success}");
            }

            return success;
        }

        /// <summary>
        /// 아이템 생성 (테스트용)
        /// </summary>
        public static bool GenerateItems(SteamItemDef_t[] itemDefs, uint[] quantities)
        {
            if (!SteamManager.Initialized)
                return false;

            SteamInventoryResult_t result;
            bool success = SteamInventory.GenerateItems(out result, itemDefs, quantities, (uint)itemDefs.Length);

            if (settings?.enableDebugLogs == true)
            {
                Debug.Log($"Steam 아이템 생성 요청: {success}");
            }

            return success;
        }

        /// <summary>
        /// Steam Inventory 결과 준비 콜백
        /// </summary>
        private static void OnSteamInventoryResultReady(SteamInventoryResultReady_t pCallback)
        {
            if (settings?.enableDebugLogs == true)
            {
                Debug.Log($"Steam Inventory Result Ready: {pCallback.m_result}, Handle: {pCallback.m_handle}");
            }

            if (pCallback.m_result == EResult.k_EResultOK)
            {
                ProcessInventoryResult(pCallback.m_handle);
            }
            else
            {
                Debug.LogError($"Steam Inventory 처리 실패: {pCallback.m_result}");
                OnInventoryLoaded?.Invoke(false);
            }
        }

        /// <summary>
        /// Steam Inventory 전체 업데이트 콜백
        /// </summary>
        private static void OnSteamInventoryFullUpdate(SteamInventoryFullUpdate_t pCallback)
        {
            if (settings?.enableDebugLogs == true)
            {
                Debug.Log($"Steam Inventory Full Update: Handle: {pCallback.m_handle}");
            }
            ProcessInventoryResult(pCallback.m_handle);
        }

        /// <summary>
        /// Steam Inventory 정의 업데이트 콜백
        /// </summary>
        private static void OnSteamInventoryDefinitionUpdate(SteamInventoryDefinitionUpdate_t pCallback)
        {
            if (settings?.enableDebugLogs == true)
            {
                Debug.Log("Steam Inventory Definition Updated");
            }
        }

        /// <summary>
        /// 인벤토리 결과 처리
        /// </summary>
        private static void ProcessInventoryResult(SteamInventoryResult_t handle)
        {
            uint itemCount = 0;
            if (!SteamInventory.GetResultItems(handle, null, ref itemCount))
            {
                Debug.LogError("Steam Inventory 아이템 개수 가져오기 실패");
                return;
            }

            if (itemCount == 0)
            {
                if (settings?.enableDebugLogs == true)
                {
                    Debug.Log("Steam Inventory가 비어있습니다.");
                }
                OnInventoryLoaded?.Invoke(true);
                return;
            }

            SteamItemDetails_t[] items = new SteamItemDetails_t[itemCount];
            if (SteamInventory.GetResultItems(handle, items, ref itemCount))
            {
                inventoryItems.Clear();
                List<SteamItemDef_t> itemDefs = new List<SteamItemDef_t>();
                List<uint> quantities = new List<uint>();

                foreach (var item in items)
                {
                    inventoryItems[item.m_iDefinition] = item.m_unQuantity;
                    itemDefs.Add(item.m_iDefinition);
                    quantities.Add(item.m_unQuantity);

                    if (settings?.enableDebugLogs == true)
                    {
                        Debug.Log($"Steam 아이템: DefID={item.m_iDefinition}, Quantity={item.m_unQuantity}");
                    }
                }

                OnInventoryLoaded?.Invoke(true);
                OnInventoryUpdated?.Invoke(itemDefs.ToArray(), quantities.ToArray());
            }
            else
            {
                Debug.LogError("Steam Inventory 아이템 상세 정보 가져오기 실패");
                OnInventoryLoaded?.Invoke(false);
            }
        }

        /// <summary>
        /// 인벤토리 새로고침
        /// </summary>
        public static void RefreshInventory()
        {
            if (SteamManager.Initialized)
            {
                LoadInventory();
            }
        }

        /// <summary>
        /// 인벤토리 디버그 정보 출력
        /// </summary>
        public static void PrintInventoryDebug()
        {
            Debug.Log("=== Steam Inventory Debug Info ===");
            Debug.Log($"초기화 상태: {isInitialized}");
            Debug.Log($"Steam 연결 상태: {SteamManager.Initialized}");
            Debug.Log($"인벤토리 아이템 수: {inventoryItems.Count}");

            foreach (var kvp in inventoryItems)
            {
                Debug.Log($"  아이템 DefID: {kvp.Key}, 수량: {kvp.Value}");
            }
        }

        /// <summary>
        /// 초기화 상태 확인
        /// </summary>
        public static bool IsInitialized => isInitialized;
    }

#else

    /// <summary>
    /// Steam Inventory 관리 클래스 (더미 클래스)
    /// </summary>
    public class SteamInventoryManager : MonoBehaviour
    {
        public static event Action<bool> OnInventoryLoaded;
        public static event Action<int[], uint[]> OnInventoryUpdated;

        public static void Initialize(SteamPaymentSettings settings)
        {
            Debug.LogWarning("Steamworks.NET이 설치되지 않았습니다. Steam Inventory 기능을 사용할 수 없습니다.");
        }

        public static void InitializeSteamInventory() { }
        public static void CleanupSteamInventory() { }
        public static void LoadInventory() { }
        public static uint GetItemQuantity(int itemDef) { return 0; }
        public static bool ExchangeItems(int[] consumeItems, uint[] consumeQuantity, int[] generateItems, uint[] generateQuantity) { return false; }
        public static bool GenerateItems(int[] itemDefs, uint[] quantities) { return false; }
        public static void RefreshInventory() { }
        public static void PrintInventoryDebug()
        {
            Debug.Log("=== Steam Inventory Debug Info (더미) ===");
            Debug.Log("Steamworks.NET이 설치되지 않았습니다.");
        }
        public static bool IsInitialized => false;
    }

#endif
}
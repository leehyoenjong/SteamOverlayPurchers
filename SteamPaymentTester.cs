using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

namespace SteamPaymentSystem
{
#if STEAMWORKS_NET

    /// <summary>
    /// Steam 결제 시스템 테스터 (독립 버전)
    /// Steam 파트너 계정을 통한 결제 테스트 기능 제공
    /// </summary>
    public class SteamPaymentTester : MonoBehaviour
    {
        [Header("설정")]
        [Tooltip("Steam 결제 시스템 설정 파일")]
        public SteamPaymentSettings paymentSettings;

        [Header("테스트 설정")]
        public bool enableTestMode = true;
        public List<int> testItemDefIds = new List<int> { 10000, 10001, 10002 };

        [Header("UI 테스트 단축키")]
        public KeyCode loadDataKey = KeyCode.F1;
        public KeyCode purchaseTestKey = KeyCode.F2;
        public KeyCode inventoryDebugKey = KeyCode.F3;
        public KeyCode paymentDebugKey = KeyCode.F4;
        public KeyCode clearPurchaseKey = KeyCode.F5;
        public KeyCode webStoreTestKey = KeyCode.F6;

        [Header("테스트 결과")]
        public bool isDataLoaded = false;
        public SteamPaymentManager.PaymentState currentPaymentState;
        public string lastPurchaseResult = "";

        void Start()
        {
            if (enableTestMode)
            {
                InitializeTester();
            }
        }        void Update()
        {
            if (!enableTestMode) return;

            HandleKeyboardInput();
            UpdateTestStatus();
        }

        /// <summary>
        /// 테스터 초기화
        /// </summary>
        private void InitializeTester()
        {
            Debug.Log("=== Steam Payment Tester 초기화 (독립 버전) ===");
            Debug.Log("🎮 테스트 모드: Steam 파트너 계정 무료 테스트 지원");
            Debug.Log("💡 키보드 단축키:");
            Debug.Log("  F1: 인앱 아이템 데이터 로드 (설정 파일 기반)");
            Debug.Log("  F2: 테스트 아이템 구매 (Steam 파트너 계정 무료)");
            Debug.Log("  F3: Steam Inventory 디버그 정보");
            Debug.Log("  F4: Payment Manager 상태 정보");
            Debug.Log("  F5: 구매 기록 초기화 (테스트용)");
            Debug.Log("  F6: Steam 웹 스토어 테스트");
            Debug.Log("");
            Debug.Log("📋 테스트 시나리오:");
            Debug.Log("  1. F1으로 설정 파일 데이터 로드");
            Debug.Log("  2. F2로 테스트 구매 (Steam 파트너 계정은 무료)");
            Debug.Log("  3. 자동으로 보상 지급 및 기록 저장");
            Debug.Log("  4. F5로 기록 초기화 후 재테스트 가능");

            // 이벤트 구독
            SteamPaymentManager.OnPaymentCompleted += OnPaymentCompleted;
            SteamPaymentManager.OnPaymentStateChanged += OnPaymentStateChanged;

            // 설정 파일이 있으면 자동 초기화
            if (paymentSettings != null)
            {
                SteamPaymentManager.Initialize(paymentSettings);
                Debug.Log("설정 파일을 통해 Payment Manager 초기화 완료");
            }
        }

        void OnDestroy()
        {
            // 이벤트 구독 해제
            SteamPaymentManager.OnPaymentCompleted -= OnPaymentCompleted;
            SteamPaymentManager.OnPaymentStateChanged -= OnPaymentStateChanged;
        }        /// <summary>
        /// 키보드 입력 처리
        /// </summary>
        private void HandleKeyboardInput()
        {
            if (Input.GetKeyDown(loadDataKey))
            {
                TestLoadInAppDataAsync();
            }

            if (Input.GetKeyDown(purchaseTestKey))
            {
                TestPurchaseItemAsync();
            }

            if (Input.GetKeyDown(inventoryDebugKey))
            {
                SteamInventoryManager.PrintInventoryDebug();
            }

            if (Input.GetKeyDown(paymentDebugKey))
            {
                SteamPaymentManager.PrintDebugInfo();
            }

            if (Input.GetKeyDown(clearPurchaseKey))
            {
                TestClearPurchaseRecordsAsync();
            }

            if (Input.GetKeyDown(webStoreTestKey))
            {
                TestSteamWebStore();
            }
        }

        /// <summary>
        /// 테스트 상태 업데이트
        /// </summary>
        private void UpdateTestStatus()
        {
            currentPaymentState = SteamPaymentManager.CurrentState;
        }

        /// <summary>
        /// 인앱 아이템 데이터 로드 테스트
        /// </summary>
        public async void TestLoadInAppDataAsync()
        {
            Debug.Log("=== 인앱 아이템 데이터 로드 테스트 시작 ===");

            bool success = await SteamPaymentManager.LoadInAppItemDataAsync();
            isDataLoaded = success;

            if (success)
            {
                Debug.Log("인앱 아이템 데이터 로드 성공!");
                var availableItems = SteamPaymentManager.GetAvailableItems();

                Debug.Log($"사용 가능한 아이템 수: {availableItems.Count}");
                foreach (var kvp in availableItems)
                {
                    var item = kvp.Value;
                    Debug.Log($"  ItemDefId: {kvp.Key}, 이름: {item.itemName}, 보상: {item.rewards.Count}개");

                    foreach (var reward in item.rewards)
                    {
                        Debug.Log($"    - Kind: {reward.kind}, ID: {reward.id}, Value: {reward.value}");
                    }
                }
            }
            else
            {
                Debug.LogError("인앱 아이템 데이터 로드 실패!");
            }
        }        /// <summary>
        /// 아이템 구매 테스트
        /// </summary>
        public async void TestPurchaseItemAsync()
        {
            if (paymentSettings == null)
            {
                Debug.LogError("PaymentSettings가 설정되지 않았습니다!");
                return;
            }

            var testIds = paymentSettings.testItemDefIds.Count > 0 ? paymentSettings.testItemDefIds : testItemDefIds;
            
            if (testIds.Count == 0)
            {
                Debug.LogWarning("테스트할 아이템 ID가 설정되지 않았습니다.");
                return;
            }

            int testItemId = testIds[0]; // 첫 번째 테스트 아이템 사용

            Debug.Log($"=== Steam 아이템 구매 테스트 시작: ItemDefId={testItemId} ===");

            // 현재 캐시된 아이템 목록 확인
            var availableItems = SteamPaymentManager.GetAvailableItems();
            Debug.Log($"현재 캐시된 아이템 목록: {string.Join(", ", availableItems.Keys)}");

            if (!availableItems.ContainsKey(testItemId))
            {
                Debug.LogError($"❌ 테스트 아이템 {testItemId}이 캐시에 없습니다!");
                Debug.LogError("💡 해결 방법:");
                Debug.LogError("  1. PaymentSettings에 해당 아이템이 설정되어 있는지 확인");
                Debug.LogError("  2. F1키로 데이터를 다시 로드해보세요");
                return;
            }

            // 구매 가능 여부 확인
            bool canPurchase = await SteamPaymentManager.CanPurchaseItemAsync(testItemId);
            Debug.Log($"구매 가능 여부: {canPurchase}");

            if (!canPurchase)
            {
                Debug.LogWarning("구매할 수 없는 아이템입니다. (이미 구매했거나 제한이 있습니다)");
                return;
            }

            // 구매 시도
            bool purchaseSuccess = await SteamPaymentManager.PurchaseItemAsync(testItemId);
            lastPurchaseResult = purchaseSuccess ? "성공" : "실패";

            if (purchaseSuccess)
            {
                Debug.Log($"✅ 구매 테스트 성공: {testItemId}");
            }
            else
            {
                Debug.LogError($"❌ 구매 테스트 실패: {testItemId}");
            }
        }        /// <summary>
        /// 구매 기록 초기화 테스트
        /// </summary>
        public async void TestClearPurchaseRecordsAsync()
        {
            Debug.Log("=== 구매 기록 초기화 테스트 ===");

            bool success = await SteamPaymentManager.ClearAllPurchaseHistoryAsync();
            
            if (success)
            {
                Debug.Log("✅ 모든 구매 기록이 초기화되었습니다.");
            }
            else
            {
                Debug.LogError("❌ 구매 기록 초기화에 실패했습니다.");
            }
        }

        /// <summary>
        /// Steam 웹 스토어 테스트
        /// </summary>
        public void TestSteamWebStore()
        {
            if (paymentSettings == null)
            {
                Debug.LogError("PaymentSettings가 설정되지 않았습니다!");
                return;
            }

            var testIds = paymentSettings.testItemDefIds.Count > 0 ? paymentSettings.testItemDefIds : testItemDefIds;
            
            if (testIds.Count == 0)
            {
                Debug.LogWarning("테스트할 아이템 ID가 설정되지 않았습니다.");
                return;
            }

            int testItemId = testIds[0];
            Debug.Log($"=== Steam 웹 스토어 테스트: ItemDefId={testItemId} ===");

            // Steam 오버레이로 스토어 페이지 열기 (권장)
            SteamPaymentManager.OpenSteamStoreOverlay(testItemId);
            Debug.Log("Steam 오버레이로 스토어 페이지를 열었습니다.");
        }

        /// <summary>
        /// 결제 완료 이벤트 핸들러
        /// </summary>
        private void OnPaymentCompleted(int steamItemDefId, bool success, string message)
        {
            lastPurchaseResult = success ? "성공" : "실패";
            
            if (success)
            {
                Debug.Log($"🎉 결제 완료 이벤트: ItemDefId={steamItemDefId}, 메시지={message}");
            }
            else
            {
                Debug.LogError($"💥 결제 실패 이벤트: ItemDefId={steamItemDefId}, 메시지={message}");
            }
        }

        /// <summary>
        /// 결제 상태 변경 이벤트 핸들러
        /// </summary>
        private void OnPaymentStateChanged(SteamPaymentManager.PaymentState newState)
        {
            currentPaymentState = newState;
            
            if (paymentSettings?.enableDebugLogs == true)
            {
                Debug.Log($"🔄 결제 상태 변경: {newState}");
            }
        }        /// <summary>
        /// 시스템 상태 리포트 생성
        /// </summary>
        [ContextMenu("Generate System Status Report")]
        public void GenerateSystemStatusReport()
        {
            Debug.Log("=== Steam Payment System Status Report ===");
            Debug.Log($"테스트 모드: {enableTestMode}");
            Debug.Log($"설정 파일: {(paymentSettings != null ? "설정됨" : "미설정")}");
            Debug.Log($"Steam 연결: {SteamManager.Initialized}");
            Debug.Log($"데이터 로드 상태: {isDataLoaded}");
            Debug.Log($"현재 결제 상태: {currentPaymentState}");
            Debug.Log($"마지막 구매 결과: {lastPurchaseResult}");
            
            if (paymentSettings != null)
            {
                Debug.Log($"설정된 아이템 수: {paymentSettings.inAppItems.Count}");
                Debug.Log($"테스트 아이템 수: {paymentSettings.testItemDefIds.Count}");
            }
        }

        /// <summary>
        /// OnGUI를 통한 테스트 UI
        /// </summary>
        void OnGUI()
        {
            if (!enableTestMode) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 400));
            GUILayout.Label("Steam Payment Tester (독립 버전)", EditorGUIUtility.isProSkin ? GUI.skin.box.normal.textColor = Color.white : Color.black);
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("F1: 데이터 로드"))
            {
                TestLoadInAppDataAsync();
            }
            
            if (GUILayout.Button("F2: 구매 테스트"))
            {
                TestPurchaseItemAsync();
            }
            
            if (GUILayout.Button("F3: 인벤토리 디버그"))
            {
                SteamInventoryManager.PrintInventoryDebug();
            }
            
            if (GUILayout.Button("F4: 결제 시스템 디버그"))
            {
                SteamPaymentManager.PrintDebugInfo();
            }
            
            if (GUILayout.Button("F5: 구매 기록 초기화"))
            {
                TestClearPurchaseRecordsAsync();
            }
            
            if (GUILayout.Button("F6: 웹 스토어 테스트"))
            {
                TestSteamWebStore();
            }
            
            GUILayout.Space(10);
            GUILayout.Label($"Steam 상태: {(SteamManager.Initialized ? "연결됨" : "연결 안됨")}");
            GUILayout.Label($"데이터 로드: {(isDataLoaded ? "완료" : "미완료")}");
            GUILayout.Label($"결제 상태: {currentPaymentState}");
            GUILayout.Label($"마지막 결과: {lastPurchaseResult}");
            
            GUILayout.EndArea();
        }
    }#else

    /// <summary>
    /// Steam 결제 시스템 테스터 (더미 클래스)
    /// </summary>
    public class SteamPaymentTester : MonoBehaviour
    {
        [Header("설정")]
        public SteamPaymentSettings paymentSettings;
        
        [Header("테스트 설정")]
        public bool enableTestMode = true;
        public List<int> testItemDefIds = new List<int> { 10000, 10001, 10002 };

        void Start()
        {
            if (enableTestMode)
            {
                Debug.LogWarning("Steamworks.NET이 설치되지 않았습니다. Steam 결제 시스템을 사용할 수 없습니다.");
            }
        }

        void OnGUI()
        {
            if (!enableTestMode) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("Steam Payment Tester (더미)");
            GUILayout.Space(10);
            GUILayout.Label("Steamworks.NET이 설치되지 않았습니다.");
            GUILayout.Label("Steam 결제 시스템을 사용할 수 없습니다.");
            GUILayout.EndArea();
        }

        public void TestLoadInAppDataAsync() { }
        public void TestPurchaseItemAsync() { }
        public void TestClearPurchaseRecordsAsync() { }
        public void TestSteamWebStore() { }
        public void GenerateSystemStatusReport() { }
    }

#endif
}
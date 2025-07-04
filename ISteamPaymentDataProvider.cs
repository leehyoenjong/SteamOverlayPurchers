using System.Collections.Generic;
using System.Threading.Tasks;

namespace SteamPaymentSystem
{
    /// <summary>
    /// Steam 결제 시스템 데이터 제공자 인터페이스
    /// 서버 연동을 추상화하여 다양한 백엔드 시스템과 호환 가능
    /// </summary>
    public interface ISteamPaymentDataProvider
    {
        /// <summary>
        /// 인앱 아이템 데이터를 로드합니다
        /// </summary>
        /// <returns>성공 여부와 아이템 데이터 딕셔너리</returns>
        Task<(bool success, Dictionary<int, SteamInAppItem> items)> LoadInAppItemDataAsync();

        /// <summary>
        /// 구매 이력이 있는지 확인합니다
        /// </summary>
        /// <param name="steamItemDefId">Steam Item Definition ID</param>
        /// <returns>구매 이력 존재 여부</returns>
        Task<bool> HasPurchaseHistoryAsync(int steamItemDefId);

        /// <summary>
        /// 구매 이력을 저장합니다
        /// </summary>
        /// <param name="steamItemDefId">Steam Item Definition ID</param>
        /// <param name="purchaseData">구매 데이터</param>
        /// <returns>저장 성공 여부</returns>
        Task<bool> SavePurchaseHistoryAsync(int steamItemDefId, PurchaseData purchaseData);

        /// <summary>
        /// 보상을 지급합니다
        /// </summary>
        /// <param name="rewards">지급할 보상 목록</param>
        /// <returns>지급 성공 여부</returns>
        Task<bool> GrantRewardsAsync(List<ItemReward> rewards);

        /// <summary>
        /// 구매 이력을 삭제합니다 (테스트용)
        /// </summary>
        /// <param name="steamItemDefId">Steam Item Definition ID</param>
        /// <returns>삭제 성공 여부</returns>
        Task<bool> RemovePurchaseHistoryAsync(int steamItemDefId);

        /// <summary>
        /// 모든 구매 이력을 삭제합니다 (테스트용)
        /// </summary>
        /// <returns>삭제 성공 여부</returns>
        Task<bool> ClearAllPurchaseHistoryAsync();
    }

    /// <summary>
    /// 구매 데이터 구조체
    /// </summary>
    [System.Serializable]
    public struct PurchaseData
    {
        public int steamItemDefId;
        public System.DateTime purchaseTime;
        public string transactionId;
        public List<ItemReward> rewards;

        public PurchaseData(int steamItemDefId, System.DateTime purchaseTime, string transactionId, List<ItemReward> rewards)
        {
            this.steamItemDefId = steamItemDefId;
            this.purchaseTime = purchaseTime;
            this.transactionId = transactionId;
            this.rewards = rewards;
        }
    }

    /// <summary>
    /// 로컬 테스트용 데이터 제공자
    /// 실제 서버 연동 없이 테스트 가능
    /// </summary>
    public class LocalTestDataProvider : ISteamPaymentDataProvider
    {
        private Dictionary<int, SteamInAppItem> cachedItems = new Dictionary<int, SteamInAppItem>();
        private HashSet<int> purchaseHistory = new HashSet<int>();
        private SteamPaymentSettings settings;

        public LocalTestDataProvider(SteamPaymentSettings settings)
        {
            this.settings = settings;
        }

        public async Task<(bool success, Dictionary<int, SteamInAppItem> items)> LoadInAppItemDataAsync()
        {
            await Task.Delay(100); // 비동기 시뮬레이션

            if (settings == null)
            {
                UnityEngine.Debug.LogError("SteamPaymentSettings가 설정되지 않았습니다.");
                return (false, new Dictionary<int, SteamInAppItem>());
            }

            cachedItems = settings.GetItemDictionary();

            if (settings.enableDebugLogs)
            {
                UnityEngine.Debug.Log($"로컬 테스트 데이터 로드 완료: {cachedItems.Count}개 아이템");
            }

            return (true, cachedItems);
        }

        public async Task<bool> HasPurchaseHistoryAsync(int steamItemDefId)
        {
            await Task.Delay(50); // 비동기 시뮬레이션
            return purchaseHistory.Contains(steamItemDefId);
        }

        public async Task<bool> SavePurchaseHistoryAsync(int steamItemDefId, PurchaseData purchaseData)
        {
            await Task.Delay(50); // 비동기 시뮬레이션
            purchaseHistory.Add(steamItemDefId);

            if (settings != null && settings.enableDebugLogs)
            {
                UnityEngine.Debug.Log($"구매 이력 저장 완료: {steamItemDefId}");
            }

            return true;
        }

        public async Task<bool> GrantRewardsAsync(List<ItemReward> rewards)
        {
            await Task.Delay(100); // 비동기 시뮬레이션

            if (settings != null && settings.enableDebugLogs)
            {
                UnityEngine.Debug.Log($"보상 지급 완료: {rewards.Count}개 보상");
                foreach (var reward in rewards)
                {
                    UnityEngine.Debug.Log($"  - Kind: {reward.kind}, ID: {reward.id}, Value: {reward.value}");
                }
            }

            return true;
        }

        public async Task<bool> RemovePurchaseHistoryAsync(int steamItemDefId)
        {
            await Task.Delay(50); // 비동기 시뮬레이션
            bool removed = purchaseHistory.Remove(steamItemDefId);

            if (settings != null && settings.enableDebugLogs)
            {
                UnityEngine.Debug.Log($"구매 이력 삭제: {steamItemDefId} (성공: {removed})");
            }

            return removed;
        }

        public async Task<bool> ClearAllPurchaseHistoryAsync()
        {
            await Task.Delay(50); // 비동기 시뮬레이션
            int count = purchaseHistory.Count;
            purchaseHistory.Clear();

            if (settings != null && settings.enableDebugLogs)
            {
                UnityEngine.Debug.Log($"모든 구매 이력 삭제 완료: {count}개 삭제");
            }

            return true;
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

namespace SteamPaymentSystem
{
    /// <summary>
    /// 스팀 결제 시스템 설정 데이터
    /// </summary>
    [CreateAssetMenu(fileName = "SteamPaymentSettings", menuName = "Steam Payment System/Settings")]
    public class SteamPaymentSettings : ScriptableObject
    {
        [Header("기본 설정")]
        [Tooltip("디버그 로그 활성화")]
        public bool enableDebugLogs = true;

        [Tooltip("자동 초기화 활성화")]
        public bool autoInitialize = true;

        [Tooltip("초기화 지연 시간 (초)")]
        public float initializationDelay = 1.0f;

        [Header("아이템 설정")]
        [Tooltip("사용 가능한 인앱 아이템 목록")]
        public List<SteamInAppItem> inAppItems = new List<SteamInAppItem>();

        [Header("테스트 설정")]
        [Tooltip("테스트 모드 활성화")]
        public bool enableTestMode = false;

        [Tooltip("테스트용 아이템 ID 목록")]
        public List<int> testItemDefIds = new List<int>();

        /// <summary>
        /// 특정 Steam Item Def ID로 아이템 찾기
        /// </summary>
        public SteamInAppItem FindItem(int steamItemDefId)
        {
            return inAppItems.Find(item => item.steamItemDefId == steamItemDefId);
        }

        /// <summary>
        /// 모든 아이템을 Dictionary 형태로 반환
        /// </summary>
        public Dictionary<int, SteamInAppItem> GetItemDictionary()
        {
            var dictionary = new Dictionary<int, SteamInAppItem>();
            foreach (var item in inAppItems)
            {
                if (item.steamItemDefId != 0 && !dictionary.ContainsKey(item.steamItemDefId))
                {
                    dictionary[item.steamItemDefId] = item;
                }
            }
            return dictionary;
        }
    }

    /// <summary>
    /// 스팀 인앱 아이템 데이터
    /// </summary>
    [System.Serializable]
    public class SteamInAppItem
    {
        [Header("Steam 설정")]
        [Tooltip("Steam Item Definition ID")]
        public int steamItemDefId;

        [Tooltip("아이템 이름")]
        public string itemName;

        [Tooltip("아이템 설명")]
        public string description;

        [Header("보상 설정")]
        [Tooltip("보상 목록")]
        public List<ItemReward> rewards = new List<ItemReward>();

        [Header("제한 설정")]
        [Tooltip("중복 구매 방지 (false = 여러 번 구매 가능)")]
        public bool preventDuplicatePurchase = true;

        [Tooltip("구매 제한 횟수 (0 = 무제한)")]
        public int purchaseLimit = 0;

        public SteamInAppItem()
        {
            rewards = new List<ItemReward>();
        }
    }

    /// <summary>
    /// 아이템 보상 데이터
    /// </summary>
    [System.Serializable]
    public class ItemReward
    {
        [Tooltip("보상 종류 (0: 아이템, 1: 앨범, 2: 코인 등)")]
        public int kind;

        [Tooltip("보상 ID")]
        public int id;

        [Tooltip("보상 수량")]
        public int value;

        public ItemReward()
        {
        }

        public ItemReward(int kind, int id, int value)
        {
            this.kind = kind;
            this.id = id;
            this.value = value;
        }
    }
}
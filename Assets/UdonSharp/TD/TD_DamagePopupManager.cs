using UdonSharp;
using UnityEngine;

namespace JumpUp.TD
{
    /// <summary>
    /// 伤害飘字管理器：提供对象池来显示数字。
    /// </summary>
    public class TD_DamagePopupManager : UdonSharpBehaviour
    {
        [Tooltip("飘字预制池")]
        public TD_DamagePopup[] popupPool;
        [Tooltip("普通伤害颜色")] public Color normalColor = Color.white;
        [Tooltip("暴击颜色")] public Color critColor = Color.yellow;

        private int cursor = 0;

        public void ShowDamage(Vector3 worldPos, int value, bool isCrit)
        {
            TD_DamagePopup popup = GetNext();
            if (popup == null) return;
            popup.Show(worldPos, value, isCrit ? critColor : normalColor, isCrit);
        }

        private TD_DamagePopup GetNext()
        {
            if (popupPool == null || popupPool.Length == 0) return null;
            for (int i = 0; i < popupPool.Length; i++)
            {
                cursor++;
                if (cursor >= popupPool.Length)
                {
                    cursor = 0;
                }

                TD_DamagePopup popup = popupPool[cursor];
                if (popup != null && !popup.gameObject.activeSelf)
                {
                    return popup;
                }
            }
            return null;
        }
    }
}

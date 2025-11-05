using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JumpUp.TD
{
    /// <summary>
    /// 敌人血量同步组件。负责将受击逻辑统一交给 Owner（默认房主）处理，然后在所有客户端播放回显。
    /// </summary>
    public class TD_EnemyState : UdonSharpBehaviour
    {
        [Tooltip("连接到的游戏管理器，用于通知死亡")] public TD_GameManager gameManager;
        [Tooltip("飘字管理器")] public TD_DamagePopupManager popupManager;
        [Tooltip("所属敌人脚本")]
        public TD_Enemy enemy;

        [Header("基础属性")] [Tooltip("最大血量")] [UdonSynced] public int maxHp = 100;
        [Tooltip("当前血量")] [UdonSynced] public int hp = 100;
        [Tooltip("是否已经死亡")] [UdonSynced] public bool isDead = false;

        private int pendingDamage = 0;
        private bool pendingCrit = false;

        /// <summary>
        /// 在敌人复用时重置状态。
        /// </summary>
        public void ResetState(int newMaxHp)
        {
            maxHp = newMaxHp;
            hp = newMaxHp;
            isDead = false;
            RequestSerialization();
        }

        /// <summary>
        /// 客户端发起的伤害请求，统一发送到 Owner。
        /// </summary>
        public void RequestDamage(int damage, bool isCrit, Vector3 hitPoint)
        {
            if (isDead) return;
            pendingDamage = damage;
            pendingCrit = isCrit;
            cachedHitPoint = hitPoint;
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OwnerApplyPendingDamage));
        }

        private Vector3 cachedHitPoint;

        public void OwnerApplyPendingDamage()
        {
            if (!Networking.IsOwner(gameObject)) return;
            ApplyDamageInternal(pendingDamage, pendingCrit, cachedHitPoint);
            pendingDamage = 0;
            pendingCrit = false;
        }

        /// <summary>
        /// 只有 Owner 才会真正扣血。
        /// </summary>
        public void ApplyDamageInternal(int damage, bool isCrit, Vector3 hitPoint)
        {
            if (isDead) return;
            hp = Mathf.Max(0, hp - damage);
            if (hp == 0)
            {
                isDead = true;
                if (gameManager != null)
                {
                    gameManager.NotifyEnemyDead();
                }
                if (enemy != null)
                {
                    enemy.OnKilled();
                }
            }

            RequestSerialization();
            BroadcastDamageFx(damage, isCrit, hitPoint);
        }

        public override void OnDeserialization()
        {
            if (hp <= 0 && !isDead)
            {
                isDead = true;
            }
        }

        private void BroadcastDamageFx(int damage, bool isCrit, Vector3 position)
        {
            if (popupManager != null)
            {
                popupManager.ShowDamage(position, damage, isCrit);
            }
        }
    }
}

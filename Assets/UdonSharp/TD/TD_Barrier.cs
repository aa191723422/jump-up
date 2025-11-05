using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JumpUp.TD
{
    /// <summary>
    /// 掩体可被敌人攻击，休息阶段自动修复。
    /// </summary>
    public class TD_Barrier : UdonSharpBehaviour
    {
        [Tooltip("关联的游戏控制器")] public TD_GameManager gameManager;
        [Tooltip("最大耐久")] public int maxHp = 200;
        [Tooltip("当前耐久")] [UdonSynced] public int hp = 200;
        [Tooltip("休息阶段恢复比例")] public float repairRatio = 1f;
        [Tooltip("敌人站位点")] public Transform[] attackSlots;

        private bool isBroken = false;
        private readonly int[] slotOwners = new int[16];

        private void Start()
        {
            hp = maxHp;
        }

        public void ReceiveDamage(int damage)
        {
            if (!Networking.IsMaster) return;
            if (isBroken) return;

            hp = Mathf.Max(0, hp - damage);
            if (hp == 0)
            {
                isBroken = true;
                if (gameManager != null)
                {
                    gameManager.NotifyBarrierDestroyed();
                }
            }
            RequestSerialization();
        }

        public void Repair()
        {
            if (!Networking.IsMaster) return;

            hp = Mathf.RoundToInt(maxHp * repairRatio);
            if (hp > 0)
            {
                isBroken = false;
            }
            RequestSerialization();
        }

        public override void OnDeserialization()
        {
            if (hp <= 0)
            {
                isBroken = true;
            }
            else
            {
                isBroken = false;
            }
        }

        /// <summary>
        /// 返回敌人应当前往的攻击位置。
        /// </summary>
        /// <param name="from">敌人当前位置</param>
        /// <returns>目标点</returns>
        public Vector3 GetAttackPoint(Vector3 from)
        {
            if (attackSlots == null || attackSlots.Length == 0)
            {
                return transform.position;
            }

            Transform best = attackSlots[0];
            float bestDist = float.MaxValue;
            for (int i = 0; i < attackSlots.Length; i++)
            {
                Transform slot = attackSlots[i];
                if (slot == null) continue;
                float dist = Vector3.Distance(from, slot.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = slot;
                }
            }
            return best != null ? best.position : transform.position;
        }

        public bool IsBroken => isBroken;
    }
}

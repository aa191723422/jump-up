using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JumpUp.TD
{
    /// <summary>
    /// 玩家武器：本地检测开火输入并从对象池中发射子弹。
    /// </summary>
    public class TD_Gun : UdonSharpBehaviour
    {
        [Tooltip("枪口位置")] public Transform muzzle;
        [Tooltip("子弹对象池")] public TD_Bullet[] bulletPool;
        [Tooltip("开火冷却")] public float fireInterval = 0.18f;
        [Tooltip("基础伤害")] public int bulletDamage = 25;
        [Tooltip("暴击概率 (0-1)")] public float critChance = 0.1f;
        [Tooltip("暴击倍率")] public float critMultiplier = 2f;
        [Tooltip("是否自动开火")] public bool autoFire = true;
        [Tooltip("火力射线长度，仅用于粒子特效")] public float fireRayLength = 50f;

        private float lastFireTime = 0f;
        private int poolCursor = 0;

        private void Update()
        {
            if (!IsLocalPlayer()) return;

            bool wantsFire = Input.GetMouseButton(0);
#if UNITY_EDITOR
            // 编辑器中允许按空格测试
            wantsFire |= Input.GetKey(KeyCode.Space);
#endif
            if (!autoFire)
            {
                wantsFire = Input.GetMouseButtonDown(0);
            }

            if (wantsFire && Time.time - lastFireTime >= fireInterval)
            {
                Fire();
            }
        }

        private void Fire()
        {
            lastFireTime = Time.time;
            TD_Bullet bullet = GetNextBullet();
            if (bullet == null) return;

            Vector3 startPos = muzzle != null ? muzzle.position : transform.position;
            Vector3 forward = muzzle != null ? muzzle.forward : transform.forward;

            bool isCrit = Random.value < critChance;
            int dmg = isCrit ? Mathf.RoundToInt(bulletDamage * critMultiplier) : bulletDamage;

            bullet.Fire(startPos, forward, dmg, isCrit, Networking.LocalPlayer);
        }

        private TD_Bullet GetNextBullet()
        {
            if (bulletPool == null || bulletPool.Length == 0) return null;
            for (int i = 0; i < bulletPool.Length; i++)
            {
                poolCursor++;
                if (poolCursor >= bulletPool.Length)
                {
                    poolCursor = 0;
                }

                TD_Bullet bullet = bulletPool[poolCursor];
                if (bullet != null && !bullet.gameObject.activeSelf)
                {
                    return bullet;
                }
            }
            return null;
        }

        private bool IsLocalPlayer()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return false;

            // 判断当前持枪者是否就是本地玩家
            return local.IsUserInVR() ? Networking.GetOwner(gameObject) == local : true;
        }
    }
}

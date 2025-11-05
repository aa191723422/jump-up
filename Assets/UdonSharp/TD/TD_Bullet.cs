using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JumpUp.TD
{
    /// <summary>
    /// 子弹实体：本地移动并在命中敌人时向敌人 Owner 发送伤害事件。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TD_Bullet : UdonSharpBehaviour
    {
        [Tooltip("飞行速度")]
        public float speed = 30f;
        [Tooltip("飞行最大时间")]
        public float lifeTime = 3f;

        private int damage;
        private bool isCrit;
        private float spawnTime;
        private Vector3 direction;
        private VRCPlayerApi owner;

        private Collider bulletCollider;

        private void Awake()
        {
            bulletCollider = GetComponent<Collider>();
            if (bulletCollider != null)
            {
                bulletCollider.isTrigger = true;
            }
        }

        private void Update()
        {
            if (!gameObject.activeSelf) return;

            transform.position += direction * (speed * Time.deltaTime);
            if (Time.time - spawnTime >= lifeTime)
            {
                Recycle();
            }
        }

        public void Fire(Vector3 position, Vector3 forward, int dmg, bool crit, VRCPlayerApi shooter)
        {
            transform.position = position;
            transform.forward = forward;
            direction = forward.normalized;
            damage = dmg;
            isCrit = crit;
            owner = shooter;
            spawnTime = Time.time;

            gameObject.SetActive(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            TD_EnemyState state = other.GetComponent<TD_EnemyState>();
            if (state != null)
            {
                state.RequestDamage(damage, isCrit, transform.position);
                Recycle();
                return;
            }

            TD_Barrier barrier = other.GetComponent<TD_Barrier>();
            if (barrier != null)
            {
                // 允许玩家修补掩体：这里直接略过
                Recycle();
            }
        }

        private void Recycle()
        {
            gameObject.SetActive(false);
        }
    }
}

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JumpUp.TD
{
    /// <summary>
    /// 敌人行为：移动、攻击掩体或玩家。所有行为由房主计算，确保同步一致。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class TD_Enemy : UdonSharpBehaviour
    {
        [Tooltip("状态同步组件")] public TD_EnemyState enemyState;
        [Tooltip("基础移动速度")] public float moveSpeed = 2.5f;
        [Tooltip("攻击间隔")] public float attackCooldown = 2f;
        [Tooltip("攻击伤害")] public int attackDamage = 15;
        [Tooltip("攻击范围")] public float attackRange = 1.5f;
        [Tooltip("优先攻击玩家的距离阈值")] public float playerPriorityDistance = 2.5f;
        [Tooltip("死亡后隐藏的延迟")] public float hideDelay = 2f;

        private TD_GameManager gameManager;
        private TD_Spawner spawner;
        private CharacterController controller;
        private Animator animator;

        private float lastAttackTime = 0f;
        private float deathTime = 0f;

        private VRCPlayerApi cachedTargetPlayer;
        private TD_Barrier cachedBarrier;

        private bool isActive = false;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();
            if (enemyState != null)
            {
                enemyState.gameManager = null;
                enemyState.enemy = this;
            }
        }

        private void Update()
        {
            if (!isActive) return;
            if (!Networking.IsMaster) return;

            if (enemyState != null && enemyState.isDead)
            {
                // 等待一段时间后回收到对象池
                if (Time.time >= deathTime)
                {
                    Despawn();
                }
                return;
            }

            SelectTarget();
            MoveAndAttack();
        }

        public void Spawn(TD_GameManager manager, TD_Spawner sourceSpawner, Vector3 position, int wave, int playerCount, int baseHp)
        {
            Networking.SetOwner(Networking.GetOwner(sourceSpawner.gameObject), gameObject);

            gameManager = manager;
            spawner = sourceSpawner;
            transform.position = position;
            cachedTargetPlayer = null;
            cachedBarrier = null;
            lastAttackTime = 0f;
            isActive = true;
            deathTime = 0f;

            if (enemyState != null)
            {
                enemyState.gameManager = manager;
                enemyState.popupManager = manager != null ? manager.popupManager : enemyState.popupManager;
                enemyState.ResetState(baseHp);
            }

            gameObject.SetActive(true);
            if (animator != null)
            {
                animator.SetBool("Dead", false);
            }
        }

        private void SelectTarget()
        {
            cachedTargetPlayer = null;
            if (gameManager == null) return;

            // 尝试寻找最近的参与玩家
            int participants = gameManager.ParticipantCount;
            float bestPlayerDist = float.MaxValue;
            for (int i = 0; i < participants; i++)
            {
                VRCPlayerApi player = gameManager.GetParticipant(i);
                if (!Utilities.IsValid(player)) continue;

                float dist = Vector3.Distance(transform.position, player.GetPosition());
                if (dist < bestPlayerDist)
                {
                    bestPlayerDist = dist;
                    cachedTargetPlayer = player;
                }
            }

            if (cachedTargetPlayer != null && bestPlayerDist <= playerPriorityDistance)
            {
                cachedBarrier = null;
                return;
            }

            cachedBarrier = gameManager.GetNearestBarrier(transform.position);
        }

        private void MoveAndAttack()
        {
            Vector3 targetPos = transform.position;
            bool inRange = false;

            if (cachedTargetPlayer != null)
            {
                targetPos = cachedTargetPlayer.GetPosition();
                float dist = Vector3.Distance(transform.position, targetPos);
                inRange = dist <= attackRange;
            }
            else if (cachedBarrier != null)
            {
                targetPos = cachedBarrier.GetAttackPoint(transform.position);
                float dist = Vector3.Distance(transform.position, targetPos);
                inRange = dist <= attackRange;
            }

            if (!inRange)
            {
                Vector3 dir = (targetPos - transform.position).normalized;
                controller.SimpleMove(dir * moveSpeed);
                if (animator != null)
                {
                    animator.SetFloat("Speed", controller.velocity.magnitude);
                }
            }
            else
            {
                controller.SimpleMove(Vector3.zero);
                TryAttack();
                if (animator != null)
                {
                    animator.SetFloat("Speed", 0f);
                }
            }
        }

        private void TryAttack()
        {
            if (Time.time - lastAttackTime < attackCooldown) return;
            lastAttackTime = Time.time;

            if (cachedTargetPlayer != null)
            {
                // 玩家受击仅作演示，可扩展为扣除护甲等
                // 这里简单调用 VRC 的伤害事件或自定义效果
            }
            else if (cachedBarrier != null)
            {
                cachedBarrier.ReceiveDamage(attackDamage);
            }
        }

        public void OnKilled()
        {
            if (!isActive) return;
            if (animator != null)
            {
                animator.SetBool("Dead", true);
            }
            deathTime = Time.time + hideDelay;
        }

        private void Despawn()
        {
            isActive = false;
            gameObject.SetActive(false);
            cachedBarrier = null;
            cachedTargetPlayer = null;
        }
    }
}

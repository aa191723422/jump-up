using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

namespace JumpUp.TD
{
    /// <summary>
    /// 游戏核心控制器：负责波次推进、阶段切换以及玩家快照。所有权默认由房主掌控。
    /// </summary>
    public class TD_GameManager : UdonSharpBehaviour
    {
        // ====== 同步常量与状态定义 ======
        private const int STATE_LOBBY = 0;      // 大厅 / 等待状态
        private const int STATE_PREPARE = 1;    // 准备倒计时，玩家即将传送
        private const int STATE_WAVE = 2;       // 战斗阶段
        private const int STATE_REST = 3;       // 休息与修复阶段
        private const int STATE_GAMEOVER = 4;   // 失败或胜利后的回合结束

        [Header("同步变量")] [Tooltip("当前波次，由房主同步到所有人")] [UdonSynced] private int syncedWave = 0;
        [Tooltip("当前阶段枚举值，由房主同步")] [UdonSynced] private int syncedState = STATE_LOBBY;
        [Tooltip("休息阶段结束时间戳，房主计算后同步")] [UdonSynced] private float syncedRestEnd = 0f;

        [Header("组件引用")] [Tooltip("准备区检测器，用于提取参与者")] public TD_JoinZone joinZone;
        [Tooltip("所有敌人生成器，会在每个波次被调用")] public TD_Spawner[] spawners;
        [Tooltip("可供敌人攻击的掩体")] public TD_Barrier[] barriers;
        [Tooltip("可选战斗区触发器，用于检测玩家全灭")] public TD_GameAreaTrigger gameAreaTrigger;
        [Tooltip("伤害飘字管理器供敌人调用")] public TD_DamagePopupManager popupManager;

        [Header("界面元素")] public TextMeshProUGUI waveLabel;
        public TextMeshProUGUI stateLabel;
        public TextMeshProUGUI timerLabel;

        [Header("阶段配置")] [Tooltip("准备阶段持续秒数")] public float prepareDuration = 5f;
        [Tooltip("休息阶段持续秒数")] public float restDuration = 12f;
        [Tooltip("失败后回到大厅的延迟")] public float resetDelay = 8f;

        [Header("玩法参数")] [Tooltip("基础敌人血量")] public int baseEnemyHp = 120;
        [Tooltip("每波血量增幅")] public int hpPerWave = 30;
        [Tooltip("每名玩家额外血量")] public int hpPerPlayer = 20;

        // ====== 本地缓存 ======
        private int localState = STATE_LOBBY;
        private int localWave = 0;
        private float localRestEnd = 0f;
        private bool hasActiveGame = false;
        private float resetTime = 0f;

        private VRCPlayerApi[] participantSnapshot = new VRCPlayerApi[32];
        private int participantCount = 0;

        private int aliveEnemyCount = 0;

        private readonly string[] stateTexts =
        {
            "等待中", "准备中", "战斗中", "休息中", "结束"
        };

        private void Start()
        {
            // 自动给战斗区触发器注入引用
            if (gameAreaTrigger != null)
            {
                gameAreaTrigger.gameManager = this;
            }

            ApplySyncedState();
            UpdateUI();
        }

        private void Update()
        {
            if (localState == STATE_REST)
            {
                // 客户端根据同步的结束时间计算剩余秒数
                float remain = Mathf.Max(0f, localRestEnd - Time.time);
                if (timerLabel != null)
                {
                    timerLabel.text = remain.ToString("F1");
                }

                if (Networking.IsMaster && hasActiveGame && remain <= 0f)
                {
                    StartNextWave();
                }
            }
            else if (localState == STATE_PREPARE)
            {
                float remain = Mathf.Max(0f, localRestEnd - Time.time);
                if (timerLabel != null)
                {
                    timerLabel.text = remain.ToString("F1");
                }

                if (Networking.IsMaster && remain <= 0f)
                {
                    BeginWave();
                }
            }
            else if (localState == STATE_GAMEOVER)
            {
                if (Networking.IsMaster && resetTime > 0f && Time.time >= resetTime)
                {
                    ReturnToLobby();
                }
            }
        }

        /// <summary>
        /// 供按钮触发的开始函数，仅允许房主执行。
        /// </summary>
        public void RequestStartGame()
        {
            if (!Networking.IsMaster) return;
            if (localState != STATE_LOBBY && localState != STATE_GAMEOVER) return;

            CaptureParticipants();
            if (participantCount == 0)
            {
                Debug.LogWarning("没有玩家站在准备区，无法开始");
                return;
            }

            hasActiveGame = true;
            syncedWave = 0;
            ChangeState(STATE_PREPARE, Time.time + prepareDuration);

            // 通知生成器准备数据
            foreach (TD_Spawner spawner in spawners)
            {
                if (spawner != null)
                {
                    spawner.ResetSpawner();
                }
            }
        }

        private void CaptureParticipants()
        {
            if (joinZone != null)
            {
                participantCount = joinZone.FillSnapshot(participantSnapshot);
            }
            else
            {
                participantCount = 0;
            }

            for (int i = participantCount; i < participantSnapshot.Length; i++)
            {
                participantSnapshot[i] = null;
            }

            // 确保只有房主执行一次传送逻辑
            if (!Networking.IsMaster) return;

            Vector3 spawnPosition = transform.position;
            Quaternion spawnRotation = transform.rotation;
            for (int i = 0; i < participantCount; i++)
            {
                VRCPlayerApi player = participantSnapshot[i];
                if (Utilities.IsValid(player))
                {
                    player.TeleportTo(spawnPosition, spawnRotation);
                }
            }
        }

        private void BeginWave()
        {
            if (!Networking.IsMaster) return;
            syncedWave++;
            ChangeState(STATE_WAVE, 0f);

            aliveEnemyCount = 0;
            int calculatedHp = baseEnemyHp + hpPerWave * (syncedWave - 1) + hpPerPlayer * Mathf.Max(0, participantCount - 1);

            foreach (TD_Spawner spawner in spawners)
            {
                if (spawner != null)
                {
                    spawner.StartWave(this, syncedWave, participantCount, calculatedHp);
                }
            }
        }

        private void StartNextWave()
        {
            if (!Networking.IsMaster) return;
            ChangeState(STATE_PREPARE, Time.time + prepareDuration);
        }

        private void EnterRestPhase()
        {
            if (!Networking.IsMaster) return;
            ChangeState(STATE_REST, Time.time + restDuration);

            foreach (TD_Barrier barrier in barriers)
            {
                if (barrier != null)
                {
                    barrier.Repair();
                }
            }
        }

        private void ReturnToLobby()
        {
            ChangeState(STATE_LOBBY, 0f);
            hasActiveGame = false;
            syncedWave = 0;
            aliveEnemyCount = 0;
            participantCount = 0;
            resetTime = 0f;
            RequestSerialization();
        }

        private void ChangeState(int newState, float timeStamp)
        {
            syncedState = newState;
            syncedRestEnd = timeStamp;
            ApplySyncedState();
            RequestSerialization();
        }

        private void ApplySyncedState()
        {
            localState = syncedState;
            localWave = syncedWave;
            localRestEnd = syncedRestEnd;

            if (stateLabel != null && localState >= 0 && localState < stateTexts.Length)
            {
                stateLabel.text = stateTexts[localState];
            }

            UpdateUI();
        }

        private void UpdateUI()
        {
            if (waveLabel != null)
            {
                waveLabel.text = localWave.ToString();
            }
        }

        public override void OnDeserialization()
        {
            ApplySyncedState();
        }

        /// <summary>
        /// 由敌人生成器在生成时调用，记录存活数量。
        /// </summary>
        public void NotifyEnemySpawned()
        {
            aliveEnemyCount++;
        }

        /// <summary>
        /// 由敌人死亡事件调用，检查是否需要进入休息阶段。
        /// </summary>
        public void NotifyEnemyDead()
        {
            if (!Networking.IsMaster) return;
            aliveEnemyCount = Mathf.Max(0, aliveEnemyCount - 1);
            if (aliveEnemyCount == 0 && localState == STATE_WAVE)
            {
                EnterRestPhase();
            }
        }

        /// <summary>
        /// 掩体被摧毁时调用，如果全部掩体损坏则直接失败。
        /// </summary>
        public void NotifyBarrierDestroyed()
        {
            if (!Networking.IsMaster) return;
            for (int i = 0; i < barriers.Length; i++)
            {
                TD_Barrier barrier = barriers[i];
                if (barrier != null && !barrier.IsBroken)
                {
                    return;
                }
            }

            TriggerGameOver(false);
        }

        /// <summary>
        /// 战斗区域检测到没有玩家时调用。
        /// </summary>
        public void NotifyPlayersDefeated()
        {
            if (!Networking.IsMaster) return;
            TriggerGameOver(false);
        }

        /// <summary>
        /// 手动触发胜利 / 失败。
        /// </summary>
        /// <param name="victory">是否胜利</param>
        public void TriggerGameOver(bool victory)
        {
            if (!Networking.IsMaster) return;
            ChangeState(STATE_GAMEOVER, 0f);
            resetTime = Time.time + resetDelay;
            hasActiveGame = false;
        }

        /// <summary>
        /// 提供给敌人选择攻击目标的帮助函数，返回离敌人最近且未破坏的掩体。
        /// </summary>
        /// <param name="position">敌人位置</param>
        /// <returns>可攻击的掩体引用</returns>
        public TD_Barrier GetNearestBarrier(Vector3 position)
        {
            TD_Barrier best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < barriers.Length; i++)
            {
                TD_Barrier barrier = barriers[i];
                if (barrier == null || barrier.IsBroken)
                {
                    continue;
                }

                float d = Vector3.Distance(position, barrier.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = barrier;
                }
            }

            return best;
        }

        public int ParticipantCount => participantCount;

        /// <summary>
        /// 根据索引获取参战玩家引用。
        /// </summary>
        public VRCPlayerApi GetParticipant(int index)
        {
            if (index < 0 || index >= participantCount)
            {
                return null;
            }

            return participantSnapshot[index];
        }

        /// <summary>
        /// 返回内部缓存的快照数组。注意：内容仅供只读使用。
        /// </summary>
        public VRCPlayerApi[] GetParticipants()
        {
            return participantSnapshot;
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            // 若玩家离开且属于快照，则重新计算失败条件
            if (!hasActiveGame) return;
            bool stillHasParticipant = false;
            for (int i = 0; i < participantCount; i++)
            {
                VRCPlayerApi cached = participantSnapshot[i];
                if (Utilities.IsValid(cached) && cached != player)
                {
                    stillHasParticipant = true;
                    break;
                }
            }

            if (!stillHasParticipant)
            {
                TriggerGameOver(false);
            }
        }
    }
}

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

namespace JumpUp.TD
{
    /// <summary>
    /// 准备区触发器：记录站入区域的玩家数量，并提供快照给游戏管理器。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TD_JoinZone : UdonSharpBehaviour
    {
        [Tooltip("可选 UI 文本，用于实时显示已准备人数")] public TextMeshProUGUI readyLabel;

        private readonly VRCPlayerApi[] players = new VRCPlayerApi[32];
        private int playerCount = 0;

        private readonly VRCPlayerApi[] snapshotBuffer = new VRCPlayerApi[32];

        private void Start()
        {
            Collider c = GetComponent<Collider>();
            if (c != null)
            {
                c.isTrigger = true;
            }
            UpdateLabel();
        }

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player)) return;
            AddPlayer(player);
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player)) return;
            RemovePlayer(player);
        }

        private void AddPlayer(VRCPlayerApi player)
        {
            if (ContainsPlayer(player)) return;

            players[playerCount] = player;
            playerCount++;
            UpdateLabel();
        }

        private void RemovePlayer(VRCPlayerApi player)
        {
            for (int i = 0; i < playerCount; i++)
            {
                if (players[i] == player)
                {
                    for (int j = i; j < playerCount - 1; j++)
                    {
                        players[j] = players[j + 1];
                    }

                    players[playerCount - 1] = null;
                    playerCount--;
                    UpdateLabel();
                    return;
                }
            }
        }

        private bool ContainsPlayer(VRCPlayerApi player)
        {
            for (int i = 0; i < playerCount; i++)
            {
                if (players[i] == player)
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdateLabel()
        {
            if (readyLabel != null)
            {
                readyLabel.text = playerCount.ToString();
            }
        }

        /// <summary>
        /// 填充快照数组并返回有效玩家数量。
        /// </summary>
        /// <param name="buffer">外部数组，长度需 >= 32</param>
        /// <returns>有效数量</returns>
        public int FillSnapshot(VRCPlayerApi[] buffer)
        {
            int length = Mathf.Min(playerCount, buffer.Length);
            for (int i = 0; i < length; i++)
            {
                buffer[i] = players[i];
            }
            return length;
        }

        /// <summary>
        /// 生成内部快照，用于外部快速访问。
        /// </summary>
        /// <returns>快照引用（注意：内容在下一次调用 FillSnapshot 前保持有效）</returns>
        public VRCPlayerApi[] GetSnapshot()
        {
            int length = FillSnapshot(snapshotBuffer);
            for (int i = length; i < snapshotBuffer.Length; i++)
            {
                snapshotBuffer[i] = null;
            }
            return snapshotBuffer;
        }

        public int ReadyCount => playerCount;
    }
}

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JumpUp.TD
{
    /// <summary>
    /// 战斗区触发器：用于检测是否还有参与玩家在战斗区域内。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TD_GameAreaTrigger : UdonSharpBehaviour
    {
        [Tooltip("关联的游戏管理器")] public TD_GameManager gameManager;

        private readonly VRCPlayerApi[] trackedPlayers = new VRCPlayerApi[32];
        private int trackedCount = 0;

        private void Start()
        {
            Collider c = GetComponent<Collider>();
            if (c != null)
            {
                c.isTrigger = true;
            }
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
            for (int i = 0; i < trackedCount; i++)
            {
                if (trackedPlayers[i] == player)
                {
                    return;
                }
            }

            trackedPlayers[trackedCount] = player;
            trackedCount++;
        }

        private void RemovePlayer(VRCPlayerApi player)
        {
            for (int i = 0; i < trackedCount; i++)
            {
                if (trackedPlayers[i] == player)
                {
                    for (int j = i; j < trackedCount - 1; j++)
                    {
                        trackedPlayers[j] = trackedPlayers[j + 1];
                    }

                    trackedPlayers[trackedCount - 1] = null;
                    trackedCount--;
                    break;
                }
            }

            if (trackedCount == 0 && gameManager != null)
            {
                gameManager.NotifyPlayersDefeated();
            }
        }
    }
}

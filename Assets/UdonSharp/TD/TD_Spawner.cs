using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace JumpUp.TD
{
    /// <summary>
    /// 敌人生成器：负责按波次定时从对象池中激活敌人。
    /// </summary>
    public class TD_Spawner : UdonSharpBehaviour
    {
        [Tooltip("连接到的游戏控制器")] public TD_GameManager gameManager;
        [Tooltip("预先布置好的敌人池，每个元素都需要挂载 TD_Enemy")]
        public TD_Enemy[] enemyPool;
        [Tooltip("生成点，如果为空则使用当前物体位置")] public Transform[] spawnPoints;
        [Tooltip("生成间隔秒数")] public float spawnInterval = 1.5f;
        [Tooltip("每波额外生成的敌人数量")] public int enemiesPerWave = 5;

        private bool isActive = false;
        private float nextSpawnTime = 0f;
        private int enemiesToSpawn = 0;
        private int poolIndex = 0;

        private int cachedWave = 0;
        private int cachedPlayerCount = 0;
        private int cachedHp = 0;

        private void Start()
        {
            // 确保所有敌人禁用，等待生成时再启用
            for (int i = 0; i < enemyPool.Length; i++)
            {
                TD_Enemy enemy = enemyPool[i];
                if (enemy != null)
                {
                    enemy.gameObject.SetActive(false);
                }
            }
        }

        public void ResetSpawner()
        {
            isActive = false;
            enemiesToSpawn = 0;
        }

        public void StartWave(TD_GameManager manager, int wave, int playerCount, int baseHp)
        {
            if (!Networking.IsMaster) return;

            gameManager = manager;
            cachedWave = wave;
            cachedPlayerCount = playerCount;
            cachedHp = baseHp;

            enemiesToSpawn = enemiesPerWave + (wave - 1) * 2;
            isActive = true;
            nextSpawnTime = Time.time + 0.5f;
        }

        private void Update()
        {
            if (!Networking.IsMaster) return;
            if (!isActive) return;

            if (enemiesToSpawn <= 0)
            {
                isActive = false;
                return;
            }

            if (Time.time >= nextSpawnTime)
            {
                SpawnEnemy();
                nextSpawnTime = Time.time + spawnInterval;
            }
        }

        private void SpawnEnemy()
        {
            TD_Enemy enemy = GetNextEnemy();
            if (enemy == null)
            {
                Debug.LogWarning("对象池耗尽，无法继续生成敌人");
                return;
            }

            Transform point = spawnPoints != null && spawnPoints.Length > 0
                ? spawnPoints[Random.Range(0, spawnPoints.Length)]
                : transform;

            enemy.Spawn(gameManager, this, point.position, cachedWave, cachedPlayerCount, cachedHp);
            enemiesToSpawn--;

            if (gameManager != null)
            {
                gameManager.NotifyEnemySpawned();
            }
        }

        private TD_Enemy GetNextEnemy()
        {
            int length = enemyPool.Length;
            for (int i = 0; i < length; i++)
            {
                poolIndex++;
                if (poolIndex >= length)
                {
                    poolIndex = 0;
                }

                TD_Enemy candidate = enemyPool[poolIndex];
                if (candidate != null && !candidate.gameObject.activeSelf)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}

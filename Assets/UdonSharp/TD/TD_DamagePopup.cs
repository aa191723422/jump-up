using UdonSharp;
using UnityEngine;
using TMPro;

namespace JumpUp.TD
{
    /// <summary>
    /// 单个飘字的播放逻辑：上飘、渐隐并在结束后自动回收。
    /// </summary>
    public class TD_DamagePopup : UdonSharpBehaviour
    {
        [Tooltip("文本组件")] public TextMeshPro textMesh;
        [Tooltip("持续时间")] public float lifeTime = 1f;
        [Tooltip("上升速度")] public float floatSpeed = 1f;
        [Tooltip("初始缩放")] public float startScale = 1f;

        private float spawnTime;
        private Color baseColor;

        private void Awake()
        {
            if (textMesh != null)
            {
                baseColor = textMesh.color;
            }
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!gameObject.activeSelf) return;
            float elapsed = Time.time - spawnTime;
            if (elapsed >= lifeTime)
            {
                gameObject.SetActive(false);
                return;
            }

            transform.position += Vector3.up * (floatSpeed * Time.deltaTime);
            float alpha = Mathf.Clamp01(1f - elapsed / lifeTime);
            if (textMesh != null)
            {
                Color c = textMesh.color;
                c.a = alpha;
                textMesh.color = c;
            }
        }

        public void Show(Vector3 position, int value, Color color, bool isCrit)
        {
            transform.position = position;
            transform.localScale = Vector3.one * startScale * (isCrit ? 1.3f : 1f);
            spawnTime = Time.time;

            if (textMesh != null)
            {
                textMesh.text = value.ToString();
                textMesh.color = color;
            }

            gameObject.SetActive(true);
        }
    }
}

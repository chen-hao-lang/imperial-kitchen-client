using UnityEngine;

namespace Applications
{
    public class GameEntry : IHotUpdateEntry
    {
        public void Start()
        {
            Debug.Log("[GameEntry] 热更新逻辑启动，版本：v1.0.0");
        }

        public void Update(float deltaTime)
        {
        }

        public void OnApplicationQuit()
        {
        }
    }
}
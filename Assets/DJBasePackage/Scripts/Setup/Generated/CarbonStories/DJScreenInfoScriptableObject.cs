using UnityEngine;

namespace CarbonStories
{
    [CreateAssetMenu(fileName = "DJScreenInfo", menuName = "CarbonStories/DJScreenInfo", order = 1)]
    public class DJScreenInfoScriptableObject : ScriptableObject
    {
        public string Name;
        public float LoadingTime;
        public float UnloadingTime;
    }
}

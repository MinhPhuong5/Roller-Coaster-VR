using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CityPeople
{
    [CreateAssetMenu(menuName = "ScriptableObjects/ToolConfig")]
    public class ToolConfig : ScriptableObject
    {
        [System.Serializable]
        public class ToolEntry
        {
            [Tooltip("Animation clip state name")]
            public string clipName;
            [Tooltip("Prefab tool model")]
            public GameObject prefab;
        }

        [Tooltip("List of clip-to-prefab mappings for tools")]
        public List<ToolEntry> entries = new List<ToolEntry>();
    }
}
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CityPeople
{
    public class SwapAndToolController : MonoBehaviour
    {
        private Animator animator;
        [Tooltip("Swap animation state name")]
        public string swapState = "swap";
        [Tooltip("Loop duration for tool animations")]
        public float toolDuration = 5f;
        [Tooltip("Reference to the ToolConfig asset containing clip-to-prefab mappings")]
        public ToolConfig toolConfig;
        [Tooltip("Name of the child transform to attach tools to (e.g. 'bip Xtra_tool')")]
        public string attachPointName = "bip Xtra_tool";

        private const float fadeDuration = 0.2f;
        private float halfSwapTime;
        private List<ToolConfig.ToolEntry> toolEntries;
        private GameObject currentTool;
        private Transform toolParent;

        void Awake()
        {
            animator = GetComponent<Animator>();
            // Calculate half of swap animation duration
            float swapLength = GetClipLength(swapState) * 0.8f ;
            halfSwapTime = swapLength * 0.5f;
            // Prepare tool entries excluding swap state
            toolEntries = toolConfig.entries
                .Where(e => e.clipName != swapState)
                .ToList();

            // Find attach point anywhere in the hierarchy
            toolParent = FindChildByName(transform, attachPointName)?.transform;
            if (toolParent == null)
                Debug.LogWarning($"SwapAndToolController: Attach point '{attachPointName}' not found in hierarchy.");
        }

        void Start()
        {
            StartCoroutine(PlayCycle());
        }

        IEnumerator PlayCycle()
        {
            while (true)
            {
                animator.CrossFade(swapState, fadeDuration);
                yield return new WaitForSeconds(halfSwapTime);

                if (currentTool != null)
                    Destroy(currentTool);

                var entry = toolEntries[Random.Range(0, toolEntries.Count)];
                if (entry.prefab != null && toolParent != null)
                {
                    currentTool = Instantiate(entry.prefab, toolParent.position, toolParent.rotation, toolParent);
                }

                yield return new WaitForSeconds(halfSwapTime);

                animator.CrossFade(entry.clipName, fadeDuration);
                yield return new WaitForSeconds(toolDuration*2f);
            }
        }

        /// <summary>
        /// Recursively finds a child GameObject by name.
        /// </summary>
        public GameObject FindChildByName(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child.gameObject;
                var result = FindChildByName(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        float GetClipLength(string stateName)
        {
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name == stateName)
                    return clip.length;
            return 0.5f;
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CityPeople
{
    /// <summary>
    /// Provides references to additional Unity objects that should be explicitly included during package export.
    /// </summary>
    /// <remarks> This helper script is only relevant for the developer of this package. The user is safe to delete this file.</remarks>
    public class PackageExporterRefs : MonoBehaviour
    {
        [Header("Force Include These Objects")]
        public GameObject[] extraPrefabs;

        [Header("Force Include These Textures")]
        public Texture[] textureVariants;

        [Header("Force Include These Materials")]
        public Material[] altMaterials;

        [Header("Force Include These Animations")]
        public AnimationClip[] extraAnims;

        [Header("Documentation & Extras")]
        // "Object" is the universal type. It accepts PDFs, Text, HTML, etc.
        public Object[] extraFiles;
    }
}
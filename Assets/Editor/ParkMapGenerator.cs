using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ParkMapGenerator
{
    private const string RootName = "Park Map (Generated)";
    private const string MaterialFolder = "Assets/Generated/ParkMap";

    [MenuItem("Tools/Roller Coaster VR/Build Park Map")]
    private static void Build()
    {
        GameObject oldRoot = GameObject.Find(RootName);
        if (oldRoot != null)
        {
            Undo.DestroyObjectImmediate(oldRoot);
        }

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build park map");

        Material grass = Material("Park Grass", new Color(0.19f, 0.45f, 0.20f));
        Material path = Material("Park Path", new Color(0.65f, 0.53f, 0.38f));
        Material red = Material("Park Red", new Color(0.74f, 0.12f, 0.09f));
        Material cream = Material("Park Cream", new Color(1f, 0.78f, 0.38f));
        Material wood = Material("Park Wood", new Color(0.31f, 0.16f, 0.06f));
        Material leaves = Material("Park Leaves", new Color(0.08f, 0.32f, 0.10f));
        Material steel = Material("Park Steel", new Color(0.22f, 0.25f, 0.30f));

        GameObject ground = Cube("Park Ground", new Vector3(0f, -0.3f, 0f), new Vector3(80f, 0.6f, 100f), grass, root.transform);
        ground.tag = "Ground";
        Cube("Main Path", new Vector3(0f, 0.03f, 1f), new Vector3(9f, 0.08f, 84f), path, root.transform);
        Cube("Left Path", new Vector3(-18f, 0.03f, 14f), new Vector3(36f, 0.08f, 7f), path, root.transform);
        Cube("Right Path", new Vector3(18f, 0.03f, 31f), new Vector3(36f, 0.08f, 7f), path, root.transform);

        BuildEntrance(root.transform, red, cream);
        BuildTicketBooth(root.transform, red, cream, wood);
        BuildCoasterZone(root.transform, red, cream, steel);
        BuildFence(root.transform, wood);
        BuildTrees(root.transform, wood, leaves);

        GameObject spawn = new GameObject("Park Spawn");
        spawn.transform.SetParent(root.transform);
        spawn.transform.SetPositionAndRotation(new Vector3(0f, 0.2f, -37f), Quaternion.identity);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    private static void BuildEntrance(Transform parent, Material red, Material cream)
    {
        Cube("Entrance Pillar L", new Vector3(-8f, 3.5f, -39f), new Vector3(2f, 7f, 2f), red, parent);
        Cube("Entrance Pillar R", new Vector3(8f, 3.5f, -39f), new Vector3(2f, 7f, 2f), red, parent);
        Cube("Entrance Header", new Vector3(0f, 7f, -39f), new Vector3(18f, 2f, 2f), cream, parent);
        Cube("ROLLER COASTER VR", new Vector3(0f, 7f, -40.05f), new Vector3(12f, 0.9f, 0.1f), red, parent);
    }

    private static void BuildTicketBooth(Transform parent, Material red, Material cream, Material wood)
    {
        Vector3 p = new Vector3(-13f, 1.5f, -26f);
        Cube("Ticket Booth", p, new Vector3(6f, 3f, 4f), cream, parent);
        Cube("Ticket Booth Roof", p + Vector3.up * 2f, new Vector3(7f, 1f, 5f), red, parent);
        Cube("Ticket Window", p + new Vector3(0f, 0.2f, -2.03f), new Vector3(3f, 1.3f, 0.1f), wood, parent);
        Cylinder("Ticket Sign", p + new Vector3(0f, 4f, 0f), 1.2f, 0.4f, red, parent).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private static void BuildCoasterZone(Transform parent, Material red, Material cream, Material steel)
    {
        Cube("Coaster Plaza", new Vector3(0f, 0.05f, 31f), new Vector3(38f, 0.1f, 26f), cream, parent);
        for (int i = -3; i <= 3; i++)
        {
            Cylinder("Coaster Support", new Vector3(i * 5f, 5f, 31f), 0.35f, 10f, steel, parent);
        }

        Cube("Coaster Track Base", new Vector3(0f, 10f, 31f), new Vector3(35f, 0.35f, 0.7f), steel, parent);
        Cube("Coaster Station", new Vector3(-12f, 2f, 23f), new Vector3(12f, 4f, 8f), red, parent);
        Cube("Coaster Station Roof", new Vector3(-12f, 4.5f, 23f), new Vector3(14f, 1f, 10f), cream, parent);
        Cube("Ride Start Marker", new Vector3(-12f, 0.15f, 18.5f), new Vector3(5f, 0.2f, 1f), red, parent);
    }

    private static void BuildFence(Transform parent, Material material)
    {
        for (int x = -36; x <= 36; x += 6)
        {
            Post(new Vector3(x, 1.2f, -45f), parent, material);
            Post(new Vector3(x, 1.2f, 45f), parent, material);
        }

        for (int z = -39; z <= 39; z += 6)
        {
            Post(new Vector3(-36f, 1.2f, z), parent, material);
            Post(new Vector3(36f, 1.2f, z), parent, material);
        }
    }

    private static void BuildTrees(Transform parent, Material trunk, Material leaves)
    {
        Vector3[] positions =
        {
            new(-28f, 0f, -30f), new(28f, 0f, -29f), new(-27f, 0f, -8f), new(26f, 0f, -5f),
            new(-28f, 0f, 12f), new(28f, 0f, 14f), new(-29f, 0f, 36f), new(27f, 0f, 38f)
        };

        foreach (Vector3 p in positions)
        {
            Cylinder("Tree Trunk", p + Vector3.up * 2f, 0.45f, 4f, trunk, parent);
            Sphere("Tree Crown", p + Vector3.up * 5f, Vector3.one * 4f, leaves, parent);
        }
    }

    private static void Post(Vector3 position, Transform parent, Material material) =>
        Cylinder("Fence Post", position, 0.13f, 2.4f, material, parent);

    private static GameObject Cube(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent);
        obj.transform.SetPositionAndRotation(position, Quaternion.identity);
        obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        return obj;
    }

    private static GameObject Cylinder(string name, Vector3 position, float radius, float height, Material material, Transform parent)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        obj.name = name;
        obj.transform.SetParent(parent);
        obj.transform.SetPositionAndRotation(position, Quaternion.identity);
        obj.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        obj.GetComponent<Renderer>().sharedMaterial = material;
        return obj;
    }

    private static GameObject Sphere(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = name;
        obj.transform.SetParent(parent);
        obj.transform.SetPositionAndRotation(position, Quaternion.identity);
        obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        return obj;
    }

    private static Material Material(string name, Color color)
    {
        EnsureMaterialFolder();
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureMaterialFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Generated"))
        {
            AssetDatabase.CreateFolder("Assets", "Generated");
        }

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets/Generated", "ParkMap");
        }
    }
}

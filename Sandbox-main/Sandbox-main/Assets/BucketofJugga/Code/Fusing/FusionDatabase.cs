using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Sandbox/Fusion Database", fileName = "FusionDatabase")]
public class FusionDatabase : ScriptableObject
{
    [Serializable]
    public struct Recipe
    {
        public string itemA;
        public string itemB;
        public GameObject resultPrefab;
    }

    [Tooltip("all fusion recipes. itemA + itemB will fuse into resultprefab.")]
    public List<Recipe> recipes = new List<Recipe>();

    public bool TryGetResult(string a, string b, out GameObject result)
    {
        result = null;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        if (a == b) return false; // blocks same-prefab/type fusion by default

        // order-independent match
        for (int i = 0; i < recipes.Count; i++)
        {
            var r = recipes[i];
            if (r.resultPrefab == null) continue;

            bool match =
                (r.itemA == a && r.itemB == b) ||
                (r.itemA == b && r.itemB == a);

            if (match)
            {
                result = r.resultPrefab;
                return true;
            }
        }

        return false;
    }
}
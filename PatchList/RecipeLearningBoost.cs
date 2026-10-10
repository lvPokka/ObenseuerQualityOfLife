using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using TMPro;
using System.Collections.Generic;

namespace ObenseuerQualityOfLife.PatchList
{
    [HarmonyPatch]
    public static class RecipeLearningBoost
    {
        [HarmonyPatch(typeof(RecipeDatabase), "FetchRecipes", new System.Type[] { typeof(RecipeType[]), typeof(RecipeCondition[]), typeof(string), typeof(OS.Items.ItemReadable.TagMode), typeof(UnityEngine.Vector2Int) })]
        public static class RecipeDatabase_FetchRecipes_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(ref System.Collections.Generic.List<Recipe> __result)
            {
                if (__result == null || __result.Count == 0 || RecipeController.instance == null) return;

                var fresh = new System.Collections.Generic.List<Recipe>();
                foreach (var r in __result)
                {
                    if (r == null) continue;
                    if (!RecipeController.instance.HasRecipe(r) && !RecipeController.instance.HasLearnedRecipe(r))
                    {
                        if (Random.value < 0.5f) fresh.Add(r);
                    }
                }

                if (fresh.Count > 0)
                {
                    __result = fresh;
                }
            }
        }

        [HarmonyPatch(typeof(RecipeController), "NoNewRecipesNotification")]

        public static class RecipeController_NoNewRecipesNotification_Patch
        {
            // ������ ����� "There was only old recipes" ������ �������� �������.
            [HarmonyPrefix]
            public static bool Prefix(int amount, string name)
            {
                Notifications.instance.CreateNotification("Old recipes", "There was only old recipes");
                return false;
            }
        }
    }
}

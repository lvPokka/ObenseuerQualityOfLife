using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using TMPro;
using System.Collections.Generic;

namespace ObenseuerQualityOfLife.PatchList
{
    [HarmonyPatch]
    public static class LearnedRecipeDisplay
    {
        [HarmonyPatch(typeof(CategoryTitle), "Init")]
        public static class CategoryTitle_Init_Patch
        {
            private static TaskItem[] allTaskItems = null;
            private static MethodInfo _getItemTypeMethod = null;

            // ���� ��� �������� ������������ total ��������, ����� �� ������ ����� � ��������� ������ ���
            private static System.Collections.Generic.Dictionary<TaskItem.Type, int> _recipeTotalCache = new System.Collections.Generic.Dictionary<TaskItem.Type, int>();
            private static System.Collections.Generic.Dictionary<TaskItem.Type, int> _taskItemTotalCache = new System.Collections.Generic.Dictionary<TaskItem.Type, int>();

            [HarmonyPostfix]
            public static void Postfix(CategoryTitle __instance, TaskItem.Type itemType, string title)
            {
                int total = 0;
                int learned = 0;
                bool isRecipeCategory = false;

                try
                {
                    if (RecipeDatabase.instance != null && RecipeController.instance != null)
                    {
                        if (_getItemTypeMethod == null)
                        {
                            _getItemTypeMethod = typeof(RecipeController).GetMethod("GetItemType", BindingFlags.NonPublic | BindingFlags.Instance);
                        }

                        if (_getItemTypeMethod != null)
                        {
                            // ���� �� ��� �� ������� total ��� ���� ��������� �������� - ������� � ��������
                            if (!_recipeTotalCache.TryGetValue(itemType, out total))
                            {
                                total = 0;
                                object[] args = new object[1];
                                foreach (var r in RecipeDatabase.Recipes)
                                {
                                    if (r != null)
                                    {
                                        args[0] = r.Type;
                                        TaskItem.Type rType = (TaskItem.Type)_getItemTypeMethod.Invoke(RecipeController.instance, args);
                                        if (rType == itemType)
                                        {
                                            total++;
                                        }
                                    }
                                }
                                _recipeTotalCache[itemType] = total;
                            }

                            if (total > 0)
                            {
                                isRecipeCategory = true;

                                // ������� ������ ��������� �������� - �� ������, �� ��� ����� ���������� 1 ������ args
                                object[] learnedArgs = new object[1];
                                foreach (var r in RecipeController.instance.GetAllRecipes())
                                {
                                    if (r != null)
                                    {
                                        learnedArgs[0] = r.Type;
                                        TaskItem.Type rType = (TaskItem.Type)_getItemTypeMethod.Invoke(RecipeController.instance, learnedArgs);
                                        if (rType == itemType)
                                        {
                                            learned++;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogError($"[CategoryTitle_Init] Recipe check error: {e}");
                }

                if (!isRecipeCategory)
                {
                    // �������� total ��� ������� TaskItem
                    if (!_taskItemTotalCache.TryGetValue(itemType, out total))
                    {
                        total = 0;
                        if (allTaskItems == null)
                        {
                            allTaskItems = Resources.LoadAll<TaskItem>("");
                            if (allTaskItems == null || allTaskItems.Length == 0)
                            {
                                allTaskItems = Resources.FindObjectsOfTypeAll<TaskItem>();
                            }
                        }

                        if (allTaskItems != null)
                        {
                            foreach (var t in allTaskItems)
                            {
                                if (t != null && t.itemType == itemType) total++;
                            }
                        }
                        _taskItemTotalCache[itemType] = total;
                    }

                    if (TaskItemsManager.instance != null && TaskItemsManager.instance.taskItems != null)
                    {
                        foreach (var tInfo in TaskItemsManager.instance.taskItems)
                        {
                            if (tInfo != null && tInfo.taskItem != null && tInfo.taskItem.itemType == itemType)
                            {
                                learned++;
                            }
                        }
                    }
                }

                // Plugin.Log.LogInfo($"[CategoryTitle_Init] Type: {itemType}, Title: {title}, Learned: {learned}, Total: {total}"); // ����� ���������������� ��� ����������

                if (total > 0 || learned > 0)
                {
                    string displayTotal = total > 0 ? total.ToString() : "?";
                    __instance.title.text = $"{title} ({learned}/{displayTotal})";
                }
            }
        }
    }
}

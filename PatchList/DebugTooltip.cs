using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using TMPro;
using System.Collections.Generic;

namespace ObenseuerQualityOfLife.PatchList
{
    [HarmonyPatch]
    public static class DebugTooltip
    {
        [HarmonyPatch(typeof(ToolTip))]
        public static class ToolTip_ConstructDataString_Patch
        {
            public static object[] lastMetas;

            [HarmonyPatch("ConstructDataString")]
            [HarmonyPrefix]
            public static void Prefix(object[] metas)
            {
                lastMetas = metas;
            }
        }

        [HarmonyPatch(typeof(TooltipUI))]
        public static class TooltipUIPatches
        {
            [HarmonyPatch("UpdateTooltip")]
            [HarmonyPrefix]
            public static void UpdateTooltip_Prefix(ref string tooltipDetails)
            {
                if (Plugin.showTechnicalTooltip && ToolTip.instance != null && ToolTip.instance.currentItem != null)
                {
                    Item item = ToolTip.instance.currentItem;
                    tooltipDetails += $"\n<size=70%>";

                    foreach (FieldInfo field in item.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        try
                        {
                            object val = field.GetValue(item);
                            if (val != null && (val.GetType().IsPrimitive || val is string || val.GetType().IsEnum))
                            {
                                tooltipDetails += $"\n<color=blue>- {field.Name}: {val}</color>";
                            }
                        }
                        catch (System.Exception ex) { Plugin.Log.LogWarning($"[TooltipUI] Error reading field {field.Name}: {ex.Message}"); }
                    }

                    object[] activeMeta = null;
                    if (ToolTip_ConstructDataString_Patch.lastMetas != null && ToolTip_ConstructDataString_Patch.lastMetas.Length > 0)
                    {
                        activeMeta = ToolTip_ConstructDataString_Patch.lastMetas;
                    }
                    else if (ItemData.currentHoverItemData != null && ItemData.currentHoverItemData.meta != null && ItemData.currentHoverItemData.meta.Length > 0)
                    {
                        activeMeta = ItemData.currentHoverItemData.meta;
                    }

                    if (activeMeta != null && activeMeta.Length > 0)
                    {
                        // ���������� ��� ����-�������, ������������� � ����� ��������
                        foreach (var metaObj in activeMeta)
                        {
                            if (metaObj != null)
                            {
                                // ������� ��� ����-������� (��������, ItemSkinData, ItemDurabilityData, ItemLiquidData)
                                string metaType = metaObj.GetType().Name;
                                tooltipDetails += $"\n<color=blue>[{metaType}]</color>";

                                foreach (FieldInfo field in metaObj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                                {
                                    try
                                    {
                                        object val = field.GetValue(metaObj);
                                        if (val != null)
                                        {
                                            if (val.GetType().IsPrimitive || val is string || val.GetType().IsEnum)
                                            {
                                                tooltipDetails += $"\n<color=blue>- {field.Name}: {val}</color>";
                                            }
                                            else if (val.GetType().Name == "ItemReference")
                                            {
                                                FieldInfo nameField = val.GetType().GetField("name");
                                                FieldInfo idField = val.GetType().GetField("ID");
                                                string itemName = nameField?.GetValue(val) as string;
                                                int itemID = idField != null ? (int)idField.GetValue(val) : 0;
                                                tooltipDetails += $"\n<color=blue>- {field.Name}: {itemName} (ID: {itemID})</color>";
                                            }
                                        }
                                    }
                                    catch (System.Exception ex) { Plugin.Log.LogWarning($"[TooltipUI] Error reading field {field.Name} on {metaObj.GetType().Name}: {ex.Message}"); }
                                }

                                foreach (PropertyInfo prop in metaObj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                                {
                                    try
                                    {
                                        object val = prop.GetValue(metaObj);
                                        if (val != null)
                                        {
                                            if (val.GetType().IsPrimitive || val is string || val.GetType().IsEnum)
                                            {
                                                tooltipDetails += $"\n<color=blue>- {prop.Name}: {val}</color>";
                                            }
                                            else if (val.GetType().Name == "ItemReference")
                                            {
                                                FieldInfo nameField = val.GetType().GetField("name");
                                                FieldInfo idField = val.GetType().GetField("ID");
                                                string itemName = nameField?.GetValue(val) as string;
                                                int itemID = idField != null ? (int)idField.GetValue(val) : 0;
                                                tooltipDetails += $"\n<color=blue>- {prop.Name}: {itemName} (ID: {itemID})</color>";
                                            }
                                        }
                                    }
                                    catch (System.Exception ex) { Plugin.Log.LogWarning($"[TooltipUI] Error reading property {prop.Name} on {metaObj.GetType().Name}: {ex.Message}"); }
                                }
                            }
                        }
                    }
                    tooltipDetails += "</size>";
                }
            }
        }



        [HarmonyPatch(typeof(ToolTip))]
        [HarmonyPatch]
        public static class ToolTipPatches
        {
            [HarmonyPatch("ConstructDataString")]
            [HarmonyPrefix]
            public static void ConstructDataString_Prefix(ItemData itemData)
            {
                // ������������� currentItem �� ���� ��� ������������� �����,
                // ����� ��� ���� UpdateTooltip ��� ������������ ���������� �������!
                if (itemData != null && itemData.item != null && ToolTip.instance != null)
                {
                    ToolTip.instance.currentItem = itemData.item;
                }
            }
        }
    }
}

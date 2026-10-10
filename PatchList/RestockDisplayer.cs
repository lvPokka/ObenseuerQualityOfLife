using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using TMPro;
using System.Collections.Generic;

namespace ObenseuerQualityOfLife.PatchList
{
    [HarmonyPatch]
    public static class RestockDisplayer
    {
        [HarmonyPatch(typeof(StoragePanelUI))]
        public static class StoragePanelUIPatches
        {
            private static FieldInfo nameField = typeof(StoragePanelUI).GetField("storageName", BindingFlags.NonPublic | BindingFlags.Instance);
            private static FieldInfo timeToRestockField = typeof(BaseShop).GetField("timeToRestock", BindingFlags.NonPublic | BindingFlags.Instance);

            [HarmonyPatch("UpdateStoragePanelUI")]
            [HarmonyPostfix]
            public static void UpdateStoragePanelUI_Postfix(StoragePanelUI __instance, Storage storage)
            {
                try
                {
                    if (storage == null) return;

                    Plugin.Log.LogInfo($"[UpdateStoragePanelUI] Opened for storage: {storage.title}");

                    if (nameField == null) return;

                    TMP_Text nameText = (TMP_Text)nameField.GetValue(__instance);
                    if (nameText == null) return;

                    string restockStr = "";

                    BaseShop baseShop = storage.GetComponent<BaseShop>();
                    Trade trade = storage.GetComponent<Trade>();

                    Plugin.Log.LogInfo($"[UpdateStoragePanelUI] BaseShop: {baseShop != null}, Trade: {trade != null}");

                    if (baseShop != null && trade == null) // Make sure it's strictly a BaseShop and not a Trade
                    {
                        if (baseShop.restockEveryDay)
                        {
                            restockStr = "Restocks daily";
                        }
                        else
                        {
                            if (timeToRestockField != null)
                            {
                                int timeToRestock = (int)timeToRestockField.GetValue(baseShop);
                                restockStr = FormatTime(timeToRestock);
                            }
                        }
                    }
                    else if (trade != null)
                    {
                        var spawnSettings = trade.traderMoneySpawnSettings;
                        if (spawnSettings != null)
                        {
                            Plugin.Log.LogInfo($"[UpdateStoragePanelUI] spawnSettings exist. respawnEnabled: {spawnSettings.respawnEnabled}, waitingSpawn: {spawnSettings.waitingSpawn}, spawnTime: {spawnSettings.spawnTime}, timetospawn: {spawnSettings.timetospawn}");

                            if (spawnSettings.respawnEnabled)
                            {
                                if (spawnSettings.waitingSpawn)
                                {
                                    if ((int)spawnSettings.spawnTime == 5) // SpawnTime.EveryDay
                                    {
                                        restockStr = "Restocks daily";
                                    }
                                    else
                                    {
                                        restockStr = FormatTime(spawnSettings.timetospawn);
                                    }
                                }
                                else
                                {
                                    restockStr = "Now";
                                }
                            }
                            else
                            {
                                restockStr = "No Auto-Restock"; // Just to verify it appears
                            }
                        }
                        else
                        {
                            Plugin.Log.LogInfo($"[UpdateStoragePanelUI] spawnSettings is NULL!");
                        }
                    }

                    if (!string.IsNullOrEmpty(restockStr))
                    {
                        Plugin.Log.LogInfo($"[UpdateStoragePanelUI] Appending text: {restockStr}");
                        nameText.richText = true;
                        nameText.enableWordWrapping = false;
                        nameText.overflowMode = TextOverflowModes.Overflow;
                        nameText.text += $"\n<size=60%><color=#aaaaaa>(Restock: {restockStr})</color></size>";
                    }
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogError($"[UpdateStoragePanelUI] Error: {e}");
                }
            }

            public static string FormatTime(int timeToRestock)
            {
                if (timeToRestock <= 0) return "Now";
                int d = timeToRestock / 86400;
                int h = (timeToRestock % 86400) / 3600;
                int m = (timeToRestock % 3600) / 60;

                if (d > 0) return $"{d}d {h}h {m}m";
                if (h > 0) return $"{h}h {m}m";
                return $"{m}m";
            }
        }

        [HarmonyPatch(typeof(FurnitureShopPanelUI))]
        public static class FurnitureShopPanelUIPatches
        {
            private static FieldInfo shopField = typeof(ShopBaseUI).GetField("currentShop", BindingFlags.NonPublic | BindingFlags.Instance);
            private static FieldInfo timeToRestockField = typeof(BaseShop).GetField("timeToRestock", BindingFlags.NonPublic | BindingFlags.Instance);
            private static FieldInfo nameField = typeof(FurnitureShopPanelUI).GetField("panelName", BindingFlags.NonPublic | BindingFlags.Instance);

            [HarmonyPatch("SetPanelInfo")]
            [HarmonyPostfix]
            public static void SetPanelInfo_Postfix(FurnitureShopPanelUI __instance, ShopBaseUI shopUI, string name)
            {
                try
                {
                    if (shopUI == null) return;

                    if (shopField == null) return;

                    BaseShop baseShop = (BaseShop)shopField.GetValue(shopUI);
                    if (baseShop == null) return;

                    string restockStr = "";

                    if (baseShop.restockEveryDay)
                    {
                        restockStr = "Restocks daily";
                    }
                    else
                    {
                        if (timeToRestockField != null)
                        {
                            int timeToRestock = (int)timeToRestockField.GetValue(baseShop);
                            restockStr = StoragePanelUIPatches.FormatTime(timeToRestock);
                        }
                    }

                    if (!string.IsNullOrEmpty(restockStr))
                    {
                        if (nameField != null)
                        {
                            TMP_Text nameText = (TMP_Text)nameField.GetValue(__instance);
                            if (nameText != null)
                            {
                                nameText.richText = true;
                                nameText.enableWordWrapping = false;
                                nameText.overflowMode = TextOverflowModes.Overflow;
                                nameText.text += $"\n<size=60%><color=#aaaaaa>Restock in: {restockStr}</color></size>";
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogError($"[FurnitureShopPanelUI] Error: {e}");
                }
            }
        }
    }
}

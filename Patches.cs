using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using TMPro;
using System.Collections.Generic;

namespace ObenseuerQualityOfLife
{
    [HarmonyPatch(typeof(ItemData))]
    public static class Patches
    {
        [HarmonyPatch("UpdateItem")]
        [HarmonyPostfix]
        public static void UpdateItem_Postfix(ItemData __instance)
        {
            if (__instance.item == null) return;
            
            // Находим или создаем наш собственный объект подсветки,
            // чтобы вообще не трогать оригинальную игровую логику
            Transform priceHighlightTransform = __instance.transform.Find("PriceHighlight");
            GameObject priceHighlightObj;
            Image priceHighlightImg;

            if (priceHighlightTransform == null)
            {
                priceHighlightObj = new GameObject("PriceHighlight");
                priceHighlightObj.transform.SetParent(__instance.transform, false);
                
                priceHighlightImg = priceHighlightObj.AddComponent<Image>();
                
                // Настраиваем RectTransform чтобы это была полоска снизу
                RectTransform rect = priceHighlightObj.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.1f, 0); // Левый нижний угол (чуть отступаем от края)
                rect.anchorMax = new Vector2(0.9f, 0); // Правый нижний угол (чуть отступаем от края)
                rect.pivot = new Vector2(0.5f, 0);
                rect.sizeDelta = new Vector2(0, 2); // Высота полоски всего 2 пикселя
                rect.anchoredPosition = new Vector2(0, 1); // Чуть приподнимем над самым низом
            }
            else
            {
                priceHighlightObj = priceHighlightTransform.gameObject;
                priceHighlightImg = priceHighlightObj.GetComponent<Image>();
            }

            if (!Plugin.showHighlight)
            {
                priceHighlightObj.SetActive(false);
                return;
            }

            float price = __instance.item.GetItemSalePriceUnrounded(__instance.meta) * __instance.amount;
            
            if (price >= Plugin.MinRedPrice.Value)
            {
                priceHighlightObj.SetActive(true);
                priceHighlightImg.color = Color.red;
            }
            else if (price >= Plugin.MinBluePrice.Value && price < Plugin.MinRedPrice.Value)
            {
                priceHighlightObj.SetActive(true);
                priceHighlightImg.color = Color.blue;
            }
            else if (price >= Plugin.MinGreenPrice.Value && price < Plugin.MinBluePrice.Value)
            {
                priceHighlightObj.SetActive(true);
                priceHighlightImg.color = Color.green;
            }
            else
            {
                // Скрываем если цена ниже зеленого порога
                priceHighlightObj.SetActive(false); 
            }
        }

        public class ShopSchedule
        {
            public int[] opens = new int[7];
            public int[] closes = new int[7];
            public string sceneName = "Unknown";
            public string customName = "";
            public bool isVisible = false;
            
            public ShopSchedule() { }

            public ShopSchedule(BaseShop bs)
            {
                sceneName = bs.gameObject.scene.name;
                for (int i = 0; i < 7; i++)
                {
                    opens[i] = 0;
                    closes[i] = 24;
                }
            }

            public ShopSchedule(Trade t)
            {
                sceneName = t.gameObject.scene.name;
                
                // Если магазин не использует автоматическое открытие/закрытие по расписанию,
                // значит он либо работает 24/7, либо закрыт навсегда по сюжету/скрипту.
                if (!t.autoOpenClose)
                {
                    int closeVal = t.open ? 24 : 0;
                    for (int i = 0; i < 7; i++)
                    {
                        opens[i] = 0;
                        closes[i] = closeVal;
                    }
                }
                else if (t.openingTimes != null)
                {
                    OpeningTimes ot = t.openingTimes;
                    opens[0] = ot.opensOnMonday; closes[0] = ot.closesOnMonday;
                    opens[1] = ot.opensOnTuesday; closes[1] = ot.closesOnTuesday;
                    opens[2] = ot.opensOnWednesday; closes[2] = ot.closesOnWednesday;
                    opens[3] = ot.opensOnThursday; closes[3] = ot.closesOnThursday;
                    opens[4] = ot.opensOnFriday; closes[4] = ot.closesOnFriday;
                    opens[5] = ot.opensOnSaturday; closes[5] = ot.closesOnSaturday;
                    opens[6] = ot.opensOnSunday; closes[6] = ot.closesOnSunday;
                }
            }

            public bool IsOpen(int currentDay, float currentHour)
            {
                int open = opens[currentDay];
                int close = closes[currentDay];
                if (open == close) return false;
                if (close == 24 && open == 0) return true; // 24/7
                if (open > close)
                {
                    return currentHour >= open || currentHour < close;
                }
                return currentHour >= open && currentHour < close;
            }

            public string GetDayString(int day)
            {
                if (opens[day] == 0 && closes[day] == 24) return "24h";
                if (opens[day] == closes[day]) return "-";
                return $"{opens[day]}-{closes[day]}";
            }
        }

        public static Dictionary<string, ShopSchedule> knownShops = new Dictionary<string, ShopSchedule>();

        public static string CacheFilePath => System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "ObenseuerQualityOfLife_ShopsCache.txt");
        public static bool cacheLoaded = false;
        
        private static Dictionary<System.Type, MemberInfo> openingTimesMembers = new Dictionary<System.Type, MemberInfo>();

        public static OpeningTimes GetOpeningTimes(MonoBehaviour mb)
        {
            System.Type type = mb.GetType();
            if (!openingTimesMembers.TryGetValue(type, out MemberInfo member))
            {
                member = (MemberInfo)type.GetField("openingTimes", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic) 
                      ?? type.GetProperty("openingTimes", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                openingTimesMembers[type] = member;
            }
            
            if (member is FieldInfo f) return f.GetValue(mb) as OpeningTimes;
            if (member is PropertyInfo p) return p.GetValue(mb, null) as OpeningTimes;
            return null;
        }

        public static void LoadCache()
        {
            if (System.IO.File.Exists(CacheFilePath))
            {
                try
                {
                    string[] lines = System.IO.File.ReadAllLines(CacheFilePath);
                    foreach(string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        
                        int firstColon = line.IndexOf(':');
                        if (firstColon > 0 && firstColon < line.Length - 1)
                        {
                            string shopName = line.Substring(0, firstColon);
                            string dataPart = line.Substring(firstColon + 1);
                            
                            string[] timesStr = dataPart.Split(',');
                            if (timesStr.Length >= 14)
                            {
                                ShopSchedule ot = new ShopSchedule();
                                for (int i = 0; i < 7; i++)
                                {
                                    int.TryParse(timesStr[i * 2], out ot.opens[i]);
                                    int.TryParse(timesStr[i * 2 + 1], out ot.closes[i]);
                                }
                                if (timesStr.Length > 14) {
                                    ot.sceneName = timesStr[14];
                                } else {
                                    ot.sceneName = "City";
                                }
                                if (timesStr.Length > 15) {
                                    bool.TryParse(timesStr[15], out ot.isVisible);
                                }
                                if (timesStr.Length > 16) {
                                    // Custom name can contain commas, so we join the rest
                                    string cName = timesStr[16];
                                    for (int j = 17; j < timesStr.Length; j++)
                                    {
                                        cName += "," + timesStr[j];
                                    }
                                    ot.customName = cName;
                                }
                                knownShops[shopName] = ot;
                            }
                        }
                    }
                    Plugin.Log.LogInfo($"[ObenseuerQualityOfLife] Loaded {knownShops.Count} shops from cache.");
                }
                catch(System.Exception e)
                {
                    Plugin.Log.LogError($"[ObenseuerQualityOfLife] Failed to load cache: {e.Message}\n{e.StackTrace}");
                }
            }
        }

        public static void SaveCache()
        {
            try
            {
                System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
                foreach(var kvp in knownShops)
                {
                    string name = kvp.Key;
                    ShopSchedule ot = kvp.Value;
                    System.Text.StringBuilder dataBuilder = new System.Text.StringBuilder();
                    for (int i = 0; i < 7; i++)
                    {
                        dataBuilder.Append($"{ot.opens[i]},{ot.closes[i]},");
                    }
                    dataBuilder.Append($"{ot.sceneName},{ot.isVisible},{ot.customName}");
                    lines.Add($"{name}:{dataBuilder.ToString()}");
                }
                System.IO.File.WriteAllLines(CacheFilePath, lines.ToArray());
            }
            catch(System.Exception e)
            {
                Plugin.Log.LogError($"[ObenseuerQualityOfLife] Failed to save cache: {e.Message}");
            }
        }

        public static System.Collections.Generic.HashSet<UnityEngine.GameObject> usedDoorControllers = new System.Collections.Generic.HashSet<UnityEngine.GameObject>();

        public static void ScanForShops()
        {
            if (!cacheLoaded)
            {
                LoadCache();
                cacheLoaded = true;
            }
            usedDoorControllers.Clear();
            bool addedNew = false;

            var allTrades = UnityEngine.Object.FindObjectsByType<Trade>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            var trades = System.Array.FindAll(allTrades, s => s.gameObject.scene.name != null && s.openingTimes != null);
            
            var allBaseShops = UnityEngine.Object.FindObjectsByType<BaseShop>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            var baseShops = System.Array.FindAll(allBaseShops, s => s.gameObject.scene.name != null);

            var allShops = new System.Collections.Generic.List<System.Tuple<MonoBehaviour, string, ShopSchedule>>();
            var processedObjects = new System.Collections.Generic.HashSet<UnityEngine.GameObject>();
            
            foreach (var t in trades)
            {
                if (t != null && t.GetComponent("NavMeshAgent") == null && t.GetComponent("CharacterController") == null)
                {
                    if (!processedObjects.Add(t.gameObject)) continue;
                    
                    string n = string.IsNullOrEmpty(t.title) ? t.gameObject.name : t.title;
                    allShops.Add(new System.Tuple<MonoBehaviour, string, ShopSchedule>(t, n, new ShopSchedule(t)));
                }
            }
            
            foreach (var bs in baseShops)
            {
                if (bs != null && bs.GetComponent("NavMeshAgent") == null && bs.GetComponent("CharacterController") == null)
                {
                    if (!processedObjects.Add(bs.gameObject)) continue;
                    
                    string n = string.IsNullOrEmpty(bs.title) ? bs.gameObject.name : bs.title;
                    allShops.Add(new System.Tuple<MonoBehaviour, string, ShopSchedule>(bs, n, new ShopSchedule(bs)));
                }
            }

            foreach (var tuple in allShops)
            {
                MonoBehaviour comp = tuple.Item1;
                string n = tuple.Item2;
                ShopSchedule sched = tuple.Item3;

                if (!knownShops.ContainsKey(n))
                {
                    knownShops[n] = sched;
                    
                    OpeningTimes correctOt = null;
                    UnityEngine.Transform current = comp.transform;
                    
                    while (current != null)
                    {
                        var rw = current.GetComponent<RelayWeekdays>();
                        if (rw != null && rw.openingTimes != null)
                        {
                            correctOt = rw.openingTimes;
                            usedDoorControllers.Add(rw.gameObject);
                            break;
                        }
                        
                        var lot = current.GetComponent<Logic_openingTimes>();
                        if (lot != null && lot.openingTimes != null)
                        {
                            correctOt = lot.openingTimes;
                            usedDoorControllers.Add(lot.gameObject);
                            break;
                        }
                        
                        if (current.parent != null)
                        {
                            var siblingRw = current.parent.GetComponentInChildren<RelayWeekdays>();
                            if (siblingRw != null && siblingRw.openingTimes != null) { correctOt = siblingRw.openingTimes; usedDoorControllers.Add(siblingRw.gameObject); break; }
                            
                            var siblingLot = current.parent.GetComponentInChildren<Logic_openingTimes>();
                            if (siblingLot != null && siblingLot.openingTimes != null) { correctOt = siblingLot.openingTimes; usedDoorControllers.Add(siblingLot.gameObject); break; }
                        }
                        
                        current = current.parent;
                    }

                    if (correctOt != null)
                    {
                        knownShops[n].opens[0] = correctOt.opensOnMonday; knownShops[n].closes[0] = correctOt.closesOnMonday;
                        knownShops[n].opens[1] = correctOt.opensOnTuesday; knownShops[n].closes[1] = correctOt.closesOnTuesday;
                        knownShops[n].opens[2] = correctOt.opensOnWednesday; knownShops[n].closes[2] = correctOt.closesOnWednesday;
                        knownShops[n].opens[3] = correctOt.opensOnThursday; knownShops[n].closes[3] = correctOt.closesOnThursday;
                        knownShops[n].opens[4] = correctOt.opensOnFriday; knownShops[n].closes[4] = correctOt.closesOnFriday;
                        knownShops[n].opens[5] = correctOt.opensOnSaturday; knownShops[n].closes[5] = correctOt.closesOnSaturday;
                        knownShops[n].opens[6] = correctOt.opensOnSunday; knownShops[n].closes[6] = correctOt.closesOnSunday;
                        
                        Plugin.Log.LogInfo($"[ObenseuerQualityOfLife] Linked shop {n} to hierarchy controller OpeningTimes");
                    }
                    
                    Plugin.Log.LogInfo($"[ObenseuerQualityOfLife] Discovered shop: {n} in {comp.gameObject.scene.name}");
                    addedNew = true;
                }
            }
            if (addedNew)
            {
                SaveCache();
            }
        }

        public static void ScanForScheduledDoors()
        {
            bool addedNew = false;
            
            // Находим все объекты с логикой расписания
            var allLogicTimes = UnityEngine.Object.FindObjectsByType<Logic_openingTimes>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            var logicTimes = System.Array.FindAll(allLogicTimes, s => s.gameObject.scene.name != null && s.openingTimes != null);

            var allRelayWeekdays = UnityEngine.Object.FindObjectsByType<RelayWeekdays>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            var relayWeekdays = System.Array.FindAll(allRelayWeekdays, s => s.gameObject.scene.name != null && s.openingTimes != null);

            void ProcessOpeningTimes(UnityEngine.MonoBehaviour comp, OpeningTimes ot)
            {
                if (usedDoorControllers.Contains(comp.gameObject)) return;

                string doorName = comp.gameObject.name;
                
                string foundTitle = null;

                string TryFindTitleInHierarchy(UnityEngine.GameObject go)
                {
                    UnityEngine.Transform current = go.transform;
                    int depth = 0;
                    while (current != null && depth < 3)
                    {
                        foreach (var c in current.GetComponents<UnityEngine.MonoBehaviour>())
                        {
                            if (c == null) continue;
                            var tField = c.GetType().GetField("title", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (tField != null) { string t = tField.GetValue(c) as string; if (!string.IsNullOrEmpty(t)) return t; }
                            var tProp = c.GetType().GetProperty("title", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (tProp != null && tProp.CanRead) { string t = tProp.GetValue(c, null) as string; if (!string.IsNullOrEmpty(t)) return t; }
                        }
                        current = current.parent;
                        depth++;
                    }
                    return null;
                }

                foundTitle = TryFindTitleInHierarchy(comp.gameObject);

                if (string.IsNullOrEmpty(foundTitle))
                {
                    if (comp is Logic_openingTimes lot && lot.outputsOpen != null)
                    {
                        foreach (var o in lot.outputsOpen)
                        {
                            if (o != null && o.OnTrigger != null)
                            {
                                int count = o.OnTrigger.GetPersistentEventCount();
                                for (int i = 0; i < count; i++)
                                {
                                    var target = o.OnTrigger.GetPersistentTarget(i);
                                    if (target is UnityEngine.Component targetComp) { foundTitle = TryFindTitleInHierarchy(targetComp.gameObject); if (!string.IsNullOrEmpty(foundTitle)) break; }
                                    else if (target is UnityEngine.GameObject targetGo) { foundTitle = TryFindTitleInHierarchy(targetGo); if (!string.IsNullOrEmpty(foundTitle)) break; }
                                }
                            }
                            if (!string.IsNullOrEmpty(foundTitle)) break;
                        }
                    }
                    else if (comp is RelayWeekdays rw && rw.onOpen != null && rw.onOpen.outputs != null)
                    {
                        foreach (var o in rw.onOpen.outputs)
                        {
                            if (o != null && o.OnTrigger != null)
                            {
                                int count = o.OnTrigger.GetPersistentEventCount();
                                for (int i = 0; i < count; i++)
                                {
                                    var target = o.OnTrigger.GetPersistentTarget(i);
                                    if (target is UnityEngine.Component targetComp) { foundTitle = TryFindTitleInHierarchy(targetComp.gameObject); if (!string.IsNullOrEmpty(foundTitle)) break; }
                                    else if (target is UnityEngine.GameObject targetGo) { foundTitle = TryFindTitleInHierarchy(targetGo); if (!string.IsNullOrEmpty(foundTitle)) break; }
                                }
                            }
                            if (!string.IsNullOrEmpty(foundTitle)) break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(foundTitle))
                {
                    doorName = foundTitle;
                }
                else
                {
                    doorName = doorName.Replace(" Times", "").Replace("Logic_openingTimes", "Door").Replace(" (Door)", "").Trim();
                    if (doorName == "RelayWeekdays") doorName = "Door";
                }
                
                doorName += " (Door)";
                
                if (!knownShops.ContainsKey(doorName))
                {
                    ShopSchedule sched = new ShopSchedule();
                    sched.sceneName = comp.gameObject.scene.name;
                    
                    sched.opens[0] = ot.opensOnMonday; sched.closes[0] = ot.closesOnMonday;
                    sched.opens[1] = ot.opensOnTuesday; sched.closes[1] = ot.closesOnTuesday;
                    sched.opens[2] = ot.opensOnWednesday; sched.closes[2] = ot.closesOnWednesday;
                    sched.opens[3] = ot.opensOnThursday; sched.closes[3] = ot.closesOnThursday;
                    sched.opens[4] = ot.opensOnFriday; sched.closes[4] = ot.closesOnFriday;
                    sched.opens[5] = ot.opensOnSaturday; sched.closes[5] = ot.closesOnSaturday;
                    sched.opens[6] = ot.opensOnSunday; sched.closes[6] = ot.closesOnSunday;

                    // Отфильтруем те, которые всегда открыты или всегда закрыты (0 до 0)
                    bool hasValidSchedule = false;
                    for (int i = 0; i < 7; i++)
                    {
                        if (sched.opens[i] != sched.closes[i]) hasValidSchedule = true;
                    }

                    if (hasValidSchedule)
                    {
                        knownShops[doorName] = sched;
                        Plugin.Log.LogInfo($"[ObenseuerQualityOfLife] Discovered scheduled door: {doorName} in {comp.gameObject.scene.name}");
                        addedNew = true;
                    }
                }
            }

            foreach (var lot in logicTimes)
            {
                ProcessOpeningTimes(lot, lot.openingTimes);
            }

            foreach (var rw in relayWeekdays)
            {
                ProcessOpeningTimes(rw, rw.openingTimes);
            }

            if (addedNew)
            {
                SaveCache();
            }
        }


        private static int lastUpdateDay = -1;
        private static int lastUpdateHour = -1;
        private static int lastUpdateMinute = -1;
        private static string lastShopsText = "";
        public static string DisplayShopsText = "";

        [HarmonyPatch(typeof(ShowTime), "UpdateTime")]
        [HarmonyPostfix]
        public static void ShowTime_UpdateTime_Postfix(ShowTime __instance)
        {
            if (!cacheLoaded)
            {
                LoadCache();
                cacheLoaded = true;
                ScanForShops(); // Делаем первое сканирование при загрузке
                ScanForScheduledDoors();
            }
            
            FieldInfo timeField = typeof(ShowTime).GetField("time", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo timeMeshField = typeof(ShowTime).GetField("timeMesh", BindingFlags.NonPublic | BindingFlags.Instance);

            var timeObj = timeField?.GetValue(__instance);
            var timeMeshObj = timeMeshField?.GetValue(__instance);

            if (timeObj == null && timeMeshObj == null)
            {
                return;
            }

            string shopsText = "";
            bool isWaitScreen = __instance.name.ToLower().Contains("wait") || 
                                __instance.name.ToLower().Contains("sleep") || 
                                __instance.name.ToLower().Contains("skip") ||
                                __instance.name.ToLower().Contains("craft");

            if (WaitingUI.instance != null && WaitingUI.instance.waitingUIisVisible) isWaitScreen = true;
            if (WaitingController.instance != null && (WaitingController.instance.IsWaiting || WaitingController.instance.IsSleeping)) isWaitScreen = true;
                                
            if (Plugin.showShopsList && !isWaitScreen)
            {
                int currentDay = TimeOfDayAzure.instance.currentTimeAndDay.weekDay;
                float currentHour = TimeOfDayAzure.instance.currentTimeAndDay.hours;
                int currentH = (int)TimeOfDayAzure.instance.CurrentHours;
                int currentM = (int)TimeOfDayAzure.instance.CurrentMinutes;

                if (currentDay != lastUpdateDay || currentH != lastUpdateHour || currentM != lastUpdateMinute)
                {
                    bool foundAny = false;
                    Dictionary<string, System.Text.StringBuilder> groupedText = new Dictionary<string, System.Text.StringBuilder>();

                foreach(var kvp in knownShops)
                {
                    string objName = kvp.Key;
                    ShopSchedule times = kvp.Value;

                    if (!times.isVisible) continue;

                    string displayName = !string.IsNullOrEmpty(times.customName) ? times.customName : objName;

                    foundAny = true;
                    bool isOpen = times.IsOpen(currentDay, currentHour);
                    string shopLine = "";

                    if (isOpen)
                    {
                        int closeTime = times.closes[currentDay];
                        if (times.opens[currentDay] == 0 && closeTime == 24)
                        {
                            shopLine = $"  {displayName}: <color=green>24/7</color>\n";
                        }
                        else
                        {
                            shopLine = $"  {displayName}: <color=green>TILL {closeTime:D2}:00</color>\n";
                        }
                    }
                    else
                    {
                        int nextOpenHour = -1;
                        int daysForward = 0;

                        for (int i = 0; i < 7; i++)
                        {
                            int checkDay = (currentDay + i) % 7;
                            int oTime = times.opens[checkDay];
                            int cTime = times.closes[checkDay];

                            if (oTime != cTime)
                            {
                                if (i == 0)
                                {
                                    if (currentHour < oTime)
                                    {
                                        nextOpenHour = oTime;
                                        daysForward = 0;
                                        break;
                                    }
                                }
                                else
                                {
                                    nextOpenHour = oTime;
                                    daysForward = i;
                                    break;
                                }
                            }
                        }

                        if (nextOpenHour != -1)
                        {
                            if (daysForward == 0)
                            {
                                shopLine = $"  {displayName}: <color=#FF4444>{nextOpenHour}:00</color>\n";
                            }
                            else
                            {
                                System.DateTime currentDate = new System.DateTime(TimeOfDayAzure.instance.GetYear(), TimeOfDayAzure.instance.GetMonth(), TimeOfDayAzure.instance.GetDay());
                                System.DateTime targetDate = currentDate.AddDays(daysForward);
                                string dayName = TimeOfDayAzure.instance.TimeController.GetDayOfWeekString(targetDate.Year, targetDate.Month, targetDate.Day);
                                if (!string.IsNullOrEmpty(dayName) && dayName.Length > 3)
                                {
                                    dayName = dayName.Substring(0, 3);
                                }
                                
                                shopLine = $"  {displayName}: <color=#FF4444>{dayName} {nextOpenHour}:00</color>\n";
                            }
                        }
                        else
                        {
                            shopLine = $"  {displayName}: <color=#FF4444>Not Working</color>\n";
                        }
                    }

                    if (!groupedText.ContainsKey(times.sceneName)) groupedText[times.sceneName] = new System.Text.StringBuilder();
                    groupedText[times.sceneName].Append(shopLine);
                }

                    if (!foundAny) 
                    {
                        lastShopsText = "";
                    }
                    else
                    {
                        System.Text.StringBuilder shopsTextBuilder = new System.Text.StringBuilder();
                        foreach(var kvp in groupedText)
                        {
                            shopsTextBuilder.AppendLine($"<color=yellow>--- {kvp.Key} ---</color>");
                            shopsTextBuilder.Append(kvp.Value.ToString());
                        }
                        shopsText = shopsTextBuilder.ToString();
                        lastShopsText = shopsText;
                    }

                    lastUpdateDay = currentDay;
                    lastUpdateHour = currentH;
                    lastUpdateMinute = currentM;
                }

                shopsText = lastShopsText;
                shopsText = lastShopsText;
            }

            bool isUGUI = timeField?.GetValue(__instance) != null;
            
            // Если это 3D-часы на стене (не UI), мы их игнорируем!
            // Расписание будет отображаться только на UI-часах игрока (в инвентаре/на экране)
            if (!isUGUI)
            {
                return;
            }

            // Ищем объект по имени
            Transform shopsUITransform = __instance.transform.Find("ShopsScheduleUI");
            TMP_Text shopsTmp = null;

            TMP_Text originalTmp = (TMP_Text)timeField.GetValue(__instance);

            if (shopsUITransform == null)
            {
                GameObject shopsUI = new GameObject("ShopsScheduleUI");
                shopsUI.transform.SetParent(__instance.transform, false);
                
                if (isUGUI)
                {
                    var rect = shopsUI.AddComponent<RectTransform>();
                    RectTransform origRect = __instance.GetComponent<RectTransform>();
                    if (origRect != null)
                    {
                        rect.anchorMin = new Vector2(1f, 0); // Правый нижний угол часов
                        rect.anchorMax = new Vector2(1f, 0);
                        rect.pivot = new Vector2(1f, 1); // Точка отсчета - верхний правый угол, чтобы текст рос влево и вниз
                        // Сдвигаем немного вниз и влево (отступ от правого края)
                        rect.anchoredPosition = new Vector2(-20, -5); 
                        rect.sizeDelta = new Vector2(500, 200);
                    }
                    
                    shopsTmp = shopsUI.AddComponent<TextMeshProUGUI>();
                }
                else
                {
                    // Поднимаем чуть выше чтобы не перекрывать циферблат
                    shopsUI.transform.localPosition = new Vector3(0, -0.2f, 0);
                    shopsTmp = shopsUI.AddComponent<TextMeshPro>();
                }

                shopsTmp.font = originalTmp.font;
                shopsTmp.fontSize = originalTmp.fontSize * 0.6f;
                shopsTmp.color = originalTmp.color;
                // Выравниваем текст по правому верхнему краю, чтобы он рос влево (внутрь экрана) и не вылезал за рамки
                shopsTmp.alignment = TextAlignmentOptions.TopRight;
                shopsTmp.overflowMode = TextOverflowModes.Overflow;
                shopsTmp.enableWordWrapping = false;
                
                Plugin.Log.LogInfo($"[ObenseuerQualityOfLife] Created ShopsScheduleUI successfully on {__instance.transform.root.name}'s clock.");
            }
            else
            {
                shopsTmp = shopsUITransform.GetComponent<TMP_Text>();
            }

            if (shopsTmp != null)
            {
                if (isWaitScreen || !Plugin.showShopsList) 
                {
                    shopsTmp.text = "";
                }
                else 
                {
                    shopsTmp.text = shopsText.TrimEnd('\n');
                }
            }
        }
    }

    [HarmonyPatch(typeof(ItemData))]
    public static class ItemData_Click_Patch
    {
        [HarmonyPatch("OnPointerDown", new System.Type[] { typeof(UnityEngine.EventSystems.PointerEventData) })]
        [HarmonyPrefix]
        public static bool OnPointerDown_Prefix(ItemData __instance, UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftAlt))
            {
                if (__instance.DoubleClick(false) || __instance.DoubleClick(true))
                {
                    var clickedSlot = __instance.slotController;
                    if (clickedSlot == null || clickedSlot.itemStack == null || clickedSlot.itemStack.itemId == -1) return false;

                    int targetItemId = clickedSlot.itemStack.itemId;
                    bool movedAny = false;

                    if (clickedSlot.slot.playerSlot)
                    {
                        var playerSlots = Inventory.instance.AllSlots(false, true);
                        for (int i = playerSlots.Length - 1; i >= 0; i--)
                        {
                            var slot = playerSlots[i];
                            if (slot != null && slot.itemStack != null && slot.itemStack.itemId == targetItemId && slot.itemStack.itemAmount > 0)
                            {
                                try 
                                {
                                    slot.MoveToOtherInventory(out int movedAmount, slot.itemStack.itemAmount);
                                    if (movedAmount > 0) movedAny = true;
                                }
                                catch (System.Exception ex)
                                {
                                    Plugin.Log.LogError($"[ObenseuerQualityOfLife] Error moving item {slot.itemStack.itemId}: {ex.Message}");
                                }
                            }
                        }
                    }
                    else
                    {
                        var foreignSlots = Inventory.instance.ForeignSlots;
                        if (foreignSlots != null)
                        {
                            for (int i = foreignSlots.Length - 1; i >= 0; i--)
                            {
                                var slot = foreignSlots[i];
                                if (slot != null && slot.itemStack != null && slot.itemStack.itemId == targetItemId && slot.itemStack.itemAmount > 0)
                                {
                                    try 
                                    {
                                        slot.MoveToOtherInventory(out int movedAmount, slot.itemStack.itemAmount);
                                        if (movedAmount > 0) movedAny = true;
                                    }
                                    catch (System.Exception ex)
                                    {
                                        Plugin.Log.LogError($"[ObenseuerQualityOfLife] Error moving item {slot.itemStack.itemId}: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }

                    if (movedAny)
                    {
                        __instance.item?.PlaySound();
                    }

                    return false; // Заменяем оригинальный Shift+DoubleClick
                }
            }
            return true;
        }
    }

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
                    // Перебираем все мета-объекты, прикрепленные к этому предмету
                    foreach (var metaObj in activeMeta)
                    {
                        if (metaObj != null)
                        {
                            // Выводим тип мета-объекта (например, ItemSkinData, ItemDurabilityData, ItemLiquidData)
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

    [HarmonyPatch(typeof(ToolTip))]
    public static class ToolTipPatches
    {
        [HarmonyPatch("ConstructDataString")]
        [HarmonyPrefix]
        public static void ConstructDataString_Prefix(ItemData itemData)
        {
            // Устанавливаем currentItem ДО того как сгенерируется текст,
            // чтобы наш патч UpdateTooltip мог использовать актуальный предмет!
            if (itemData != null && itemData.item != null && ToolTip.instance != null)
            {
                ToolTip.instance.currentItem = itemData.item;
            }
        }
        [HarmonyPatch("ECM.Components.MouseLook", "LookRotation")]
        [HarmonyPrefix]
        public static bool MouseLook_LookRotation_Prefix()
        {
            if (MenuComponent.IsMenuOpen)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(CategoryTitle), "Init")]
    public static class CategoryTitle_Init_Patch
    {
        private static TaskItem[] allTaskItems = null;

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
                    MethodInfo getItemTypeMethod = typeof(RecipeController).GetMethod("GetItemType", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (getItemTypeMethod != null)
                    {
                        foreach (var r in RecipeDatabase.Recipes)
                        {
                            if (r != null)
                            {
                                TaskItem.Type rType = (TaskItem.Type)getItemTypeMethod.Invoke(RecipeController.instance, new object[] { r.Type });
                                if (rType == itemType)
                                {
                                    total++;
                                    isRecipeCategory = true;
                                }
                            }
                        }

                        if (isRecipeCategory)
                        {
                            foreach (var r in RecipeController.instance.GetAllRecipes())
                            {
                                if (r != null)
                                {
                                    TaskItem.Type rType = (TaskItem.Type)getItemTypeMethod.Invoke(RecipeController.instance, new object[] { r.Type });
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
                
                if (TaskItemsManager.instance != null && TaskItemsManager.instance.taskItems != null)
                {
                    foreach(var tInfo in TaskItemsManager.instance.taskItems)
                    {
                        if (tInfo != null && tInfo.taskItem != null && tInfo.taskItem.itemType == itemType)
                        {
                            learned++;
                        }
                    }
                }
            }

            Plugin.Log.LogInfo($"[CategoryTitle_Init] Type: {itemType}, Title: {title}, Learned: {learned}, Total: {total}");

            if (total > 0 || learned > 0)
            {
                string displayTotal = total > 0 ? total.ToString() : "?";
                __instance.title.text = $"{title} ({learned}/{displayTotal})";
            }
        }
    }
}

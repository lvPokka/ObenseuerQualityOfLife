using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using TMPro;
using System.Collections.Generic;

namespace ObenseuerQualityOfLife.PatchList
{
    [HarmonyPatch]
    public static class ShopWorktimeUI
    {
        public class ShopSchedule
        {
            public int[] opens = new int[7];
            public int[] closes = new int[7];
            public string sceneName = "Unknown";
            public string customName = "";
            public bool isVisible = false;

            // Runtime-only (�� ����������� � ���): ������� � ������� ����� � ���������� �� ������
            public List<Transform> anchors = new List<Transform>();
            public float distance = -1f;

            public void AddAnchor(Transform t)
            {
                anchors.RemoveAll(a => a == null);
                if (t != null && !anchors.Contains(t)) anchors.Add(t);
            }

            public void UpdateDistance(Vector3 playerPos)
            {
                anchors.RemoveAll(a => a == null);
                float best = -1f;
                foreach (var a in anchors)
                {
                    float d = Vector3.Distance(playerPos, a.position);
                    if (best < 0f || d < best) best = d;
                }
                distance = best;
            }

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

                // ���� ������� �� ���������� �������������� ��������/�������� �� ����������,
                // ������ �� ���� �������� 24/7, ���� ������ �������� �� ������/�������.
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
                    foreach (string line in lines)
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
                                if (timesStr.Length > 14)
                                {
                                    ot.sceneName = timesStr[14];
                                }
                                else
                                {
                                    ot.sceneName = "City";
                                }
                                if (timesStr.Length > 15)
                                {
                                    bool.TryParse(timesStr[15], out ot.isVisible);
                                }
                                if (timesStr.Length > 16)
                                {
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
                catch (System.Exception e)
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
                foreach (var kvp in knownShops)
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
            catch (System.Exception e)
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

                if (knownShops.TryGetValue(n, out var knownExisting)) knownExisting.AddAnchor(comp.transform);

                if (!knownShops.ContainsKey(n))
                {
                    knownShops[n] = sched;
                    sched.AddAnchor(comp.transform);

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

            // ������� ��� ������� � ������� ����������
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

                Transform doorAnchor = (comp.GetComponentInParent<Door>() ?? comp.GetComponentInChildren<Door>())?.transform ?? comp.transform;
                if (knownShops.TryGetValue(doorName, out var knownDoor)) knownDoor.AddAnchor(doorAnchor);

                if (!knownShops.ContainsKey(doorName))
                {
                    ShopSchedule sched = new ShopSchedule();
                    sched.sceneName = comp.gameObject.scene.name;
                    sched.AddAnchor(doorAnchor);

                    sched.opens[0] = ot.opensOnMonday; sched.closes[0] = ot.closesOnMonday;
                    sched.opens[1] = ot.opensOnTuesday; sched.closes[1] = ot.closesOnTuesday;
                    sched.opens[2] = ot.opensOnWednesday; sched.closes[2] = ot.closesOnWednesday;
                    sched.opens[3] = ot.opensOnThursday; sched.closes[3] = ot.closesOnThursday;
                    sched.opens[4] = ot.opensOnFriday; sched.closes[4] = ot.closesOnFriday;
                    sched.opens[5] = ot.opensOnSaturday; sched.closes[5] = ot.closesOnSaturday;
                    sched.opens[6] = ot.opensOnSunday; sched.closes[6] = ot.closesOnSunday;

                    // ����������� ��, ������� ������ ������� ��� ������ ������� (0 �� 0)
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


        private static int distDay = -1, distHour = -1, distMinute = -1;

        // ������������� ���������� �� ���������/������ ��� � ������� ������ (��� �������������)
        public static void UpdateDistances(bool force)
        {
            try
            {
                if (TimeOfDayAzure.instance == null || PlayerLocator.instance == null) return;
                int d = TimeOfDayAzure.instance.currentTimeAndDay.weekDay;
                int h = (int)TimeOfDayAzure.instance.CurrentHours;
                int m = (int)TimeOfDayAzure.instance.CurrentMinutes;
                if (!force && d == distDay && h == distHour && m == distMinute) return;
                distDay = d; distHour = h; distMinute = m;

                Vector3 pos = PlayerLocator.instance.transform.position;
                foreach (var kvp in knownShops) kvp.Value.UpdateDistance(pos);
            }
            catch (System.Exception e) { Plugin.Log.LogWarning($"[ObenseuerQualityOfLife] UpdateDistances: {e.Message}"); }
        }

        private static int lastUpdateDay = -1;
        private static int lastUpdateHour = -1;
        private static int lastUpdateMinute = -1;
        private static string lastShopsText = "";
        public static string DisplayShopsText = "";

        private static FieldInfo _timeField = null;
        private static System.Runtime.CompilerServices.ConditionalWeakTable<ShowTime, TMP_Text> _cachedClocks = new System.Runtime.CompilerServices.ConditionalWeakTable<ShowTime, TMP_Text>();
        private static System.Runtime.CompilerServices.ConditionalWeakTable<ShowTime, object> _ignoredClocks = new System.Runtime.CompilerServices.ConditionalWeakTable<ShowTime, object>();

        [HarmonyPatch(typeof(ShowTime), "UpdateTime")]
        [HarmonyPostfix]
        public static void ShowTime_UpdateTime_Postfix(ShowTime __instance)
        {
            if (!cacheLoaded)
            {
                LoadCache();
                cacheLoaded = true;
                ScanForShops();
                ScanForScheduledDoors();
            }

            UpdateDistances(false);

            // ������� ����� ��� ��-UI �����, ����� �� ������� �������
            if (_ignoredClocks.TryGetValue(__instance, out _))
            {
                return;
            }

            TMP_Text shopsTmp = null;

            // �������� ������ ����� ��� UI ������� ���������� �� O(1)
            if (!_cachedClocks.TryGetValue(__instance, out shopsTmp) || shopsTmp == null)
            {
                if (_timeField == null)
                {
                    _timeField = typeof(ShowTime).GetField("time", BindingFlags.NonPublic | BindingFlags.Instance);
                }

                var timeObj = _timeField?.GetValue(__instance);

                // ���� ��� 3D-���� �� ����� (�� UI), �� �� ���������� ��������
                if (timeObj == null)
                {
                    _ignoredClocks.Add(__instance, null);
                    return;
                }

                Transform shopsUITransform = __instance.transform.Find("ShopsScheduleUI");
                TMP_Text originalTmp = (TMP_Text)timeObj;

                if (shopsUITransform == null)
                {
                    GameObject shopsUI = new GameObject("ShopsScheduleUI");
                    shopsUI.transform.SetParent(__instance.transform, false);

                    var rect = shopsUI.AddComponent<RectTransform>();
                    RectTransform origRect = __instance.GetComponent<RectTransform>();
                    if (origRect != null)
                    {
                        rect.anchorMin = new Vector2(1f, 0);
                        rect.anchorMax = new Vector2(1f, 0);
                        rect.pivot = new Vector2(1f, 1);
                        rect.anchoredPosition = new Vector2(-20, -5);
                        rect.sizeDelta = new Vector2(500, 200);
                    }

                    shopsTmp = shopsUI.AddComponent<TextMeshProUGUI>();

                    shopsTmp.font = originalTmp.font;
                    shopsTmp.fontSize = originalTmp.fontSize * 0.6f;
                    shopsTmp.color = originalTmp.color;
                    shopsTmp.alignment = TextAlignmentOptions.TopRight;
                    shopsTmp.overflowMode = TextOverflowModes.Overflow;
                    shopsTmp.enableWordWrapping = false;

                    Plugin.Log.LogInfo($"[ObenseuerQualityOfLife] Created ShopsScheduleUI successfully on {__instance.transform.root.name}'s clock.");
                }
                else
                {
                    shopsTmp = shopsUITransform.GetComponent<TMP_Text>();
                }

                _cachedClocks.Remove(__instance);
                _cachedClocks.Add(__instance, shopsTmp);
            }

            // ������� �������� ��������� ������, �� ��������� ���������
            bool isWaitScreen = __instance.name.ToLower().Contains("wait") ||
                                __instance.name.ToLower().Contains("sleep") ||
                                __instance.name.ToLower().Contains("skip") ||
                                __instance.name.ToLower().Contains("craft");

            if (WaitingUI.instance != null && WaitingUI.instance.waitingUIisVisible) isWaitScreen = true;
            if (WaitingController.instance != null && (WaitingController.instance.IsWaiting || WaitingController.instance.IsSleeping)) isWaitScreen = true;

            if (!Plugin.showShopsList || isWaitScreen)
            {
                shopsTmp.text = "";
                return;
            }

            int currentDay = TimeOfDayAzure.instance.currentTimeAndDay.weekDay;
            float currentHour = TimeOfDayAzure.instance.currentTimeAndDay.hours;
            int currentH = (int)TimeOfDayAzure.instance.CurrentHours;
            int currentM = (int)TimeOfDayAzure.instance.CurrentMinutes;

            // ������ ����� ������ ���� ���������� ���� �� 1 ������ � ����
            if (currentDay != lastUpdateDay || currentH != lastUpdateHour || currentM != lastUpdateMinute)
            {
                bool foundAny = false;
                Dictionary<string, System.Text.StringBuilder> groupedText = new Dictionary<string, System.Text.StringBuilder>();

                foreach (var kvp in knownShops)
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
                    foreach (var kvp in groupedText)
                    {
                        shopsTextBuilder.AppendLine($"<color=yellow>--- {kvp.Key} ---</color>");
                        shopsTextBuilder.Append(kvp.Value.ToString());
                    }
                    lastShopsText = shopsTextBuilder.ToString();
                }

                lastUpdateDay = currentDay;
                lastUpdateHour = currentH;
                lastUpdateMinute = currentM;
            }

            shopsTmp.text = lastShopsText.TrimEnd('\n');
        }
    }
}


using UnityEngine;
using BepInEx.Configuration;

namespace ObenseuerQualityOfLife
{
    public class MenuComponent : MonoBehaviour
    {
        private bool _show = false;
        public static bool IsMenuOpen = false;
        private Rect _rect = new Rect(100, 100, 850, 800);

        private string greenText;
        private string blueText;
        private string redText;

        private void Start()
        {
            greenText = Plugin.MinGreenPrice.Value.ToString();
            blueText = Plugin.MinBluePrice.Value.ToString();
            redText = Plugin.MinRedPrice.Value.ToString();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F4))
            {
                _show = !_show;
                IsMenuOpen = _show;
                if (_show)
                {
                    if (!Patches.cacheLoaded)
                    {
                        Patches.LoadCache();
                        Patches.cacheLoaded = true;
                    }
                    
                    if (GameController.instance != null)
                    {
                        GameController.instance.ControlsDisabled(this.gameObject, true, false);
                    }
                    Time.timeScale = 0f;
                    
                    // Обновляем текст из конфига при открытии меню
                    greenText = Plugin.MinGreenPrice.Value.ToString();
                    blueText = Plugin.MinBluePrice.Value.ToString();
                    redText = Plugin.MinRedPrice.Value.ToString();
                }
                else
                {
                    Time.timeScale = 1f;
                    
                    if (GameController.instance != null)
                    {
                        GameController.instance.ControlsEnabled(this.gameObject);
                    }
                    
                    if (Patches.cacheLoaded)
                    {
                        Patches.SaveCache();
                    }
                }
            }
        }

        private int _currentTab = 0;
        private string[] _tabs = new string[] { "Prices", "Shops & Doors" };
        private UnityEngine.Vector2 _scrollPosition = UnityEngine.Vector2.zero;

        private void OnGUI()
        {
            if (!_show) return;
            
            // Используем стандартный стиль окна Unity
            _rect = GUI.Window(1024, _rect, WindowRoutine, "ObenseuerQualityOfLife");
        }

        private void WindowRoutine(int id)
        {
            GUILayout.Space(10);
            _currentTab = GUILayout.Toolbar(_currentTab, _tabs);
            GUILayout.Space(10);

            if (_currentTab == 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Green Label Price:");
                greenText = GUILayout.TextField(greenText, GUILayout.Width(100));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Blue Label Price:");
                blueText = GUILayout.TextField(blueText, GUILayout.Width(100));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Red Label Price:");
                redText = GUILayout.TextField(redText, GUILayout.Width(100));
                GUILayout.EndHorizontal();

                GUILayout.Space(10);
                Plugin.showTechnicalTooltip = GUILayout.Toggle(Plugin.showTechnicalTooltip, "Show technical Item IDs");
            }
            else if (_currentTab == 1)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Show", GUILayout.Width(35));
                GUILayout.Label("Original Name", GUILayout.Width(170));
                GUILayout.Label("Custom Name", GUILayout.Width(170));
                GUILayout.Label("Mo", GUILayout.Width(45));
                GUILayout.Label("Tu", GUILayout.Width(45));
                GUILayout.Label("We", GUILayout.Width(45));
                GUILayout.Label("Th", GUILayout.Width(45));
                GUILayout.Label("Fr", GUILayout.Width(45));
                GUILayout.Label("Sa", GUILayout.Width(45));
                GUILayout.Label("Su", GUILayout.Width(45));
                GUILayout.EndHorizontal();

                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
                System.Collections.Generic.List<string> scenes = new System.Collections.Generic.List<string>();
                foreach (var kvp in Patches.knownShops)
                {
                    if (!scenes.Contains(kvp.Value.sceneName))
                    {
                        scenes.Add(kvp.Value.sceneName);
                    }
                }

                foreach (string scene in scenes)
                {
                    GUILayout.Space(10);
                    GUILayout.Label($"=== {scene} ===");
                    
                    foreach (var kvp in Patches.knownShops)
                    {
                        if (kvp.Value.sceneName == scene)
                        {
                            GUILayout.BeginHorizontal();
                            
                            kvp.Value.isVisible = GUILayout.Toggle(kvp.Value.isVisible, "", GUILayout.Width(35));
                            
                            GUILayout.Label(kvp.Key, GUILayout.Width(170));
                            
                            kvp.Value.customName = GUILayout.TextField(kvp.Value.customName, GUILayout.Width(170));
                            
                            GUILayout.Label(kvp.Value.GetDayString(0), GUILayout.Width(45));
                            GUILayout.Label(kvp.Value.GetDayString(1), GUILayout.Width(45));
                            GUILayout.Label(kvp.Value.GetDayString(2), GUILayout.Width(45));
                            GUILayout.Label(kvp.Value.GetDayString(3), GUILayout.Width(45));
                            GUILayout.Label(kvp.Value.GetDayString(4), GUILayout.Width(45));
                            GUILayout.Label(kvp.Value.GetDayString(5), GUILayout.Width(45));
                            GUILayout.Label(kvp.Value.GetDayString(6), GUILayout.Width(45));
                            
                            GUILayout.EndHorizontal();
                        }
                    }
                }
                GUILayout.EndScrollView();
            }

            GUILayout.Space(20);

            if (GUILayout.Button("Save"))
            {
                if (float.TryParse(greenText, out float g)) Plugin.MinGreenPrice.Value = g;
                if (float.TryParse(blueText, out float b)) Plugin.MinBluePrice.Value = b;
                if (float.TryParse(redText, out float r)) Plugin.MinRedPrice.Value = r;
                
                Plugin.Instance.Config.Save();
                
                // Моментально обновляем предметы
                ItemData[] allItems = FindObjectsByType<ItemData>(FindObjectsSortMode.None);
                foreach (ItemData item in allItems)
                {
                    if (item.gameObject.activeInHierarchy)
                    {
                        item.UpdateItem();
                    }
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("Close menu: F4");

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }
    }
}

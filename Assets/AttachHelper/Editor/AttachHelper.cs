using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AttachHelper.Editor
{
    public class AttachHelper : EditorWindow
    {
        public class PropertyComparer : IEqualityComparer<UniquePropertyInfo>
        {
            public bool Equals(UniquePropertyInfo x, UniquePropertyInfo y)
            {
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                return Equals(x.index, y.index) && Equals(x.propertyPath, y.propertyPath) && Equals(x.sceneName, y.sceneName) && Equals(x.GetHierarchy(), y.GetHierarchy());
            }
            
            public int GetHashCode(UniquePropertyInfo obj)
            {
                return HashCode.Combine(obj.index, obj.propertyPath, obj.sceneName, obj.GetHierarchy());
            }
        }
        
        public class UniqueProperty : UniquePropertyInfo
        {
            public SerializedProperty SerializedProperty;
            public GameObject GameObject;
        
            public UniqueProperty(Component component, SerializedProperty serializedProperty) : base(component, serializedProperty)
            {
                SerializedProperty = serializedProperty.Copy();
                GameObject = component.gameObject;
            }
        }
    
        public class UniquePropertyInfo
        {
            public int index;
            public string propertyPath;
            public string sceneName;
            public List<string> objNames;
        
            public UniquePropertyInfo(Component component, SerializedProperty serializedProperty)
            {
                var components = component.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i].Equals(component))
                    {
                        index = i;
                        break;
                    }
                }
                objNames = new List<string>();
                AddRecursiveParent(component.transform);
                propertyPath = serializedProperty.propertyPath;
                sceneName = SceneManager.GetActiveScene().name;
            }
        
            public UniquePropertyInfo(int index, string propertyPath, string sceneName, string hierarchy)
            {
                this.index = index;
                this.propertyPath = propertyPath;
                this.sceneName = sceneName;
                objNames = new List<string>();
                foreach (string objName in hierarchy.Split(" > "))
                {
                    objNames.Add(objName);
                }
            }
            
            private void AddRecursiveParent(Transform transform)
            {
                if (transform is null) return;
                
                objNames.Add(transform.name);
                AddRecursiveParent(transform.parent);
            }

            public string GetHierarchy()
            {
                StringBuilder stringBuilder = new StringBuilder();
                for (int i = 0; i < objNames.Count - 1; i++)
                {
                    stringBuilder.Append(objNames[i]);
                    stringBuilder.Append(" > ");
                }
                stringBuilder.Append(objNames[^1]);
                return stringBuilder.ToString();
            }
        }
    
        /// <summary>
        /// Noneなやつ
        /// </summary>
        static List<UniqueProperty> show = new();
    
        /// <summary>
        /// showに登録されたやつを検索するためのやつ
        /// </summary>
        private static HashSet<UniquePropertyInfo> showcomp = new HashSet<UniquePropertyInfo>(new PropertyComparer());
    
        /// <summary>
        /// Noneだけどそれでいいから無視するやつ。Noneじゃなくなってもそのまま。
        /// </summary>
        private static HashSet<UniquePropertyInfo> ignores = new HashSet<UniquePropertyInfo>(new PropertyComparer());

        private Vector2 scrollPosition = Vector2.zero;
    
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorSceneManager.sceneOpened += (_, _) =>
            {
                show.Clear();
                showcomp.Clear();
                RestoreData();
                RegisterSerializeNone();
            };
            
            RestoreData();
            RegisterSerializeNone();
            
            if (HasOpenInstances<AttachHelper>()) {
                FocusWindowIfItsOpen<AttachHelper>();
            }
            else
            {
                if (!IsShowAny()) return;
                AttachHelper window = GetWindow<AttachHelper>();
                window.Show();
            }
        }

        static bool IsShowAny()
        {
            foreach (UniqueProperty serializedObj in show)
            {
                if (ignores.Contains(serializedObj)) continue;
                return true;
            }
            return false;
        }
    
        [MenuItem("AttachHelper/Check")]
        public static void ShowWindow()
        {
            RestoreData();
            RegisterSerializeNone();
            if (HasOpenInstances<AttachHelper>()) {
                FocusWindowIfItsOpen<AttachHelper>();
            }
            else
            {
                AttachHelper window = GetWindow<AttachHelper>();
                window.Show();
            }
        }
    
        [MenuItem("AttachHelper/Reset")]
        public static void Clear()
        {
            show.Clear();
            showcomp.Clear();
            ignores.Clear();
            ClearData();
        }

        private static void ClearData()
        {
            if (string.IsNullOrEmpty(EditorUserSettings.GetConfigValue("ignoreCount")))
            {
                return;
            }
            int ignoreCount = int.Parse(EditorUserSettings.GetConfigValue("ignoreCount"));
            for (int i = 0; i < ignoreCount; i++)
            {
                EditorUserSettings.SetConfigValue($"propertyPath{i}", null);
                EditorUserSettings.SetConfigValue($"index{i}", null);
                EditorUserSettings.SetConfigValue($"sceneName{i}", null);
                EditorUserSettings.SetConfigValue($"hierarchy{i}", null);
            }
            EditorUserSettings.SetConfigValue("ignoreCount", null);
        }
    
        private static void RestoreData()
        {
            if (string.IsNullOrEmpty(EditorUserSettings.GetConfigValue("ignoreCount")))
            {
                EditorUserSettings.SetConfigValue("ignoreCount", "0");
            }
        
            int ignoreCount = int.Parse(EditorUserSettings.GetConfigValue("ignoreCount"));
            for (int i = 0; i < ignoreCount; i++)
            {
                string propertyPath = EditorUserSettings.GetConfigValue($"propertyPath{i}");
                int index = int.Parse(EditorUserSettings.GetConfigValue($"index{i}"));
                string sceneName = EditorUserSettings.GetConfigValue($"sceneName{i}");
                string hierarchy = EditorUserSettings.GetConfigValue($"hierarchy{i}");
                ignores.Add(new UniquePropertyInfo(index, propertyPath, sceneName, hierarchy));
            }
        }
        
        private static void AddIgnore(UniquePropertyInfo uniquePropertyInfo)
        {
            ignores.Add(uniquePropertyInfo);
        
            int ignoreCount = int.Parse(EditorUserSettings.GetConfigValue("ignoreCount"));
            EditorUserSettings.SetConfigValue($"propertyPath{ignoreCount}", uniquePropertyInfo.propertyPath);
            EditorUserSettings.SetConfigValue($"index{ignoreCount}", uniquePropertyInfo.index.ToString());
            EditorUserSettings.SetConfigValue($"sceneName{ignoreCount}", uniquePropertyInfo.sceneName);
            EditorUserSettings.SetConfigValue($"hierarchy{ignoreCount}", uniquePropertyInfo.GetHierarchy());
            EditorUserSettings.SetConfigValue("ignoreCount", (ignoreCount + 1).ToString());
        }
    
        static void RegisterSerializeNone()
        {
            var objs = new List<GameObject>();
            var scene = SceneManager.GetActiveScene();
            foreach (var obj in scene.GetRootGameObjects())
            {
                FindRecursive(ref objs, obj);
            }
        
            foreach (var obj in objs)
            {
                Component[] components = obj.GetComponents<Component>();
                foreach (Component component in components)
                {
                    if (component == null) continue;
                
                    var serializedObj = new SerializedObject(component);
                
                    var serializedProp = serializedObj.GetIterator();
                    while (serializedProp.NextVisible(true))
                    {
                        var uniqueProperty = new UniqueProperty(component, serializedProp);
                        if (serializedProp.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (serializedProp.objectReferenceValue != null) continue;
                        if (showcomp.Contains(uniqueProperty)) continue;
                        
                        show.Add(uniqueProperty);
                        showcomp.Add(uniqueProperty);
                    }
                }
            }
        }
        
        private static void FindRecursive(ref List<GameObject> list, GameObject root)
        {
            list.Add(root);
            foreach (Transform child in root.transform)
            {
                FindRecursive(ref list, child.gameObject);
            }
        }
    
        void OnGUI()
        {
            using (var scrollViewScope = new EditorGUILayout.ScrollViewScope(scrollPosition, false, false))
            {
                scrollPosition = scrollViewScope.scrollPosition;
                foreach (var serializedObj in show)
                {
                    if (ignores.Contains(serializedObj)) continue;
                    var serializedProp = serializedObj.SerializedProperty;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Inspect", GUILayout.Width(100)))
                        {
                            Selection.activeGameObject = serializedObj.GameObject;
                        }

                        GUILayout.Label(
                            $"{serializedObj.GameObject.name} > {serializedObj.GameObject.GetComponents<Component>()[serializedObj.index].GetType()} > {serializedProp.displayName}",
                            GUILayout.MinWidth(200));

                        GUILayout.FlexibleSpace();
                        EditorGUILayout.PropertyField(serializedProp, new GUIContent(GUIContent.none), true,
                            GUILayout.MinWidth(150), GUILayout.MaxWidth(200), GUILayout.ExpandWidth(false));
                        serializedProp.serializedObject.ApplyModifiedProperties();
                        if (GUILayout.Button("Decide", GUILayout.Width(100)))
                        {
                            AddIgnore(serializedObj);
                            AssetDatabase.SaveAssets();
                        }
                    }
                }
            }
        
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Decide All None"))
                {
                    foreach (var serializedObj in show)
                    {
                        if (ignores.Contains(serializedObj)) continue;
                        var serializedProp = serializedObj.SerializedProperty;
                        if (serializedProp.objectReferenceValue != null) continue;
                        AddIgnore(serializedObj);
                    }
                
                    AssetDatabase.SaveAssets();
                }
            
                if (GUILayout.Button("Decide All Attached"))
                {
                    foreach (var serializedObj in show)
                    {
                        if (ignores.Contains(serializedObj)) continue;
                        var serializedProp = serializedObj.SerializedProperty;
                        if (serializedProp.objectReferenceValue == null) continue;
                        AddIgnore(serializedObj);
                    }
                
                    AssetDatabase.SaveAssets();
                }
            }
        
            if (GUILayout.Button("Decide All", GUILayout.Height(40)))
            {
                foreach (var serializedObj in show)
                {
                    if (ignores.Contains(serializedObj)) continue;
                
                    AddIgnore(serializedObj);
                }
            
                AssetDatabase.SaveAssets();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close"))
            {
                Close();
            }
        }
    }
}
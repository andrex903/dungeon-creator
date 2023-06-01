#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RedeevEditor.Utilities;
using UnityEditor;
using UnityEngine;

namespace RedeevEditor.DungeonCreator
{
    public class DungeonCreatorWindow : EditorWindow
    {
        private int controlID;

        private DungeonBlock lastBlock;

        private bool blocksFoldout = true;
        private bool gridFoldout = true;
        private bool autoGenerationFoldout = true;
        private bool importExportFoldout = true;
        private bool placeholdersFoldout = true;

        private Vector2 scrollPos;

        private List<Room> rooms = new();
        private List<Room> Rooms
        {
            get
            {
                if (SceneData.checkConnections) return rooms;
                return SceneData.prefabs;
            }
        }

        private DungeonCreatorSceneData data = null;
        private DungeonCreatorSceneData SceneData
        {
            get
            {
                if (data == null) data = FindObjectOfType<DungeonCreatorSceneData>();
                return data;
            }
        }
        public int PrefabIndex
        {
            get => Mathf.Clamp(SceneData.prefabIndex, 0, Rooms.Count - 1);
            set
            {
                SceneData.prefabIndex = Mathf.Clamp(value, 0, Rooms.Count - 1);
                Repaint();
            }
        }

        private readonly int HASH = "DungeonCreator".GetHashCode();

        [MenuItem("Tools/Dungeon Creator")]
        public static void ShowWindow()
        {
            GetWindow<DungeonCreatorWindow>("Dungeon Creator");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            StopEdit();
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) StopEdit();
        }

        #region Generic  

        public void StartEdit()
        {
            SceneData.isEditing = true;

            if (SceneData.activeBlock == null) return;

            Selection.objects = new UnityEngine.Object[0];
            Tools.hidden = true;

            CenterOnGroup(SceneData.activeBlock);
        }

        public void StopEdit()
        {
            if (SceneData) SceneData.isEditing = false;

            Tools.hidden = false;
        }

        private Vector3 GetMouseWorldPoint(Event evt)
        {
            return HandleUtility.GUIPointToWorldRay(evt.mousePosition).origin;
        }

        private void CenterOnGroup(DungeonBlock group)
        {
            if (group.transform.childCount == 0)
            {
                CenterOnPoint(group.transform.position);
                return;
            }

            Vector3 center = Vector3.zero;
            for (int i = 0; i < group.transform.childCount; i++)
            {
                center += group.transform.GetChild(i).position;
            }
            center /= group.transform.childCount;
            CenterOnPoint(center);
        }

        private void CenterOnPoint(Vector3 point)
        {
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.pivot = point;
                SceneView.lastActiveSceneView.size = SceneData.activeBlock != null ? Mathf.Max(6, SceneData.activeBlock.matrix.scale) : 6;
                SceneView.lastActiveSceneView.Repaint();
            }
        }

        public float DetectScale()
        {
            if (SceneData.prefabs.Count > 0)
            {
                return GetBounds(SceneData.prefabs[0].gameObject).size.x;
            }
            return 1f;
        }

        #endregion

        #region Rooms

        private void CreateRoom(Room prefab, Vector3 position)
        {
            CreateRoom(prefab, SceneData.activeBlock, position);
        }

        private void CreateRoom(Room prefab, DungeonBlock block, Vector3 position)
        {
            if (block == null || prefab == null) return;

            Room instance = PrefabUtility.InstantiatePrefab(prefab) as Room;
            instance.transform.position = position;
            Vector3 center = GetBoundCenter(instance.gameObject);
            if (center != position) instance.transform.position += (position - center);
            instance.boundCenter = GetBoundCenter(instance.gameObject);

            instance.transform.rotation = prefab.transform.rotation;
            instance.transform.SetParent(block.transform);
            instance.name = prefab.name;

            block.AddRoom(instance, position);

            if (SceneData.isEditing) Select(instance);
        }

        private Room GetRoom()
        {
            if (Rooms.Count > 0) return Rooms[PrefabIndex];
            return null;
        }

        private List<Room> GetRoomsFilterByConnection(List<Room> original, Vector3 worldPoint)
        {
            List<Room> filtered = new();
            Direction forbidden = SceneData.activeBlock.matrix.GetForbiddenDirections(worldPoint);
            Direction required = SceneData.activeBlock.matrix.GetRequiredDirections(worldPoint);

            for (int i = 0; i < original.Count; i++)
            {
                if (original[i].connections.NotContains(forbidden) && original[i].connections.Has(required)) filtered.Add(original[i]);
            }

            return filtered;
        }

        private void Delete(Room room)
        {
            if (SceneData.activeBlock == null) return;

            SceneData.activeBlock.RemoveRoom(room);
            if (SceneData.selectedRoom == room.gameObject) Deselect();
            DestroyImmediate(room.gameObject);

        }

        private void RandomizeRooms(DungeonBlock block)
        {
            foreach (var room in block.rooms.ToList())
            {
                List<Room> prefabs = SceneData.prefabs.FindAll(x => x.connections == room.connections);
                if (prefabs.Count == 0) continue;

                Room prefab = prefabs[UnityEngine.Random.Range(0, prefabs.Count)];
                Vector3 position = GetBoundCenter(room.gameObject);
                Delete(room);
                CreateRoom(prefab, block, position);
            }
        }

        #endregion

        #region GUI

        private void OnGUI()
        {
            if (!SceneData)
            {
                EditorGUILayout.LabelField("No Scene Data", EditorStyles.centeredGreyMiniLabel);
                if (GUILayout.Button("Create Scene Data"))
                {
                    data = new GameObject($"({nameof(DungeonCreatorSceneData)})").AddComponent<DungeonCreatorSceneData>();
                    data.gameObject.hideFlags = HideFlags.HideInInspector;
                }
                return;
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorGUI.BeginChangeCheck();

            PrefabsGUI();
            BlocksGUI();
            OptionsGUI();
            ImportGUI();
            //AutoGenerationGUI();

            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(SceneData);
            EditorGUILayout.EndScrollView();
        }

        private void BlocksGUI()
        {
            EditorGUILayout.BeginVertical("HelpBox");
            blocksFoldout = GUILayout.Toggle(blocksFoldout, "Blocks", EditorStyles.foldout);
            if (blocksFoldout)
            {
                for (int i = 0; i < SceneData.blocks.Count; i++)
                {
                    EditorGUILayout.BeginVertical("Box");
                    var block = SceneData.blocks[i];

                    EditorGUILayout.BeginHorizontal();
                    block.isActive = EditorGUILayout.Toggle(block.isActive, GUILayout.Width(15f));
                    if (block.isActive)
                    {
                        block.matrix.scale = SceneData.scale;
                        if (block != lastBlock)
                        {
                            lastBlock = block;
                            StopEdit();
                            SceneData.ActivateBlock(block);
                        }
                    }
                    else
                    {
                        if (block == lastBlock)
                        {
                            SceneData.DeactivateAllBlocks();
                            StopEdit();
                            lastBlock = null;
                        }
                    }

                    string oldName = block.name;
                    block.name = EditorGUILayout.TextField(block.name);
                    if (string.IsNullOrEmpty(block.name)) block.name = oldName;
                    else if (oldName != block.name) block.transform.name = block.name;

                    GUI.enabled = block.isActive;

                    if (block.isActive && SceneData.isEditing)
                    {
                        GUI.backgroundColor = Color.yellow;
                    }

                    if (EditorUtilityGUI.IconButton("ClothInspector.PaintTool", 25f, 20f, tooltip: "Edit group"))
                    {
                        if (SceneData.isEditing) StopEdit();
                        else StartEdit();
                    }

                    if (block.isActive && SceneData.isEditing)
                    {
                        GUI.backgroundColor = Color.white;
                    }

                    if (EditorUtilityGUI.IconButton("d_preAudioLoopOff", 25f, 20f, tooltip: "Randomize"))
                    {
                        RandomizeRooms(block);
                    }

                    GUI.enabled = true;

                    if (EditorUtilityGUI.IconButton("d_Grid.MoveTool", 25f, 20f, tooltip: "Focus group"))
                    {
                        if (block.transform) CenterOnGroup(block);
                    }

                    if (EditorUtilityGUI.IconButton("d_SaveAs", 25f, 20f, tooltip: "Save group"))
                    {
                        Export(block);
                    }

                    if (EditorUtilityGUI.IconButton("d_TreeEditor.Trash", 25f, 20f))
                    {
                        if (EditorUtility.DisplayDialog("", "Are you sure to delete this block?", "Delete"))
                        {
                            StopEdit();
                            SceneData.RemoveBlock(SceneData.blocks[i]);
                        }
                    }

                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                }

                if (GUILayout.Button(EditorGUIUtility.IconContent("Add-Available")))
                {
                    SceneData.CreateNewBlock($"Block{SceneData.blocks.Count}");
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void OptionsGUI()
        {
            EditorGUILayout.BeginVertical("HelpBox");
            gridFoldout = GUILayout.Toggle(gridFoldout, "Options", EditorStyles.foldout);
            if (gridFoldout)
            {
                EditorGUILayout.BeginHorizontal();
                SceneData.scale = EditorGUILayout.FloatField("Scale", SceneData.scale);
                if (EditorUtilityGUI.IconButton("Refresh", 25f, 20f, tooltip: "Refresh")) SceneData.scale = DetectScale();
                EditorGUILayout.EndHorizontal();
                SceneData.boundsSource = (BoundsSource)EditorGUILayout.EnumPopup("Bounds Source", SceneData.boundsSource);
                if (SceneData.boundsSource == BoundsSource.CustomChild) SceneData.customChildName = EditorGUILayout.TextField("Custom Child Name", SceneData.customChildName);
                EditorGUILayout.Space();
                SceneData.randomize = EditorGUILayout.Toggle("Randomize Rooms", SceneData.randomize);
                SceneData.checkConnections = EditorGUILayout.Toggle("Filter by Connections", SceneData.checkConnections);
                SceneData.useGrid = EditorGUILayout.Toggle("Show Grid", SceneData.useGrid);
                if (SceneData.useGrid) SceneData.gridSize = Mathf.Max(0, EditorGUILayout.IntSlider("Grid Size", SceneData.gridSize, 2, 100));
            }
            EditorGUILayout.EndVertical();
        }

        private void PrefabsGUI()
        {
            Rect rect = EditorGUILayout.BeginVertical("HelpBox");
            placeholdersFoldout = GUILayout.Toggle(placeholdersFoldout, "Prefabs", EditorStyles.foldout);
            if (placeholdersFoldout)
            {
                if (SceneData.isEditing) GUI.enabled = false;
                if (SceneData.prefabs.Count == 0)
                {
                    EditorGUILayout.LabelField("Add or drag a room here", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(20f));
                }
                else EditorGUILayout.Space();

                for (int i = 0; i < SceneData.prefabs.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();

                    if (SceneData.isEditing)
                    {
                        if (!Rooms.Contains(SceneData.prefabs[i]))
                        {
                            GUI.backgroundColor = Color.red;
                        }
                        else if (SceneData.selectedRoom && PrefabUtility.GetCorrespondingObjectFromSource(SceneData.selectedRoom) == SceneData.prefabs[i])
                        {
                            EditorGUILayout.LabelField(EditorGUIUtility.IconContent("d_Favorite Icon"), GUILayout.Width(20f));
                            GUI.backgroundColor = Color.green;
                        }
                    }

                    SceneData.prefabs[i] = EditorGUILayout.ObjectField("", SceneData.prefabs[i], typeof(Room), false) as Room;
                    GUI.backgroundColor = Color.white;

                    int total = 0;
                    int count = 0;
                    for (int j = 0; j < SceneData.blocks.Count; j++)
                    {
                        total += SceneData.blocks[j].rooms.Count;
                        count += SceneData.blocks[j].GetRoomCount(SceneData.prefabs[i].name);
                    }
                    if (total > 0) EditorGUILayout.LabelField($"{Mathf.Round(((float)count / total) * 100f)}%", GUILayout.Width(30f));

                    if (EditorUtilityGUI.IconButton("d_TreeEditor.Trash", 25f, 20f))
                    {
                        SceneData.prefabs.RemoveAt(i);
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.Space();
                if (GUILayout.Button("Clear"))
                {
                    SceneData.prefabs.Clear();
                }
                GUI.enabled = true;
            }
            EditorGUILayout.EndVertical();

            EditorUtilityGUI.DropAreaGUI(rect, obj =>
            {
                if (obj is GameObject go && go.TryGetComponent(out Room room))
                {
                    if (!SceneData.prefabs.Contains(room))
                    {
                        SceneData.prefabs.Add(room);
                        SceneData.scale = DetectScale();
                    }
                }
            });
        }

        private void AutoGenerationGUI()
        {
            EditorGUILayout.BeginVertical("HelpBox");
            autoGenerationFoldout = GUILayout.Toggle(autoGenerationFoldout, "AutoGeneration", EditorStyles.foldout);
            if (autoGenerationFoldout)
            {
                if (GUILayout.Button("Auto Generate"))
                {

                }
            }
            EditorGUILayout.EndVertical();
        }

        private void ImportGUI()
        {
            Rect rect = EditorGUILayout.BeginVertical("HelpBox");
            importExportFoldout = GUILayout.Toggle(importExportFoldout, "Import", EditorStyles.foldout);
            if (importExportFoldout)
            {
                if (SceneData.blockData.Count == 0)
                {
                    EditorGUILayout.LabelField("Add or drag a BlockData here", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(20f));
                }
                else
                {
                    for (int i = 0; i < SceneData.blockData.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        SceneData.blockData[i] = EditorGUILayout.ObjectField(SceneData.blockData[i], typeof(BlockData), false) as BlockData;
                        if (EditorUtilityGUI.IconButton("d_TreeEditor.Trash", 25f, 20f))
                        {
                            SceneData.blockData.RemoveAt(i);
                            EditorGUILayout.EndHorizontal();
                            break;
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    if (GUILayout.Button("Clear"))
                    {
                        SceneData.blockData.Clear();
                    }
                    if (GUILayout.Button($"Import"))
                    {
                        Import();
                    }
                }
            }
            EditorGUILayout.EndVertical();

            EditorUtilityGUI.DropAreaGUI(rect, obj =>
            {
                if (obj is BlockData data)
                {
                    if (!SceneData.blockData.Contains(data))
                    {
                        SceneData.blockData.Add(data);
                    }
                }
            });
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!SceneData) return;

            Event evt = Event.current;
            controlID = GUIUtility.GetControlID(HASH, FocusType.Passive);

            if (!SceneData.isEditing || evt.alt) return;

            if (evt.GetTypeForControl(controlID) == EventType.KeyDown)
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    StopEdit();
                    Repaint();
                    evt.Use();
                }
                else if (SceneData.selectedRoom)
                {
                    if (evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.A || evt.keyCode == KeyCode.D))
                    {
                        ChangeSelected(evt.keyCode == KeyCode.A ? -1 : 1);
                        evt.Use();
                    }
                    else if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Delete)
                    {
                        DeleteSelected();
                        evt.Use();
                    }
                }
            }
            else if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                Vector3 worldPoint = GetMouseWorldPoint(evt);
                if (SceneData.checkConnections)
                {
                    rooms = GetRoomsFilterByConnection(SceneData.prefabs, worldPoint);
                    Repaint();
                }

                if (SceneData.activeBlock.TryGetRoom(worldPoint, out Room room))
                {
                    Select(room);
                    evt.Use();
                }
                else
                {
                    if (SceneData.randomize) PrefabIndex = UnityEngine.Random.Range(0, Rooms.Count);
                    CreateRoom(GetRoom(), SceneData.activeBlock.matrix.GetCenter(worldPoint));
                    evt.Use();
                }
            }
            else if (evt.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlID);
            }
        }

        #endregion

        #region Selection

        private void Select(Room selected)
        {
            SceneData.selectedRoom = selected;
        }

        public void Deselect()
        {
            SceneData.selectedRoom = null;
        }

        public void ChangeSelected(int amount)
        {
            if (!SceneData.selectedRoom) return;

            int currentIndex = Rooms.IndexOf(PrefabUtility.GetCorrespondingObjectFromSource(SceneData.selectedRoom));
            PrefabIndex = currentIndex + amount;

            Vector3 position = SceneData.selectedRoom.boundCenter;
            DeleteSelected();
            CreateRoom(GetRoom(), position);
        }

        private void DeleteSelected()
        {
            if (SceneData.selectedRoom != null)
            {
                Delete(SceneData.selectedRoom);
            }
        }

        #endregion      

        #region Bounds

        private Vector3 GetBoundCenter(GameObject room)
        {
            Vector3 center = GetBounds(room).center;
            center.y = 0;
            return center;
        }

        private Bounds GetBounds(GameObject room)
        {
            Bounds bounds = new(room.transform.position, Vector3.zero);

            switch (SceneData.boundsSource)
            {
                case BoundsSource.FirstChild:
                    if (room.transform.childCount > 0) bounds = GetRenderBounds(room.transform.GetChild(0).gameObject);
                    break;
                case BoundsSource.CustomChild:
                    if (!string.IsNullOrEmpty(SceneData.customChildName))
                    {
                        foreach (Transform child in room.transform)
                        {
                            if (child.gameObject.name.Equals(SceneData.customChildName) && child.TryGetComponent(out Renderer childRender))
                            {
                                bounds = childRender.bounds;
                                break;
                            }
                        }
                    }
                    break;
                case BoundsSource.AllChildren:
                    bounds = GetRenderBounds(room);
                    foreach (Transform child in room.transform)
                    {
                        if (child.TryGetComponent(out Renderer childRender)) bounds.Encapsulate(childRender.bounds);
                        else bounds.Encapsulate(GetBounds(child.gameObject));
                    }
                    break;
            }

            return bounds;
        }

        private Bounds GetRenderBounds(GameObject element)
        {
            Bounds bounds = new(element.transform.position, Vector3.zero);
            if (element.TryGetComponent(out Renderer render)) return render.bounds;
            return bounds;
        }

        #endregion     

        #region Import/Export

        private void Export(DungeonBlock block)
        {
            if (block == null) return;

            BlockData data = new();
            data.matrix = block.matrix.Clone();

            string path = EditorUtility.SaveFilePanelInProject("Export block", $"{block.name}", "asset", "");

            if (!string.IsNullOrEmpty(path))
            {
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(data, path);
                AssetDatabase.Refresh();
            }
        }

        private void Import()
        {
            for (int i = 0; i < SceneData.blockData.Count; i++)
            {
                if (SceneData.blockData[i] == null) continue;

                DungeonBlock block = SceneData.CreateNewBlock(SceneData.blockData[i].name);
                block.matrix.scale = SceneData.scale;

                foreach (var element in SceneData.blockData[i].matrix.elements)
                {
                    Room prefab = SceneData.prefabs.Find(x => x.connections == element.connections);
                    if (prefab) CreateRoom(prefab, block, block.matrix.GetCenter(element));
                    else Debug.LogError("Room is missing");
                }

                block.transform.gameObject.SetActive(false);
            }
        }

        #endregion
    }

    [Serializable]
    public class DungeonBlock
    {
        public string name = "New Block";
        public bool isActive = false;
        public Transform transform;
        public Matrix matrix;
        public List<Room> rooms = new();

        public DungeonBlock()
        {
            matrix = new(1f);
        }

        public void Activate()
        {
            isActive = true;
            if (transform) transform.gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            isActive = false;
            if (transform) transform.gameObject.SetActive(false);
        }

        public void AddRoom(Room room, Vector3 position)
        {
            Element element = new() { connections = room.connections };
            matrix.Add(element, position);
            rooms.Add(room);
        }

        public void RemoveRoom(Room room)
        {
            if (matrix.TryGet(room.boundCenter, out var element))
            {
                matrix.Remove(element);
            }
            rooms.Remove(room);
        }

        public bool TryGetRoom(Vector3 point, out Room room)
        {
            room = null;
            int index = matrix.IndexOf(point);
            if (index >= 0 && index < rooms.Count)
            {
                room = rooms[index];
                return true;
            }
            return false;
        }

        public int GetRoomCount(string name)
        {
            int count = 0;
            for (int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i] && rooms[i].name == name) count++;
            }
            return count;
        }
    }

    public enum BoundsSource
    {
        None,
        FirstChild,
        CustomChild,
        AllChildren
    }

    [Flags]
    public enum Direction
    {
        North = 1,
        South = 2,
        East = 4,
        West = 8
    }
}
#endif
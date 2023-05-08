#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using RedeevEditor.Utilities;
using UnityEditor;
using UnityEngine;

namespace RedeevEditor.DungeonCreator
{
    public class DungeonCreatorWindow : EditorWindow
    {
        private DungeonCreatorSceneData data = null;
        private int controlID;

        private DungeonBlock lastBlock;

        private bool blocksFoldout = true;
        private bool gridFoldout = true;
        private bool autoGenerationFoldout = true;
        private bool importExportFoldout = true;
        private bool placeholdersFoldout = true;

        private List<Room> rooms = new();
        private List<Room> Rooms
        {
            get
            {
                if (SceneData.checkConnections) return rooms;
                return SceneData.placeholders;
            }
        }

        private DungeonCreatorSceneData SceneData
        {
            get
            {
                if (data == null)
                {
                    data = FindObjectOfType<DungeonCreatorSceneData>();
                    if (data == null)
                    {
                        data = new GameObject($"({nameof(DungeonCreatorSceneData)})").AddComponent<DungeonCreatorSceneData>();
                        data.gameObject.hideFlags = HideFlags.HideInInspector;
                    }
                }
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
            SceneData.isEditing = false;

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
                SceneView.lastActiveSceneView.size = 6;
                SceneView.lastActiveSceneView.Repaint();
            }
        }

        #endregion

        #region Rooms

        public void CreateRoom(Room prefab, Vector3 position, bool checkBound = true)
        {
            CreateRoom(prefab, SceneData.activeBlock, position, checkBound);
        }

        public void CreateRoom(Room prefab, DungeonBlock block, Vector3 position, bool checkBound = true)
        {
            if (block == null || prefab == null) return;

            Room instance = PrefabUtility.InstantiatePrefab(prefab) as Room;
            instance.transform.localScale = SceneData.scaleRoom ? prefab.transform.localScale * block.matrix.scale : prefab.transform.localScale;
            instance.transform.position = position;
            if (checkBound)
            {
                Vector3 center = GetBoundCenter(instance.gameObject);
                if (center != position) instance.transform.position += (position - center);
                instance.boundCenter = GetBoundCenter(instance.gameObject);
            }
            instance.transform.rotation = prefab.transform.rotation;
            instance.transform.SetParent(block.transform);
            instance.name = prefab.name;

            block.AddRoom(instance, position);

            if (SceneData.isEditing) Select(instance);
        }

        public Room GetRoom()
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
                if (original[i].connectionType.NotContains(forbidden) && original[i].connectionType.Has(required)) filtered.Add(original[i]);
            }

            return filtered;
        }

        public void Delete(Room room)
        {
            if (SceneData.activeBlock == null) return;

            SceneData.activeBlock.RemoveRoom(room);
            if (SceneData.selectedRoom == room.gameObject) Deselect();
            DestroyImmediate(room.gameObject);

        }

        #endregion

        #region GUI

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();

            PlaceholdersGUI();
            BlocksGUI();
            OptionsGUI();
            ImportGUI();
            //AutoGenerationGUI();

            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(SceneData);
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

                    GUI.enabled = block.isActive && SceneData.isEditing;

                    string oldName = block.name;
                    block.name = EditorGUILayout.TextField(block.name);
                    if (string.IsNullOrEmpty(block.name)) block.name = oldName;
                    else if (oldName != block.name) block.transform.name = block.name;
                   
                    block.matrix.scale = EditorGUILayout.FloatField(block.matrix.scale, GUILayout.Width(30f));                    

                    GUI.enabled = block.isActive;

                    if (block.isActive && SceneData.isEditing)
                    {
                        GUI.backgroundColor = Color.yellow;
                    }

                    if (EditorUtilityGUI.IconButton("ClothInspector.PaintTool", 25f, 20f))
                    {
                        if (SceneData.isEditing) StopEdit();
                        else StartEdit();
                    }

                    if (block.isActive && SceneData.isEditing)
                    {
                        GUI.backgroundColor = Color.white;
                    }

                    GUI.enabled = true;

                    if (EditorUtilityGUI.IconButton("d_Grid.MoveTool", 25f, 20f))
                    {
                        if (block.transform) CenterOnGroup(block);
                    }

                    if (EditorUtilityGUI.IconButton("d_SaveAs", 25f, 20f))
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
                SceneData.checkConnections = EditorGUILayout.Toggle("Filter by Connections", SceneData.checkConnections);
                SceneData.scaleRoom = EditorGUILayout.Toggle("Scale Room", SceneData.scaleRoom);
                SceneData.useGrid = EditorGUILayout.Toggle("Show Grid", SceneData.useGrid);
                if (SceneData.useGrid) SceneData.gridSize = Mathf.Max(0, EditorGUILayout.IntSlider("Grid Size", SceneData.gridSize, 2, 100));
                SceneData.boundsSource = (BoundsSource)EditorGUILayout.EnumPopup("Bounds Source", SceneData.boundsSource);
                if (SceneData.boundsSource == BoundsSource.CustomChild) SceneData.customChildName = EditorGUILayout.TextField("Custom Child Name", SceneData.customChildName);
            }
            EditorGUILayout.EndVertical();
        }

        private void PlaceholdersGUI()
        {
            Rect rect = EditorGUILayout.BeginVertical("HelpBox");
            placeholdersFoldout = GUILayout.Toggle(placeholdersFoldout, "Rooms", EditorStyles.foldout);
            if (placeholdersFoldout)
            {
                if (SceneData.isEditing) GUI.enabled = false;
                if (SceneData.placeholders.Count == 0)
                {
                    EditorGUILayout.LabelField("Add or drag a room here", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(20f));
                }
                else EditorGUILayout.Space();

                for (int i = 0; i < SceneData.placeholders.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();

                    if (SceneData.isEditing)
                    {
                        if (!Rooms.Contains(SceneData.placeholders[i]))
                        {
                            GUI.backgroundColor = Color.red;
                        }
                        else if (SceneData.selectedRoom && PrefabUtility.GetCorrespondingObjectFromSource(SceneData.selectedRoom) == SceneData.placeholders[i])
                        {
                            EditorGUILayout.LabelField(EditorGUIUtility.IconContent("d_Favorite Icon"), GUILayout.Width(20f));
                            GUI.backgroundColor = Color.green;
                        }
                    }

                    SceneData.placeholders[i] = EditorGUILayout.ObjectField("", SceneData.placeholders[i], typeof(Room), false) as Room;
                    GUI.backgroundColor = Color.white;
                    if (EditorUtilityGUI.IconButton("d_TreeEditor.Trash", 25f, 20f))
                    {
                        SceneData.placeholders.RemoveAt(i);
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                if (GUILayout.Button(EditorGUIUtility.IconContent("Add-Available")))
                {
                    SceneData.placeholders.Add(new());
                }
                GUI.enabled = true;
            }
            EditorGUILayout.EndVertical();

            EditorUtilityGUI.DropAreaGUI(rect, obj =>
            {
                if (obj is GameObject go && go.TryGetComponent(out Room room))
                {
                    if (!SceneData.placeholders.Contains(room)) SceneData.placeholders.Add(room);
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
            EditorGUILayout.BeginVertical("HelpBox");
            importExportFoldout = GUILayout.Toggle(importExportFoldout, "Import", EditorStyles.foldout);
            if (importExportFoldout)
            {
                SceneData.blockData = EditorGUILayout.ObjectField("Dungeon Block", SceneData.blockData, typeof(BlockData), false) as BlockData;
                if (GUILayout.Button($"Import"))
                {
                    Import();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
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
                    rooms = GetRoomsFilterByConnection(SceneData.placeholders, worldPoint);
                    Repaint();
                }

                if (SceneData.activeBlock.TryGetRoom(worldPoint, out Room room))
                {
                    Select(room);
                    evt.Use();
                }
                else
                {
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
                AssetDatabase.CreateAsset(data, path);
            }
        }

        private void Import()
        {
            if (SceneData.blockData == null) return;

            DungeonBlock block = SceneData.CreateNewBlock(SceneData.blockData.name);
            block.matrix.scale = SceneData.blockData.matrix.scale;

            foreach (var element in SceneData.blockData.matrix.elements)
            {
                Room prefab = SceneData.placeholders.Find(x => x.connectionType == element.connections);
                if (prefab) CreateRoom(prefab, block, block.matrix.GetCenter(element), false);
                else Debug.LogError("Room is missing");
            }

            block.transform.gameObject.SetActive(false);
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
            Element element = new() { connections = room.connectionType };
            matrix.Add(element, position);
            rooms.Add(room);
        }

        public void RemoveRoom(Room room)
        {
            if (matrix.TryGet(room.transform.position, out var element))
            {
                matrix.Remove(element);
            }
            rooms.Remove(room);
        }

        public bool TryGetRoom(Vector3 point, out Room room)
        {
            room = null;
            int index = matrix.IndexOf(point);
            if (index >= 0)
            {
                room = rooms[index];
                return true;
            }
            return false;
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
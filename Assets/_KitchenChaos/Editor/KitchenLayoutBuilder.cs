using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KitchenChaos.Layout;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace KitchenChaos.EditorTools
{
    /// <summary>Creates an editable, saved graybox without overwriting GameplayScene.</summary>
    public static class KitchenLayoutBuilder
    {
        const string ScenePath = "Assets/_KitchenChaos/Scenes/KitchenLayout_Graybox.unity";
        const string MaterialPath = "Assets/_KitchenChaos/Materials/Graybox";
        const string PrefabPath = "Assets/_KitchenChaos/Prefabs/Graybox";
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        static Transform root;
        static Camera camera;
        static Font font;
        static string EvidenceDir => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Docs/Step4Evidence");

        [MenuItem("Kitchen Chaos/Step 4/Open Kitchen Layout")]
        public static void OpenLayout()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Kitchen Chaos/Step 4/Create Kitchen Layout (new only)")]
        public static void Build()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Layout already exists. Open it to edit; generation never overwrites your scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(MaterialPath);
            Directory.CreateDirectory(PrefabPath);
            Directory.CreateDirectory(EvidenceDir);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            root = new GameObject("KitchenLayout_Graybox_18x12").transform;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Materials.Clear();
            SetupCameraAndLight();
            BuildRoom();
            BuildStations();
            BuildConveyor();
            BuildWetZone("WetZone_A", -2, 30);
            BuildWetZone("WetZone_B", 2, 100);
            BuildSpawn("Spawn_P1", new Vector3(-7, 0, -3.6f), "blue");
            BuildSpawn("Spawn_P2", new Vector3(-7, 0, -4.9f), "mintDark");
            Label("Title", "KITCHEN CHAOS", new Vector3(0, .08f, 5.25f), .34f, Mat("navy"), root);
            Label("Subtitle", "CA BẾP GIỜ CAO ĐIỂM", new Vector3(0, .08f, 5.0f), .1f, Mat("blue"), root);
            // Text is cosmetic and never contributes a collision obstacle.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.35f, .38f, .42f);
            RenderSettings.skybox = null;
            Physics.SyncTransforms();
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Scene save failed.");
            // Reopen so validation tests the serialized scene, not only objects in memory.
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ValidateAndRender();
            Debug.Log("KC_STEP4_COMPLETE: saved scene, prefabs, validation and previews.");
        }

        static Material Mat(string key)
        {
            if (Materials.TryGetValue(key, out var cached)) return cached;
            string path = MaterialPath + "/GB_" + key + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var colors = new Dictionary<string, string> {
                    {"navy","24364B"},{"blue","416B88"},{"mint","DCEFE4"},{"mintDark","4A8871"},
                    {"gold","F5CB60"},{"coral","E88473"},{"cream","FFF8EC"},{"floor","E6E4D9"},
                    {"tile","F0EEE5"},{"steel","B5C6CD"},{"dark","303B45"},{"wet","A7D7DF"},
                    {"green","8EBE6F"},{"red","DB7768"},{"bread","D9AF68"},{"beef","A86D64"},{"carrot","EEAB56"}
                };
                ColorUtility.TryParseHtmlString("#" + colors[key], out var color);
                var shader = Shader.Find(GraphicsSettings.currentRenderPipeline == null ? "Standard" : "Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("Required scene shader unavailable.");
                material = new Material(shader) { name = "GB_" + key, color = color };
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .18f);
                if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", .18f);
                AssetDatabase.CreateAsset(material, path);
            }
            Materials[key] = material;
            return material;
        }

        static GameObject Shape(string name, PrimitiveType type, Vector3 pos, Vector3 scale, string color, Transform parent, bool collision = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            var collider = go.GetComponent<Collider>();
            if (!collision) Object.DestroyImmediate(collider);
            go.isStatic = true;
            return go;
        }
        static GameObject Cube(string n, Vector3 p, Vector3 s, string c, Transform t, bool solid = false) => Shape(n, PrimitiveType.Cube, p, s, c, t, solid);

        static void Label(string name, string text, Vector3 position, float size, Material color, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.rotation = camera.transform.rotation;
            var mesh = go.AddComponent<TextMesh>(); mesh.text = text; mesh.font = font;
            mesh.fontSize = 64; mesh.characterSize = size / 4.5f; mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center; mesh.color = color.color;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = font.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }

        static void SetupCameraAndLight()
        {
            var go = new GameObject("Main Camera"); go.tag = "MainCamera";
            camera = go.AddComponent<Camera>(); go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(0, 13, -9.5f);
            go.transform.LookAt(new Vector3(0, 0, 0));
            camera.orthographic = true; camera.orthographicSize = 6.8f; camera.aspect = 16f / 9f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.13f,.19f,.24f);
            camera.nearClipPlane = .1f; camera.farClipPlane = 80;
            var sun = new GameObject("Kitchen Key Light").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = .85f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0); RenderSettings.sun = sun;
        }

        static void BuildRoom()
        {
            var room = new GameObject("01_Room_18x12").transform; room.SetParent(root);
            Cube("Floor_18x12", new Vector3(0,-.15f,0), new Vector3(18,.3f,12), "floor", room, true);
            for (int x=-9;x<9;x++) for(int z=-6;z<6;z++)
                if ((x+z)%2==0) Cube("Tile", new Vector3(x+.5f,.002f,z+.5f),new Vector3(.985f,.004f,.985f),"tile",room);
            Cube("Wall_North",new Vector3(0,.65f,5.9f),new Vector3(18,1.3f,.2f),"mintDark",room,true);
            Cube("Wall_West",new Vector3(-8.9f,.65f,0),new Vector3(.2f,1.3f,11.6f),"mintDark",room,true);
            Cube("Wall_East",new Vector3(8.9f,.65f,0),new Vector3(.2f,1.3f,11.6f),"mintDark",room,true);
            var south = Cube("Wall_South_LowVisual",new Vector3(0,.12f,-5.9f),new Vector3(18,.24f,.2f),"blue",room);
            var barrier = new GameObject("Wall_South_Collision"); barrier.transform.SetParent(room);
            barrier.transform.localPosition = new Vector3(0,.65f,-5.9f);
            barrier.AddComponent<BoxCollider>().size = new Vector3(18,1.3f,.2f);
        }

        static KitchenLayoutMarker Station(string name, KitchenLayoutMarker.Kind kind, float x, float z, float w, float depth, string label, string color)
        {
            var go=new GameObject(name); go.transform.SetParent(root);
            var mark=go.AddComponent<KitchenLayoutMarker>(); mark.kind=kind; mark.footprint=new Vector2(w,depth);
            mark.requirementId="MAP-01 / MAP-02"; mark.description="Graybox only. No cooking, inventory or network gameplay yet.";
            Cube("Cabinet",new Vector3(0,.43f,0),new Vector3(w-.08f,.86f,depth-.08f),color,go.transform);
            Cube("CounterTop",new Vector3(0,.94f,0),new Vector3(w,.12f,depth),"cream",go.transform);
            var collider=go.AddComponent<BoxCollider>(); collider.center=new Vector3(0,.5f,0); collider.size=new Vector3(w,1,depth);
            Cube("FrontBadge",new Vector3(0,.55f,-depth/2+.01f),new Vector3(w*.75f,.22f,.04f),"navy",go.transform);
            Label("StationLabel",label,new Vector3(0,1.5f,0),.17f,Mat("navy"),go.transform);
            var anchor=new GameObject("InteractionAnchor").transform; anchor.SetParent(go.transform);
            anchor.localPosition=new Vector3(0,.75f,-depth/2-.02f); mark.interactionAnchor=anchor;
            go.transform.position=new Vector3(x,0,z);
            return mark;
        }

        static void SavePrefab(GameObject go)
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, PrefabPath+"/"+go.name+".prefab", InteractionMode.AutomatedAction);
        }

        static void BuildStations()
        {
            string[] names={"XÀ LÁCH","CÀ CHUA","BÁNH MÌ","THỊT BÒ","CÀ RỐT"};
            string[] colors={"green","red","bread","beef","carrot"};
            for(int i=0;i<5;i++)
            {
                var shelf=Station("IngredientShelf_"+i,KitchenLayoutMarker.Kind.IngredientShelf,-8f+.9f*i,3.5f,.9f,1.4f,names[i],"blue");
                Shape("IngredientPlaceholder",PrimitiveType.Sphere,new Vector3(0,1.12f,0),new Vector3(.4f,.25f,.4f),colors[i],shelf.transform);
                shelf.GetComponentInChildren<TextMesh>().characterSize = .12f / 4.5f;
                shelf.description="One ingredient selection; placeholder visual only. " + names[i];
                SavePrefab(shelf.gameObject);
            }
            var chop=Station("ChoppingStation",KitchenLayoutMarker.Kind.Chopping,-2.7f,3.5f,1.5f,1.4f,"THỚT","gold");
            Cube("CuttingBoard",new Vector3(0,1.03f,0),new Vector3(.95f,.05f,.75f),"bread",chop.transform);
            SavePrefab(chop.gameObject);
            var grill=Station("GrillStation",KitchenLayoutMarker.Kind.Grill,-.7f,3.5f,1.4f,1.4f,"NƯỚNG","coral");
            Cube("GrillPlate",new Vector3(0,1.03f,0),new Vector3(1.05f,.05f,.85f),"dark",grill.transform);
            for(int i=0;i<6;i++) Cube("GrillBar",new Vector3(-.42f+i*.17f,1.07f,0),new Vector3(.045f,.025f,.8f),"steel",grill.transform);
            SavePrefab(grill.gameObject);
            var soup=Station("SoupStation",KitchenLayoutMarker.Kind.Soup,1,3.5f,1.4f,1.4f,"NỒI SÚP","coral");
            Shape("Pot",PrimitiveType.Cylinder,new Vector3(0,1.17f,0),new Vector3(.8f,.17f,.8f),"steel",soup.transform);
            Shape("PotInterior",PrimitiveType.Cylinder,new Vector3(0,1.345f,0),new Vector3(.66f,.012f,.66f),"dark",soup.transform);
            SavePrefab(soup.gameObject);
            var assembly=Station("AssemblyStation",KitchenLayoutMarker.Kind.Assembly,3.1f,3.5f,1.8f,1.4f,"LẮP RÁP","blue");
            Cube("AssemblyWorktop",new Vector3(0,1.02f,0),new Vector3(1.35f,.03f,.9f),"mint",assembly.transform);
            SavePrefab(assembly.gameObject);
            var delivery=Station("DeliveryStation",KitchenLayoutMarker.Kind.Delivery,6,3.5f,2.4f,1.4f,"GIAO MÓN","mintDark");
            for(int i=0;i<3;i++) Cube("DeliveryMark",new Vector3(-.7f+.7f*i,1.015f,0),new Vector3(.45f,.02f,.6f),"gold",delivery.transform);
            SavePrefab(delivery.gameObject);
            var trash=Station("TrashStation",KitchenLayoutMarker.Kind.Trash,7,-4.3f,1.2f,1.2f,"THÙNG BỎ","blue");
            Cube("BinOpening",new Vector3(0,1.02f,0),new Vector3(.65f,.025f,.65f),"dark",trash.transform);
            trash.interactionAnchor.localPosition = new Vector3(0,.75f,.62f); trash.interactionAnchor.localRotation = Quaternion.Euler(0,180,0); SavePrefab(trash.gameObject);
        }

        static void BuildConveyor()
        {
            var go=new GameObject("Conveyor_LeftToRight"); go.transform.SetParent(root); go.transform.position=new Vector3(0,0,-.5f);
            var marker=go.AddComponent<KitchenLayoutMarker>(); marker.kind=KitchenLayoutMarker.Kind.Conveyor; marker.footprint=new Vector2(11.5f,1.1f);
            marker.requirementId="ENV-03"; marker.description="Layout only: +X at 1.5 unit/s later. Three output slots spaced 0.9 unit.";
            var col=go.AddComponent<BoxCollider>(); col.center=new Vector3(0,.4f,0); col.size=new Vector3(11.5f,.8f,1.1f);
            Cube("Base",new Vector3(0,.36f,0),new Vector3(11.5f,.72f,1.1f),"blue",go.transform);
            Cube("BeltSurface",new Vector3(0,.76f,0),new Vector3(11.3f,.08f,.92f),"dark",go.transform);
            for(int i=0;i<24;i++) Cube("BeltSlat",new Vector3(-5.4f+i*.46f,.808f,0),new Vector3(.025f,.016f,.85f),"steel",go.transform);
            for(int i=0;i<7;i++)
            {
                float x=-4.8f+i*1.1f;
                var a=Cube("DirectionArrow_A",new Vector3(x,.83f,.13f),new Vector3(.33f,.025f,.045f),"gold",go.transform);
                a.transform.localRotation=Quaternion.Euler(0,45,0);
                var b=Cube("DirectionArrow_B",new Vector3(x,.83f,-.1f),new Vector3(.33f,.025f,.045f),"gold",go.transform);
                b.transform.localRotation=Quaternion.Euler(0,-45,0);
            }
            for(int i=0;i<3;i++)
            {
                float x=3.4f+i*.9f;
                var slot=Shape("OutputSlot_"+(i+1),PrimitiveType.Cylinder,new Vector3(x,.83f,0),new Vector3(.6f,.02f,.6f),"gold",go.transform);
                var mark=slot.AddComponent<KitchenLayoutMarker>();mark.kind=KitchenLayoutMarker.Kind.OutputSlot;mark.requirementId="ENV-03";
                Label("SlotLabel",(i+1).ToString(),new Vector3(x,.88f,0),.18f,Mat("navy"),go.transform);
            }
            Cube("EndStop",new Vector3(5.62f,.94f,0),new Vector3(.12f,.28f,1.05f),"coral",go.transform);
            Cube("SideRail_N",new Vector3(0,.88f,.51f),new Vector3(11.5f,.16f,.08f),"steel",go.transform);
            Cube("SideRail_S",new Vector3(0,.88f,-.51f),new Vector3(11.5f,.16f,.08f),"steel",go.transform);
            Label("ConveyorLabel","BĂNG CHUYỀN  →",new Vector3(-1.5f,1.2f,0),.2f,Mat("cream"),go.transform);
            SavePrefab(go);
        }

        static void BuildWetZone(string name,float x,float start)
        {
            var go=new GameObject(name);go.transform.SetParent(root);go.transform.position=new Vector3(x,0,-4.3f);
            var marker=go.AddComponent<KitchenLayoutMarker>();marker.kind=KitchenLayoutMarker.Kind.WetZone;marker.footprint=new Vector2(2.2f,1.2f);
            marker.requirementId="ENV-01 / ENV-02";marker.warningAt=start-3;marker.activeAt=start;marker.activeDuration=8;
            marker.description="Static footprint preview, not an active slippery surface. Future trigger controls 6 unit/s² braking.";
            var trigger=go.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.center=new Vector3(0,.2f,0);trigger.size=new Vector3(2.2f,.4f,1.2f);
            Cube("WetZonePreview",new Vector3(0,.015f,0),new Vector3(2.2f,.025f,1.2f),"wet",go.transform);
            for(int i=0;i<5;i++) Cube("WarningEdge",new Vector3(-.9f+i*.45f,.035f,-.57f),new Vector3(.22f,.02f,.05f),"coral",go.transform);
            Label("ZoneLabel",name.EndsWith("A") ? "VÙNG A · 30s" : "VÙNG B · 100s",new Vector3(0,.08f,0),.19f,Mat("navy"),go.transform);
            SavePrefab(go);
        }

        static void BuildSpawn(string name,Vector3 position,string color)
        {
            var go=new GameObject(name);go.transform.SetParent(root);go.transform.position=position;
            var marker=go.AddComponent<KitchenLayoutMarker>();marker.kind=KitchenLayoutMarker.Kind.Spawn;marker.footprint=new Vector2(.7f,.7f);
            marker.requirementId="SCP-02 / MAP-02";marker.description="Spawn marker; mannequin is not a controller or final character asset.";
            var mannequin=new GameObject("Placeholder_Mannequin").transform;mannequin.SetParent(go.transform, false);
            Shape("Body",PrimitiveType.Capsule,new Vector3(0,.65f,0),new Vector3(.55f,.5f,.55f),color,mannequin);
            Shape("Head",PrimitiveType.Sphere,new Vector3(0,1.25f,0),new Vector3(.42f,.42f,.42f),"bread",mannequin);
            Shape("Hat",PrimitiveType.Cylinder,new Vector3(0,1.52f,0),new Vector3(.52f,.14f,.52f),"cream",mannequin);
            Label("PlayerLabel",name.EndsWith("P1")?"P1":"P2",new Vector3(0,1.95f,0),.25f,Mat(color),go.transform);
            SavePrefab(go);
        }

        [MenuItem("Kitchen Chaos/Step 4/Validate and Export Previews")]
        public static void ValidateAndRender()
        {
            if (SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("Open the KitchenLayout_Graybox scene first.");
            if (!Application.isBatchMode && SceneManager.GetActiveScene().isDirty)
                if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene())) return;
            camera=Camera.main;
            var markers=Object.FindObjectsByType<KitchenLayoutMarker>(FindObjectsSortMode.None);
            var report=new StringBuilder("# Step 4 — Kiểm tra bố cục bằng Unity\n\n");
            report.AppendLine("Scene: " + ScenePath + "\nUnity: " + Application.unityVersion + "\n");
            int failed=0;
            Action<string,bool> check=(name,ok)=>{report.AppendLine("- " +(ok?"PASS":"FAIL")+": "+name);if(!ok)failed++;};
            var floor=GameObject.Find("Floor_18x12").GetComponent<BoxCollider>();
            Physics.SyncTransforms();
            check("Floor bounds 18 × 12 unit",Mathf.Abs(floor.bounds.size.x-18)<.001f && Mathf.Abs(floor.bounds.size.z-12)<.001f);
            check("5 ingredient shelves",markers.Count(m=>m.kind==KitchenLayoutMarker.Kind.IngredientShelf)==5);
            foreach(var kind in new[]{KitchenLayoutMarker.Kind.Chopping,KitchenLayoutMarker.Kind.Grill,KitchenLayoutMarker.Kind.Soup,KitchenLayoutMarker.Kind.Assembly,KitchenLayoutMarker.Kind.Delivery,KitchenLayoutMarker.Kind.Trash,KitchenLayoutMarker.Kind.Conveyor})
                check("One "+kind,markers.Count(m=>m.kind==kind)==1);
            check("2 spawn markers",markers.Count(m=>m.kind==KitchenLayoutMarker.Kind.Spawn)==2);
            check("Spawn visuals aligned with markers",markers.Where(m=>m.kind==KitchenLayoutMarker.Kind.Spawn).All(m=>m.transform.Find("Placeholder_Mannequin").localPosition.sqrMagnitude<.0001f));
            check("3 belt output slots",markers.Count(m=>m.kind==KitchenLayoutMarker.Kind.OutputSlot)==3);
            check("2 wet zones",markers.Count(m=>m.kind==KitchenLayoutMarker.Kind.WetZone)==2);
            check("All metadata explicitly layout-only",markers.All(m=>m.layoutOnly));
            var belt=markers.Single(m=>m.kind==KitchenLayoutMarker.Kind.Conveyor).GetComponent<BoxCollider>().bounds;
            var chop=markers.Single(m=>m.kind==KitchenLayoutMarker.Kind.Chopping).GetComponent<BoxCollider>().bounds;
            check("Main north aisle 2.75 unit",Mathf.Abs(chop.min.z-belt.max.z-2.75f)<.001f);
            check("Belt bypasses 3.05 unit each",Mathf.Abs(8.8f-belt.max.x-3.05f)<.001f && Mathf.Abs(belt.min.x+8.8f-3.05f)<.001f);
            var zones=markers.Where(m=>m.kind==KitchenLayoutMarker.Kind.WetZone).ToArray();
            check("Dry south aisle 2.65 unit",zones.All(m=>Mathf.Abs(belt.min.z-m.GetComponent<BoxCollider>().bounds.max.z-2.65f)<.001f));
            check("Wet schedule metadata 27/30/8 and 97/100/8",zones.Any(m=>m.activeAt==30&&m.warningAt==27&&m.activeDuration==8)&&zones.Any(m=>m.activeAt==100&&m.warningAt==97&&m.activeDuration==8));
            // Geometric reachability using a circular footprint, not a gameplay test.
            var solids=Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c=>!c.isTrigger&&c!=floor).Select(c=>c.bounds).ToArray();
            Func<Vector3,bool> free=p=> !solids.Any(b=>p.x>b.min.x-.35f&&p.x<b.max.x+.35f&&p.z>b.min.z-.35f&&p.z<b.max.z+.35f);
            foreach(var spawn in markers.Where(m=>m.kind==KitchenLayoutMarker.Kind.Spawn))
                check(spawn.name+" clear and outside wet zones",free(spawn.transform.position)&&!zones.Any(z=>z.GetComponent<BoxCollider>().bounds.Contains(spawn.transform.position+Vector3.up*.1f)));
            var reachable=Flood(solids);
            foreach(var station in markers.Where(m=>m.interactionAnchor!=null))
            {
                var standing=station.interactionAnchor.position-station.interactionAnchor.forward*.55f;standing.y=0;
                check(station.name+" front approach reachable (r=0.35)",free(standing)&&reachable.Contains(Cell(standing)));
            }
            check("Routes around both belt ends connected",reachable.Contains(Cell(new Vector3(-7.25f,0,1)))&&reachable.Contains(Cell(new Vector3(7.25f,0,1))));
            var renderers=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            check("No missing materials/shaders",renderers.All(r=>r.sharedMaterials.All(m=>m!=null&&m.shader!=null&&m.shader.isSupported)));
            check("No missing scripts",SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0));
            check("Camera frames floor corners at 16:9",new[]{new Vector3(-9,0,-6),new Vector3(9,0,-6),new Vector3(-9,0,6),new Vector3(9,0,6)}.All(p=>{var v=camera.WorldToViewportPoint(p);return v.x>0&&v.x<1&&v.y>0&&v.y<1&&v.z>0;}));
            report.AppendLine("\nPhạm vi: kiểm tra scene đã lưu/mở lại và đường đi hình học với bán kính giả định 0,35 unit. Chưa phải kiểm thử controller, hai người thật, multiplayer, FPS, core loop hoặc độ cân bằng. Các vùng trơn/belt chỉ là bố trí tĩnh.");
            Directory.CreateDirectory(EvidenceDir);
            File.WriteAllText(Path.Combine(EvidenceDir,"LayoutValidation.md"),report.ToString(),Encoding.UTF8);
            Export(camera,Path.Combine(EvidenceDir,"KitchenLayout_Perspective.png"));
            var oldPos=camera.transform.position;var oldRot=camera.transform.rotation;var oldSize=camera.orthographicSize;
            try {
            camera.transform.position=new Vector3(0,20,0);camera.transform.rotation=Quaternion.Euler(90,0,0);camera.orthographicSize=7.2f;
            Export(camera,Path.Combine(EvidenceDir,"KitchenLayout_TopDown.png"));
            } finally {camera.transform.SetPositionAndRotation(oldPos,oldRot);camera.orthographicSize=oldSize;}
            if(failed>0)throw new InvalidOperationException("Layout validation failed: "+failed+" checks. See Docs/Step4Evidence.");
            Debug.Log("KC_LAYOUT_VALIDATION_PASS");
        }

        // Batch-mode entry point for validating the persisted layout without regenerating it.
        public static void ValidateSavedLayout()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ValidateAndRender();
        }
        static Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x/.1f),Mathf.RoundToInt(p.z/.1f));
        static HashSet<Vector2Int> Flood(Bounds[] solids)
        {
            var found=new HashSet<Vector2Int>();var queue=new Queue<Vector2Int>();
            var start=Cell(new Vector3(-7,0,-3.6f));found.Add(start);queue.Enqueue(start);
            var dirs=new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
            while(queue.Count>0)
            {
                var p=queue.Dequeue();foreach(var delta in dirs)
                {
                    var n=p+delta;float x=n.x*.1f,z=n.y*.1f;
                    if(found.Contains(n)||Mathf.Abs(x)>8.4f||Mathf.Abs(z)>5.4f)continue;
                    if(solids.Any(b=>x>b.min.x-.35f&&x<b.max.x+.35f&&z>b.min.z-.35f&&z<b.max.z+.35f))continue;
                    found.Add(n);queue.Enqueue(n);
                }
            }
            return found;
        }

        static void Export(Camera cam,string path)
        {
            var rt=new RenderTexture(1920,1080,24);rt.antiAliasing=4;
            var asyncCompilation=ShaderUtil.allowAsyncCompilation;
            var prior=RenderTexture.active;var target=cam.targetTexture;
            var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try {ShaderUtil.allowAsyncCompilation=false;cam.targetTexture=rt;cam.Render();cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
            finally {ShaderUtil.allowAsyncCompilation=asyncCompilation;cam.targetTexture=target;RenderTexture.active=prior;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);}
        }
    }
}

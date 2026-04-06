using UnityEngine;
using System.Collections.Generic;
using ForeverEngine.Genres.Strategy;

namespace ForeverEngine.Demo.Overworld
{
    public class OverworldRenderer : UnityEngine.MonoBehaviour
    {
        private Dictionary<(int,int), GameObject> _tileObjects = new();
        private GameObject _playerToken;
        private Dictionary<string, GameObject> _locationMarkers = new();
        private Camera _cam;

        // Hex geometry
        private const float HEX_SIZE = 1f;
#pragma warning disable CS0414
        private static readonly float HEX_WIDTH = HEX_SIZE * 1.5f;
#pragma warning restore CS0414
        private static readonly float HEX_HEIGHT = HEX_SIZE * Mathf.Sqrt(3f);

        private static readonly Color COLOR_PLAINS = new Color(0.45f, 0.55f, 0.3f);
        private static readonly Color COLOR_FOREST = new Color(0.2f, 0.4f, 0.15f);
        private static readonly Color COLOR_MOUNTAIN = new Color(0.5f, 0.45f, 0.4f);
        private static readonly Color COLOR_WATER = new Color(0.15f, 0.25f, 0.5f);
        private static readonly Color COLOR_RUINS = new Color(0.4f, 0.35f, 0.3f);
        private static readonly Color COLOR_FOG_UNEXPLORED = new Color(0.05f, 0.08f, 0.05f);
        private static readonly Color COLOR_FOG_EXPLORED = new Color(0.15f, 0.18f, 0.15f);
        private static readonly Color COLOR_PLAYER = new Color(0.2f, 0.6f, 1f);
        private static readonly Color COLOR_LOCATION = new Color(1f, 0.85f, 0.3f);

        private Mesh _hexMesh;
        private Material _hexMaterial;

        public void Initialize(Dictionary<(int,int), HexTile> tiles, Camera cam)
        {
            _cam = cam;
            CreateHexMesh();
            CreateHexMaterial();

            // Create tile objects
            foreach (var kv in tiles)
            {
                var (q, r) = kv.Key;
                var tile = kv.Value;
                var go = new GameObject($"Hex_{q}_{r}");
                go.transform.SetParent(transform);
                go.transform.position = HexToWorld(q, r);

                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = _hexMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(_hexMaterial);
                mr.sharedMaterial.color = GetTerrainColor(tile.Type);

                _tileObjects[(q, r)] = go;
            }

            // Player token
            _playerToken = CreateToken("Player", COLOR_PLAYER, 0.35f, -2f);

            // Location markers
            foreach (var loc in LocationData.GetAll())
            {
                var marker = CreateToken($"Loc_{loc.Id}", COLOR_LOCATION, 0.2f, -1f);
                marker.transform.position = HexToWorld(loc.HexQ, loc.HexR) + Vector3.back;
                // Add label
                var labelGO = new GameObject($"Label_{loc.Id}");
                labelGO.transform.SetParent(marker.transform);
                labelGO.transform.localPosition = new Vector3(0, 0.7f, 0);
                var tm = labelGO.AddComponent<TextMesh>();
                tm.text = loc.Name;
                tm.characterSize = 0.15f;
                tm.fontSize = 48;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = COLOR_LOCATION;

                _locationMarkers[loc.Id] = marker;
            }
        }

        public void UpdateVisuals(int playerQ, int playerR, OverworldFog fog, bool isNight)
        {
            // Update player position
            Vector3 targetPos = HexToWorld(playerQ, playerR) + Vector3.back * 2;
            _playerToken.transform.position = Vector3.Lerp(_playerToken.transform.position, targetPos, Time.deltaTime * 12f);

            // Camera follow
            if (_cam != null)
            {
                Vector3 camTarget = new Vector3(targetPos.x, targetPos.y, _cam.transform.position.z);
                _cam.transform.position = Vector3.Lerp(_cam.transform.position, camTarget, Time.deltaTime * 5f);
            }

            // Update tile visibility
            float nightDim = isNight ? 0.5f : 1f;
            foreach (var kv in _tileObjects)
            {
                var (q, r) = kv.Key;
                var go = kv.Value;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr == null) continue;

                if (fog.IsVisible(q, r))
                {
                    mr.enabled = true;
                    var tile = OverworldManager.Instance?.Tiles.GetValueOrDefault((q, r));
                    if (tile.HasValue)
                        mr.material.color = GetTerrainColor(tile.Value.Type) * nightDim;
                }
                else if (fog.IsExplored(q, r))
                {
                    mr.enabled = true;
                    mr.material.color = COLOR_FOG_EXPLORED;
                }
                else
                {
                    mr.enabled = true;
                    mr.material.color = COLOR_FOG_UNEXPLORED;
                }
            }

            // Update location marker visibility
            foreach (var kv in _locationMarkers)
            {
                var loc = LocationData.Get(kv.Key);
                if (loc != null)
                    kv.Value.SetActive(fog.IsExplored(loc.HexQ, loc.HexR));
            }
        }

        private Color GetTerrainColor(TileType type) => type switch
        {
            TileType.Plains => COLOR_PLAINS,
            TileType.Forest => COLOR_FOREST,
            TileType.Mountain => COLOR_MOUNTAIN,
            TileType.Water => COLOR_WATER,
            TileType.Road => COLOR_RUINS, // Ruins in our demo
            _ => COLOR_PLAINS
        };

        public static Vector3 HexToWorld(int q, int r)
        {
            float x = HEX_SIZE * 1.5f * q;
            float y = HEX_SIZE * Mathf.Sqrt(3f) * (r + q * 0.5f);
            return new Vector3(x, y, 0);
        }

        private GameObject CreateToken(string name, Color color, float radius, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);

            // Create circle mesh
            int segments = 16;
            var mesh = new Mesh();
            var verts = new Vector3[segments + 1];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                verts[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = (i + 1) % segments + 1;
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(_hexMaterial);
            mr.sharedMaterial.color = color;
            mr.sortingOrder = 5;

            return go;
        }

        private void CreateHexMesh()
        {
            _hexMesh = new Mesh();
            var verts = new Vector3[7];
            verts[0] = Vector3.zero;
            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.PI / 3f * i + Mathf.PI / 6f;
                verts[i + 1] = new Vector3(
                    Mathf.Cos(angle) * HEX_SIZE * 0.95f,
                    Mathf.Sin(angle) * HEX_SIZE * 0.95f, 0);
            }
            _hexMesh.vertices = verts;
            _hexMesh.triangles = new[]
            {
                0,1,2, 0,2,3, 0,3,4, 0,4,5, 0,5,6, 0,6,1
            };
            _hexMesh.RecalculateNormals();
        }

        private void CreateHexMaterial()
        {
            // Create a white pixel texture — required for Sprites/Default and URP shaders
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();

            // Sprites/Default works in URP when it has a texture assigned
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            _hexMaterial = new Material(shader);
            _hexMaterial.mainTexture = tex;
        }
    }
}

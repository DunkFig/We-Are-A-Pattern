using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Pool;

using Color = System.Drawing.Color;

namespace HSD
{
    public class nDebug : MonoBehaviour
    {
        private static nDebug _instance;

        public static nDebug Instance
        {
            get
            {
                if (_instance == null)
                {
#if UNITY_6000
                    _instance = FindAnyObjectByType<nDebug>();
#else
                    _instance = FindObjectOfType<nDebug>();
#endif
                    if (_instance == null)
                    {
                        GameObject debugObj = new GameObject("nDebug");
                        _instance = debugObj.AddComponent<nDebug>();
                        DontDestroyOnLoad(debugObj);
                    }
                    DontDestroyOnLoad(_instance.gameObject);
                }

                return _instance;
            }
        }
        

        public enum SphereRes
        {
            four = 4,
            eight = 8,
            sixteen = 16,
            twentyfour = 24,
            thirtytwo = 32,
            sixtyfour = 64,
        }

        #region SHAPES

        private class Line
        {
            public Mesh Mesh;
            public Vector3 Start;
            public Vector3 End;
            public Color32 Color;
            public bool Overlay;
            public Renderer Renderer;

            public void Initialize()
            {
                if (Mesh == null)
                {
                    Mesh = new Mesh();
                }
            }

            public void UpdateMesh()
            {
                Mesh.Clear();
                Mesh.vertices = new Vector3[] { Start, End };
                Mesh.SetIndices(new int[] { 0, 1 }, MeshTopology.Lines, 0);
                Mesh.normals = new Vector3[] { Vector3.up, Vector3.up };
            }
        }

        private class Cube
        {
            public Mesh Mesh;
            public Vector3 Center;
            public float Size;
            public Color32 Color;
            public bool Overlay;

            public void Initialize()
            {
                if (Mesh == null)
                {
                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Mesh = cube.GetComponent<MeshFilter>().mesh;
                    Destroy(cube);
                }
            }

        }

        private class PulseCube
        {
            public Mesh Mesh;
            public Vector3 Center;
            public float InitialSize;
            public float FinalSize;
            public Color32 InitialColor;
            public Color32 FinalColor;
            public float Duration;
            public float StartTime;
            public float Size;
            public Color32 Color;
            public bool Overlay;

            public bool IsActive(float currentTime)
            {
                return (currentTime - StartTime) < Duration;
            }

            public void Initialize()
            {
                if (Mesh == null)
                {
                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Mesh = cube.GetComponent<MeshFilter>().mesh;
                    Destroy(cube);
                }
            }

            public void Update(float time)
            {
                float t = Mathf.Clamp01((time - StartTime) / Duration);
                Size = Mathf.Lerp(InitialSize, FinalSize, t);
                Color = Color32.Lerp(InitialColor, FinalColor, t);
            }
        }


        private class Sphere
        {

            public Mesh Mesh;
            public Vector3 Center;
            public float Radius = 1f;
            public SphereRes Resolution = SphereRes.four;
            public Color32 Color;
            public bool Overlay;

            public void Initialize()
            {
                if (Mesh == null)
                {
                    Mesh = new Mesh();
                    Mesh = CreateSphereMesh();
                }
            }

            public void UpdateMesh() // Default resolution
            {
                Mesh.Clear();

                Mesh = CreateSphereMesh();
            }

            private Mesh CreateSphereMesh()
            {
                int resolution = (int)Resolution;


                // Latitudinal (vertical) resolution is half of the longitudinal (horizontal) resolution
                int latResolution = resolution / 2;
                int lonResolution = resolution;

                List<Vector3> vertices = new List<Vector3>();
                List<int> triangles = new List<int>();

                // Generate vertices
                for (int lat = 0; lat <= latResolution; lat++)
                {
                    float theta = Mathf.PI * lat / latResolution;
                    float sinTheta = Mathf.Sin(theta);
                    float cosTheta = Mathf.Cos(theta);

                    for (int lon = 0; lon <= lonResolution; lon++)
                    {
                        float phi = 2 * Mathf.PI * lon / lonResolution;
                        vertices.Add(new Vector3(Mathf.Cos(phi) * sinTheta, cosTheta, Mathf.Sin(phi) * sinTheta) *
                                     Radius);
                    }
                }

                // Generate triangles
                for (int lat = 0; lat < latResolution; lat++)
                {
                    for (int lon = 0; lon < lonResolution; lon++)
                    {
                        int current = lon + lat * (lonResolution + 1);
                        int next = current + lonResolution + 1;

                        // Check if we're at the seam
                        if (lon == lonResolution - 1)
                        {
                            // Connect the last vertices of this ring to the first vertices of the next ring
                            triangles.Add(lat * (lonResolution + 1)); // First vertex of this ring
                            triangles.Add(next);
                            triangles.Add(current);

                            triangles.Add((lat + 1) * (lonResolution + 1)); // First vertex of the next ring
                            triangles.Add(next);
                            triangles.Add(lat * (lonResolution + 1));
                        }
                        else
                        {
                            // Regular quad
                            triangles.Add(current + 1);
                            triangles.Add(next);
                            triangles.Add(current);

                            triangles.Add(next + 1);
                            triangles.Add(next);
                            triangles.Add(current + 1);
                        }
                    }
                }


                Mesh.vertices = vertices.ToArray();
                Mesh.triangles = triangles.ToArray();
                Mesh.RecalculateNormals();
                Mesh.RecalculateBounds();

                return Mesh;
            }
        }

        #endregion




        private List<Line> lines = new List<Line>();
        private List<Cube> cubes = new List<Cube>();
        private List<Sphere> spheres = new List<Sphere>();
        private List<PulseCube> pulseCubes = new List<PulseCube>();


        private ObjectPool<Line> linePool;
        private ObjectPool<Cube> cubePool;
        private ObjectPool<Sphere> spherePool;
        private ObjectPool<PulseCube> pulseCubePool;

        private Material solidMaterial;
        private Material overlayMaterial;

        private MaterialPropertyBlock drawPropertyBlock;
        
        




        // Initialization
        void Awake()
        {
            _instance = this;

            solidMaterial = new Material(Shader.Find("HSD/nDebug/HalfLitSolid"));
            overlayMaterial = new Material(Shader.Find("HSD/nDebug/HalfLitOverlay"));
            
            drawPropertyBlock = new MaterialPropertyBlock();

            linePool = new ObjectPool<Line>(
                createFunc: () => new Line(),
                actionOnGet: line => { line.Initialize(); },
                actionOnRelease: line => { },
                actionOnDestroy: line => { },
                collectionCheck: false,
                defaultCapacity: 1, // Initial capacity
                maxSize: 5000); // Maximum number of objects the pool can hold
            cubePool = new ObjectPool<Cube>(
                createFunc: () => new Cube(),
                actionOnGet: cube => { cube.Initialize(); },
                actionOnRelease: cube => { },
                actionOnDestroy: cube => { },
                collectionCheck: false,
                defaultCapacity: 1, // Initial capacity
                maxSize: 5000); // Maximum number of objects the pool can hold
            spherePool = new ObjectPool<Sphere>(
                createFunc: () => new Sphere(),
                actionOnGet: sphere => { sphere.Initialize(); },
                actionOnRelease: sphere => { },
                actionOnDestroy: sphere => { },
                collectionCheck: false,
                defaultCapacity: 1, // Initial capacity
                maxSize: 5000); // Maximum number of objects the pool can hold
            pulseCubePool = new ObjectPool<PulseCube>(
                createFunc: () => new PulseCube(),
                actionOnGet: pulseCube => { pulseCube.Initialize(); },
                actionOnRelease: pulsceCube => { },
                actionOnDestroy: pulsceCube => { },
                collectionCheck: false,
                defaultCapacity: 1, // Initial capacity
                maxSize: 5000); // Maximum number of objects the pool can hold

        }




        #region DRAWING

        public static void DrawLine(Vector3 start, Vector3 end, Color color, float alpha = 1f, bool overlay = false)
        {
            Line line = Instance.linePool.Get();
            line.Start = start;
            line.End = end;
            line.Color = ToColor32(color, alpha);
            line.Overlay = overlay;
            line.UpdateMesh();
            Instance.lines.Add(line);
        }

        public static void DrawCube(Vector3 center, float size, Color color, float alpha = 1f, bool overlay = false)
        {
            Cube cube = Instance.cubePool.Get();
            cube.Center = center;
            cube.Size = size;
            cube.Color = ToColor32(color, alpha);
            cube.Overlay = overlay;
            //cube.UpdateMesh(); 
            Instance.cubes.Add(cube);
        }

        public static void DrawSphere(Vector3 center, float radius, Color color,
            SphereRes resolution = SphereRes.sixteen, float alpha = 1f, bool overlay = false)
        {
            Sphere sphere = Instance.spherePool.Get();
            sphere.Center = center;
            sphere.Radius = radius;
            sphere.Color = ToColor32(color, alpha);
            sphere.Resolution = resolution;
            sphere.Overlay = overlay;
            sphere.UpdateMesh();
            Instance.spheres.Add(sphere);
        }

        public static void DrawPulseCube(Vector3 center, float initialSize, float finalSize, Color initialColor,
            Color finalColor, float duration, float initialAlpha = 1f, float finalAlpha = 1f, bool overlay = false)
        {
            PulseCube pulseCube = Instance.pulseCubePool.Get();

            pulseCube.Center = center;
            pulseCube.InitialSize = initialSize;
            pulseCube.FinalSize = finalSize;
            pulseCube.InitialColor = ToColor32(initialColor, initialAlpha); // Assuming full alpha
            pulseCube.FinalColor = ToColor32(finalColor, finalAlpha); // Assuming full alpha
            pulseCube.Duration = duration;
            pulseCube.StartTime = Time.time;
            pulseCube.Overlay = overlay;

            Instance.pulseCubes.Add(pulseCube);
        }



        #endregion

        #region UPDATE



        public void LateUpdate()
        {
            float currentTime = Time.time;

            foreach (var line in lines)
            {
                if (line.Overlay)
                {
                    drawPropertyBlock.SetColor("_Color", line.Color);
                    drawPropertyBlock.SetFloat("_RenderQueue", 4000);
                    
                }
                else
                {
                    drawPropertyBlock.SetColor("_Color", line.Color);
                    drawPropertyBlock.SetFloat("_RenderQueue", 3000);
                }
                
                Graphics.DrawMesh(line.Mesh, Vector3.zero, Quaternion.identity, overlayMaterial, 0, null, 0,
                    drawPropertyBlock, false, false, false);
                
                linePool.Release(line);
            }

            lines.Clear();

            foreach (var cube in cubes)
            {
                Material chosenMaterial = cube.Overlay ? overlayMaterial : solidMaterial;
                
                drawPropertyBlock.SetColor("_Color", cube.Color);
               
                Graphics.DrawMesh(cube.Mesh, Matrix4x4.TRS(cube.Center, Quaternion.identity, Vector3.one * cube.Size), chosenMaterial, 0, null,
                    0, drawPropertyBlock, false, false, false);
                    
                cubePool.Release(cube);
            }

            cubes.Clear();

            foreach (var sphere in spheres)
            {
                Material chosenMaterial = sphere.Overlay ? overlayMaterial : solidMaterial;
                
                drawPropertyBlock.SetColor("_Color", sphere.Color);
                
                Graphics.DrawMesh(sphere.Mesh,
                    Matrix4x4.TRS(sphere.Center, Quaternion.identity, Vector3.one), chosenMaterial, 0, null, 0,
                    drawPropertyBlock, false, false, false);
                
                spherePool.Release(sphere);
            }

            spheres.Clear();


            for (int i = pulseCubes.Count - 1; i >= 0; i--)
            {
                var pulseCube = pulseCubes[i];
                pulseCube.Update(currentTime);

                if (pulseCube.IsActive(currentTime))
                {
                    Material chosenMaterial = pulseCube.Overlay ? overlayMaterial : solidMaterial;
                
                    drawPropertyBlock.SetColor("_Color", pulseCube.Color);
                    
                    Graphics.DrawMesh(pulseCube.Mesh,
                        Matrix4x4.TRS(pulseCube.Center, Quaternion.identity, Vector3.one * pulseCube.Size),
                        chosenMaterial, 0, null, 0, drawPropertyBlock, false, false, false);
                }
                else
                {
                    pulseCubePool.Release(pulseCube);
                    pulseCubes.RemoveAt(i);
                }
            }

        }

        #endregion




        #region Utilities

        public static Color32 ToColor32(Color color, float alpha = 1f)
        {
            byte a = (byte)(Mathf.Clamp01(alpha) * 255);
            return new Color32(color.R, color.G, color.B, a);
        }

        #endregion


    }
}
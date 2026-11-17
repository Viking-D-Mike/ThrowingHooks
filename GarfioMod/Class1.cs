using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GarfioMod
{
    // Todo lo que distingue al hacha del arpón: modelos, forma, ajustes y tienda
    public class Arma
    {
        public string Clave;                 // para el log ("Hacha", "Arpon")
        public string Nombre;                // nombre dentro del juego
        public string Prefab, PrefabVuelo;   // nombres en garfio_bundle
        public int Indice;                   // posición fija en la lista de objetos del juego
        public bool Gira;                    // el hacha da vueltas al volar; el arpón va de punta
        public float Hundir;                 // cuánto se mete al clavarse (m)
        public float Vel;                    // velocidad en el aire (m/s)
        public float Masa;

        public GameObject Objeto, Vuelo;
        public Vector3 ArgollaLocal, FiloLocal;
        public Vector3 HojaDir = Vector3.right, MangoDir = Vector3.forward;
        public bool Registrado;
        public GameObject Puesto;

        public ConfigEntry<float> ManoDistancia, ManoAltura, ManoLado, GiroX, GiroY, GiroZ;
        public ConfigEntry<string> TiendaEscena, TiendaRaiz;
        public ConfigEntry<float> TiendaX, TiendaY, TiendaZ, TiendaGiro, TiendaInclinacion, TiendaVuelta;
        public ConfigEntry<int> Precio;
        public ConfigEntry<float> ColgX, ColgY, ColgZ, ColgRotX, ColgRotY, ColgRotZ;
    }

    // ============================================================
    // HACHA Y ARPÓN CON CUERDA
    // Llévalos en la mano y pulsa la tecla de usar (R) mirando a algo
    // que flote: vuela, se clava y lo trae tirando de la cuerda.
    // R otra vez suelta la cuerda y vuelve a tu mano.
    // ============================================================
    [BepInPlugin(ID, "VDM Throwing Hooks", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        // Identificador único del mod (no puede coincidir con el de nadie más)
        public const string ID = "vikingdmike.grapplinghook";

        public static Plugin Instancia;
        public static ManualLogSource Log;

        public static Arma Hacha, Arpon, Mikhtaf, Kaginawa;
        public static Arma[] Armas;

        public static System.Type TipoAttachable;
        public static PropertyInfo PropPosColgado, PropGiroColgado;
        private ConfigEntry<KeyCode> teclaLocalizar;
        public static ConfigEntry<float> Brillo;
        public static ConfigEntry<bool> SinReflejos;
        public static ConfigEntry<float> ProbPesca;
        private static readonly Dictionary<Material, Color> coloresOriginales = new Dictionary<Material, Color>();

        // Cuerda y vuelo
        private const float Alcance = 35f;      // hasta dónde llega (m)
        private const float Recoger = 2f;       // a qué velocidad recoges cuerda (m/s)
        private const float CuerdaMin = 0.9f;   // lo mínimo que recoges (m)
        private const float Rigidez = 15f;
        private const float TiroMax = 35f;
        private const float Freno = 2f;
        private const float TiempoMax = 40f;

        private enum Fase { Nada, Volando, Pescando, Clavada, Volviendo }
        private Fase fase = Fase.Nada;
        private Arma arma;                      // lo que se ha lanzado (hacha o arpón)
        private GarfioItem lanzadora;           // el que llevas en la mano
        private GameObject copia;               // el que vuela
        private ShipItem objetivo;
        private Rigidbody cuerpo;
        private Vector3 desde, hasta;           // puntos del vuelo (por donde va el filo o la punta)
        private Vector3 golpeLocal;             // dónde se clava, respecto al objeto
        private float vueltas;
        private float tVuelo, durVuelo, giro;
        private float largo, inicio, tLanzado;
        private LineRenderer cuerda;
        private bool pescar;                    // el arpón ha caído al mar y ha salido pez
        private const float NivelMar = 0f;

        public bool Ocupada { get { return fase != Fase.Nada; } }

        private void Awake()
        {
            Instancia = this;
            Log = Logger;

            Hacha = new Arma { Clave = "Hacha", Nombre = "grappling axe", Prefab = "hacha", PrefabVuelo = "hacha_vuelo",
                Indice = 24601, Gira = true, Hundir = 0.03f, Vel = 14f, Masa = 2f };
            Arpon = new Arma { Clave = "Arpon", Nombre = "harpoon", Prefab = "arpon_con_cuerda", PrefabVuelo = "arpon_vuelo",
                Indice = 24602, Gira = false, Hundir = 0.3f, Vel = 20f, Masa = 3f };
            Mikhtaf = new Arma { Clave = "Mikhtaf", Nombre = "mikhtaf", Prefab = "mikhtaf_con_cuerda", PrefabVuelo = "mikhtaf_vuelo",
                Indice = 24603, Gira = true, Hundir = 0.03f, Vel = 15f, Masa = 1.5f };
            Kaginawa = new Arma { Clave = "Kaginawa", Nombre = "kaginawa", Prefab = "kaginawa_con_cuerda", PrefabVuelo = "kaginawa_vuelo",
                Indice = 24604, Gira = true, Hundir = 0.03f, Vel = 15f, Masa = 1.5f };
            Armas = new[] { Hacha, Arpon, Mikhtaf, Kaginawa };

            // El hacha conserva los nombres de apartado de siempre (así no se pierden tus ajustes)
            Configurar(Hacha, "", "axe", 0.6f, -0.1f, 0f, new Vector3(90f, 0f, -70f), "island 17 M (Mount Malefic)", "island 17 M (mount malefic) scenery", -412.218f, 4.266f, 281.102f, new Vector3(25f, 90f, 0f), 300, new Vector3(0.1f, 0f, -0.1f), new Vector3(90f, 0f, 0f));
            // El mikhtaf, en el mercado de Gold Rock City
            Configurar(Mikhtaf, "Mikhtaf ", "mikhtaf", 0.5f, -0.2f, 0.2f, new Vector3(0f, 0f, 165f), "island 1 A Gold Rock", "island 1 A (gold rock) scenery",
                1544.214f, 7.6f, -375.610f, new Vector3(90f, 0f, 0f), 250, new Vector3(0f, -0.17f, -0.1f), Vector3.zero);
            // El kaginawa, en una mesa de Dragon Cliffs
            Configurar(Kaginawa, "Kaginawa ", "kaginawa", 0.5f, -0.2f, 0.2f, new Vector3(0f, 0f, 165f), "island 9 E Dragon Cliffs", "island 9 E (dragon cliffs) scenery",
                -66.2f, 4.45f, -547.221f, new Vector3(-25f, 0f, 0f), 250, new Vector3(0f, -0.17f, -0.12f), Vector3.zero);
            Configurar(Arpon, "Harpoon ", "harpoon", 0.7f, 0.1f, 0.25f, Vector3.zero, "island 15 M (Fort)", "island 15 M (Fort) scenery", -125.202f, 3.025f, 32.083f, new Vector3(99f, 0f, 36f), 450, new Vector3(0f, -0.2f, -0.1f), new Vector3(-90f, 0f, 0f));

            Brillo = Config.Bind("Colors", "Brightness", 1f, new ConfigDescription("Multiplies the colors (1 = as in Blender)", new AcceptableValueRange<float>(0.5f, 3f)));
            SinReflejos = Config.Bind("Colors", "NoShine", true, "Removes the grey shine of the sky");
            Brillo.SettingChanged += (a, b) => AplicarColores();
            SinReflejos.SettingChanged += (a, b) => AplicarColores();
            ProbPesca = Config.Bind("Harpoon Fishing", "Chance", 3f, new ConfigDescription("Chance (%) of spearing a fish when the harpoon falls into the sea", new AcceptableValueRange<float>(0f, 100f)));
            #if DEBUG
            teclaLocalizar = Config.Bind("Testing", "LocateKey", KeyCode.F10, "Writes in the log what you are looking at (to place shops)");
#endif

            CargarPaquete();
            Harmony.CreateAndPatchAll(typeof(ParcheDirectorio), ID);
            if (PrefabsDirectory.instance != null)
            {
                Insertar(PrefabsDirectory.instance);
                AccessTools.Method(typeof(PrefabsDirectory), "PopulateShipItems").Invoke(PrefabsDirectory.instance, null);
            }
            SceneManager.sceneLoaded += (e, m) =>
            {
                foreach (Arma a in Armas) if (a.Objeto != null && e.name == a.TiendaEscena.Value) StartCoroutine(PonerEnTienda(a));
            };
            StartCoroutine(RegistrarColgado());
            Logger.LogInfo("VDM Throwing Hooks cargado");
        }


        private void Configurar(Arma a, string pre, string cosa, float dist, float alto, float lado, Vector3 giro, string escena, string raiz, float x, float y, float z, Vector3 tienda, int precio, Vector3 colg, Vector3 colgGiro)
        {
            a.ManoDistancia = Config.Bind(pre + "Hand", "Distance", dist, "Distance from the camera when held");
            a.ManoAltura = Config.Bind(pre + "Hand", "Height", alto, "Height when held");
            a.ManoLado = Config.Bind(pre + "Hand", "Side", lado, "Moves it to the right (+) or left (-) when held");
            a.GiroX = Config.Bind(pre + "Hand", "RotX", giro.x, "Rotation X when held");
            a.GiroY = Config.Bind(pre + "Hand", "RotY", giro.y, "Rotation Y when held");
            a.GiroZ = Config.Bind(pre + "Hand", "RotZ", giro.z, "Rotation Z when held");
            a.TiendaEscena = Config.Bind(pre + "Shop", "Scene", escena, "Island scene of the " + cosa + " shop");
            a.TiendaRaiz = Config.Bind(pre + "Shop", "Root", raiz, "Island root object");
            a.TiendaX = Config.Bind(pre + "Shop", "PosX", x, "Shop position X");
            a.TiendaY = Config.Bind(pre + "Shop", "PosY", y, "Shop position Y");
            a.TiendaZ = Config.Bind(pre + "Shop", "PosZ", z, "Shop position Z");
            a.TiendaGiro = Config.Bind(pre + "Shop", "Rotation", tienda.x, "Shop rotation");
            a.TiendaInclinacion = Config.Bind(pre + "Shop", "Tilt", tienda.y, "Shop tilt (90 = lying)");
            a.TiendaVuelta = Config.Bind(pre + "Shop", "Roll", tienda.z, "Shop roll (turns it over its own length)");
            a.Precio = Config.Bind(pre + "Shop", "Price", precio, "Price");
            a.TiendaEscena.SettingChanged += (o, e) => CambiarTienda(a);
            a.TiendaRaiz.SettingChanged += (o, e) => CambiarTienda(a);
            a.ColgX = Config.Bind(pre + "Hook", "PosX", colg.x, "Offset X when hanging on a hook (HooksHangMore)");
            a.ColgY = Config.Bind(pre + "Hook", "PosY", colg.y, "Offset Y when hanging on a hook");
            a.ColgZ = Config.Bind(pre + "Hook", "PosZ", colg.z, "Offset Z when hanging on a hook");
            a.ColgRotX = Config.Bind(pre + "Hook", "RotX", colgGiro.x, "Rotation X when hanging on a hook");
            a.ColgRotY = Config.Bind(pre + "Hook", "RotY", colgGiro.y, "Rotation Y when hanging on a hook");
            a.ColgRotZ = Config.Bind(pre + "Hook", "RotZ", colgGiro.z, "Rotation Z when hanging on a hook");
        }

        // ---------------- Carga del paquete y registro ----------------

        private void CargarPaquete()
        {
            string ruta = Path.Combine(Path.GetDirectoryName(Info.Location), "garfio_bundle");
            if (!File.Exists(ruta)) { Logger.LogError("No encuentro garfio_bundle en " + ruta); return; }
            AssetBundle paquete = AssetBundle.LoadFromFile(ruta);
            if (paquete == null) { Logger.LogError("No he podido abrir garfio_bundle"); return; }

            foreach (Arma a in Armas)
            {
                GameObject obj = paquete.LoadAsset<GameObject>(a.Prefab);
                GameObject vuelo = paquete.LoadAsset<GameObject>(a.PrefabVuelo);
                if (obj == null || vuelo == null) { Logger.LogError("Faltan prefabs de " + a.Clave + " en garfio_bundle"); continue; }

                AjustarColores(obj);
                AjustarColores(vuelo);

                GarfioItem item = a == Hacha ? (GarfioItem)obj.AddComponent<HachaItem>() : a == Arpon ? (GarfioItem)obj.AddComponent<ArponItem>() : a == Mikhtaf ? (GarfioItem)obj.AddComponent<MikhtafItem>() : obj.AddComponent<KaginawaItem>();
                item.holdDistance = a.ManoDistancia.Value;
                item.holdHeight = a.ManoAltura.Value;
                item.furniturePlaceHeight = 0.04f;
                item.mass = a.Masa;
                item.value = a.Precio.Value;
                item.name = a.Nombre;
                item.category = TransactionCategory.toolsAndSupplies;
                item.inventoryScale = 1f;
                item.floaterHeight = 1.6f;
                item.allowPlacingItems = true;
                SaveablePrefab guardable = obj.AddComponent<SaveablePrefab>();
                guardable.prefabIndex = a.Indice;

                a.Objeto = obj;
                a.Vuelo = vuelo;
                BuscarGeometria(a);
            }
        }

        private void AjustarColores(GameObject go)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                foreach (Material m in r.sharedMaterials)
                    if (m != null && m.HasProperty("_Color") && !coloresOriginales.ContainsKey(m))
                    {
                        // Blender guarda el color en "lineal", pero Unity lo lee como si fuera el color que ves (sRGB),
                        // así que lo oscurece otra vez. Lo pasamos al color que se ve en Blender.
                        Color c = m.color;
                        coloresOriginales[m] = new Color(Mathf.LinearToGammaSpace(c.r), Mathf.LinearToGammaSpace(c.g), Mathf.LinearToGammaSpace(c.b), c.a);
                        Logger.LogDebug("COLOR: " + m.name + " pasa de " + c + " a " + coloresOriginales[m]);
                    }
            AplicarColores();
        }

        // Aplica el brillo y quita (o devuelve) el reflejo gris del cielo. Se puede cambiar en vivo con F1.
        public static void AplicarColores()
        {
            foreach (KeyValuePair<Material, Color> par in coloresOriginales)
            {
                Material m = par.Key;
                if (m == null) continue;
                Color c = par.Value;
                float k = Brillo.Value;
                m.color = new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
                bool sin = SinReflejos.Value;
                // Hierro mate, aunque en Blender se haya puesto "Metálico" (en el juego lo vuelve gris y apagado)
                if (sin && m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                if (sin && m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);
                if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", sin ? 0f : 1f);
                if (m.HasProperty("_GlossyReflections")) m.SetFloat("_GlossyReflections", sin ? 0f : 1f);
                if (sin) { m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); m.EnableKeyword("_GLOSSYREFLECTIONS_OFF"); }
                else { m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF"); m.DisableKeyword("_GLOSSYREFLECTIONS_OFF"); }
            }
        }

        // Mira la forma del modelo que vuela: dónde está la argolla (el extremo del mango o del palo),
        // hacia dónde va el mango, y dónde está el filo (hacha) o la punta (arpón)
        private void BuscarGeometria(Arma a)
        {
            GameObject go = a.Vuelo;
            List<Vector3> puntos = new List<Vector3>();
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                if (!mf.sharedMesh.isReadable) { Logger.LogWarning("El modelo " + a.PrefabVuelo + " no tiene Read/Write activado en Unity"); continue; }
                Matrix4x4 m = go.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                foreach (Vector3 v in mf.sharedMesh.vertices) puntos.Add(m.MultiplyPoint3x4(v));
            }
            if (puntos.Count == 0) { Logger.LogWarning("No puedo leer la forma de " + a.Clave + ": uso valores por defecto"); return; }

            Bounds b = new Bounds(puntos[0], Vector3.zero);
            foreach (Vector3 p in puntos) b.Encapsulate(p);
            int eje = (b.size.x >= b.size.y && b.size.x >= b.size.z) ? 0 : (b.size.y >= b.size.z ? 1 : 2);
            float min = b.min[eje], max = b.max[eje], largoTotal = max - min;

            // Los dos extremos del eje largo: el más estrecho es el de la argolla, el otro el de la cabeza
            List<Vector3> extMin = new List<Vector3>(), extMax = new List<Vector3>();
            List<Vector3> extMin2 = new List<Vector3>(), extMax2 = new List<Vector3>();
            foreach (Vector3 p in puntos)
            {
                if (p[eje] <= min + largoTotal * 0.06f) extMin.Add(p);
                if (p[eje] >= max - largoTotal * 0.25f) extMax.Add(p);
                if (p[eje] >= max - largoTotal * 0.06f) extMax2.Add(p);
                if (p[eje] <= min + largoTotal * 0.25f) extMin2.Add(p);
            }
            // Hacha: se mira la puntita de cada extremo. Arpón: un cuarto de cada extremo
            // (la punta de la hoja es fina, pero con las púas es mucho más ancha que la argolla)
            bool argollaEnMin = a.Gira ? Ancho(extMin) < Ancho(extMax2) : Ancho(extMin2) < Ancho(extMax);
            List<Vector3> ladoArgolla = argollaEnMin ? extMin : extMax2;
            List<Vector3> ladoCabeza = argollaEnMin ? extMax : extMin2;

            // La argolla: el punto medio del extremo (en el arpón, solo la puntita final del palo)
            if (a.Gira) a.ArgollaLocal = Media(ladoArgolla);
            else
            {
                List<Vector3> fin = new List<Vector3>();
                foreach (Vector3 p in puntos)
                    if (argollaEnMin ? p[eje] <= min + 0.04f : p[eje] >= max - 0.04f) fin.Add(p);
                a.ArgollaLocal = Media(fin);
            }
            Vector3 cabeza = Media(ladoCabeza);
            a.MangoDir = (cabeza - a.ArgollaLocal).normalized;

            // Lo que más se aleja de la línea del mango: en el hacha es el filo; en el arpón, una púa
            float mejor = -1f;
            Vector3 lejos = Vector3.zero;
            foreach (Vector3 p in ladoCabeza)
            {
                Vector3 r = p - a.ArgollaLocal;
                Vector3 fuera = r - Vector3.Project(r, a.MangoDir);
                if (fuera.sqrMagnitude > mejor) { mejor = fuera.sqrMagnitude; lejos = p; a.HojaDir = fuera.normalized; }
            }
            if (a.Gira) a.FiloLocal = lejos;
            else
            {
                // La punta del arpón: el punto que más lejos queda de la argolla, a lo largo del palo
                float m2 = float.MinValue;
                foreach (Vector3 p in ladoCabeza)
                {
                    float d = Vector3.Dot(p - a.ArgollaLocal, a.MangoDir);
                    if (d > m2) { m2 = d; a.FiloLocal = p; }
                }
            }
            Logger.LogDebug(a.Clave + ": argolla " + a.ArgollaLocal.ToString("F3") + ", filo/punta " + a.FiloLocal.ToString("F3") +
                ", mango hacia " + a.MangoDir.ToString("F2") + ", hoja/pua hacia " + a.HojaDir.ToString("F2"));
        }

        private static float Ancho(List<Vector3> ps)
        {
            if (ps.Count == 0) return float.MaxValue;
            Bounds b = new Bounds(ps[0], Vector3.zero);
            foreach (Vector3 p in ps) b.Encapsulate(p);
            return b.size.sqrMagnitude;
        }

        private static Vector3 Media(List<Vector3> ps)
        {
            Vector3 s = Vector3.zero;
            foreach (Vector3 p in ps) s += p;
            return ps.Count > 0 ? s / ps.Count : Vector3.zero;
        }

        // Giro al clavarse. Hacha: la hoja hacia delante y el mango hacia arriba.
        // Arpón: la punta hacia delante y las púas a los lados.
        private static Quaternion GiroClavada(Arma a, Vector3 dir)
        {
            Vector3 arriba = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(dir.normalized, arriba)) > 0.95f) arriba = Vector3.forward;
            if (a.Gira)
                return Quaternion.LookRotation(dir, arriba) * Quaternion.Inverse(Quaternion.LookRotation(a.HojaDir, a.MangoDir));
            Vector3 plano = Vector3.Cross(a.MangoDir, a.HojaDir);
            if (plano.sqrMagnitude < 0.0001f) plano = Vector3.up;
            return Quaternion.LookRotation(dir, arriba) * Quaternion.Inverse(Quaternion.LookRotation(a.MangoDir, plano.normalized));
        }

        // Si el mod HooksHangMore está instalado, le decimos cómo colgar cada objeto (sin depender de él)
        private IEnumerator RegistrarColgado()
        {
            while (PrefabsDirectory.instance == null || !Hacha.Registrado) yield return new WaitForSeconds(1f);
            try
            {
                System.Type tipo = null;
                foreach (Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (tipo == null) tipo = asm.GetType("HooksHangMore.HHM_Plugin");
                    if (TipoAttachable == null) TipoAttachable = asm.GetType("HooksHangMore.AttachableItem");
                }
                if (tipo == null) { Logger.LogInfo("COLGAR: HooksHangMore no esta instalado"); yield break; }
                if (TipoAttachable != null)
                {
                    PropPosColgado = TipoAttachable.GetProperty("PositionOffset", BindingFlags.Public | BindingFlags.Instance);
                    PropGiroColgado = TipoAttachable.GetProperty("RotationOffset", BindingFlags.Public | BindingFlags.Instance);
                }
                MethodInfo metodo = tipo.GetMethod("AddAttachedOffset", BindingFlags.Public | BindingFlags.Static);
                if (metodo == null) { Logger.LogWarning("COLGAR: esta version de HooksHangMore no tiene AddAttachedOffset"); yield break; }
                foreach (Arma a in Armas)
                {
                    if (!a.Registrado) continue;
                    object r = metodo.Invoke(null, new object[] { a.Nombre,
                        new Vector3(a.ColgX.Value, a.ColgY.Value, a.ColgZ.Value), new Vector3(a.ColgRotX.Value, a.ColgRotY.Value, a.ColgRotZ.Value) });
                    Logger.LogDebug("COLGAR: AddAttachedOffset('" + a.Nombre + "') -> " + r);
                }
            }
            catch (System.Exception e) { Logger.LogWarning("COLGAR: no se pudo registrar: " + e.Message); }
        }

        [HarmonyPatch(typeof(PrefabsDirectory), "Start")]
        private static class ParcheDirectorio
        {
            private static void Prefix(PrefabsDirectory __instance) { Insertar(__instance); }
        }

        private static void Insertar(PrefabsDirectory dir)
        {
            foreach (Arma a in Armas)
            {
                if (a.Objeto == null || a.Registrado) continue;
                if (a.Indice < dir.directory.Length && dir.directory[a.Indice] != null && dir.directory[a.Indice] != a.Objeto)
                {
                    Log.LogError("La posicion " + a.Indice + " ya esta ocupada por otro objeto: no registro " + a.Clave);
                    continue;
                }
                if (a.Indice >= dir.directory.Length)
                {
                    GameObject[] nueva = new GameObject[a.Indice + 1];
                    dir.directory.CopyTo(nueva, 0);
                    dir.directory = nueva;
                }
                dir.directory[a.Indice] = a.Objeto;
                a.Registrado = true;
                Log.LogDebug(a.Clave + " registrado en la posicion " + a.Indice);
            }
        }

        // Si cambias la isla de la tienda en F1, se quita de donde estaba y se pone en la nueva (si está cargada)
        private void CambiarTienda(Arma a)
        {
            if (a.Puesto != null) { a.Puesto.name = "viejo"; a.Puesto.transform.SetParent(null); Destroy(a.Puesto); }
            a.Puesto = null;
            if (a.Objeto != null && SceneManager.GetSceneByName(a.TiendaEscena.Value).isLoaded) StartCoroutine(PonerEnTienda(a));
        }

        private IEnumerator PonerEnTienda(Arma a)
        {
            while (!a.Registrado) yield return new WaitForSeconds(1f);
            GameObject raiz = null;
            for (int i = 0; i < 30 && raiz == null; i++)
            {
                Scene escena = SceneManager.GetSceneByName(a.TiendaEscena.Value);
                if (escena.isLoaded)
                    foreach (GameObject c in escena.GetRootGameObjects()) if (c.name == a.TiendaRaiz.Value) { raiz = c; break; }
                if (raiz == null) yield return new WaitForSeconds(1f);
            }
            if (raiz == null) { Logger.LogError("TIENDA: no encuentro " + a.TiendaRaiz.Value); yield break; }
            string nombre = a.Prefab + " spawner";
            if (raiz.transform.Find(nombre) != null) yield break;

            a.Puesto = new GameObject(nombre);
            a.Puesto.transform.parent = raiz.transform;
            a.Puesto.transform.localPosition = new Vector3(a.TiendaX.Value, a.TiendaY.Value, a.TiendaZ.Value);
            a.Puesto.transform.localRotation = Quaternion.Euler(a.TiendaInclinacion.Value, a.TiendaGiro.Value, a.TiendaVuelta.Value);
            a.Puesto.AddComponent<MeshFilter>().sharedMesh = a.Objeto.GetComponentInChildren<MeshFilter>().sharedMesh;
            a.Puesto.AddComponent<MeshRenderer>();
            a.Puesto.AddComponent<ShopItemSpawner>().itemPrefab = a.Objeto;
            Logger.LogDebug("TIENDA: " + a.Clave + " a la venta en " + a.TiendaRaiz.Value);
        }

        // ---------------- Cada fotograma ----------------

        private void Update()
        {
            if (teclaLocalizar != null && teclaLocalizar.Value != KeyCode.None && Input.GetKeyDown(teclaLocalizar.Value)) Localizar();

            // Las tiendas se recolocan en vivo si cambias los valores en F1
            foreach (Arma a in Armas)
            {
                if (a.Puesto == null) continue;
                a.Puesto.transform.localPosition = new Vector3(a.TiendaX.Value, a.TiendaY.Value, a.TiendaZ.Value);
                a.Puesto.transform.localRotation = Quaternion.Euler(a.TiendaInclinacion.Value, a.TiendaGiro.Value, a.TiendaVuelta.Value);
            }

            if (fase != Fase.Nada) ActualizarVuelo();
        }

        private void Localizar()
        {
            Camera cam = Camera.main;
            RaycastHit g;
            if (cam != null && Physics.Raycast(cam.transform.position, cam.transform.forward, out g, 20f, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform raiz = g.transform.root;
                Vector3 l = raiz.InverseTransformPoint(g.point);
                Logger.LogDebug("LOCALIZAR: escena '" + raiz.gameObject.scene.name + "', raiz '" + raiz.name + "', objeto '" + g.transform.name +
                    "', punto respecto a la raiz (" + l.x.ToString("F3") + ", " + l.y.ToString("F3") + ", " + l.z.ToString("F3") + ")");
            }
        }

        // Dónde agarras la cuerda: un poco por debajo y delante de los ojos
        private static Vector3 Mano(Transform cam)
        {
            return cam.position + cam.forward * 0.4f - cam.up * 0.3f + cam.right * 0.2f;
        }

        // ---------------- Lanzar ----------------

        // La llama el hacha cuando pulsas R con ella en la mano
        public void PulsarUsar(GarfioItem hacha)
        {
            if (fase == Fase.Nada) Lanzar(hacha);
            else Soltar("soltado con R");
        }

        private void Lanzar(GarfioItem hacha)
        {
            Camera cam = Camera.main;
            Arma elegida = hacha.Datos;
            if (cam == null || elegida == null || elegida.Vuelo == null) return;
            Transform c = cam.transform;

            objetivo = null;
            cuerpo = null;
            Vector3 punto = c.position + c.forward * Alcance;

            pescar = false;
            float primerGolpe = float.MaxValue;
            RaycastHit[] golpes = Physics.RaycastAll(c.position, c.forward, Alcance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(golpes, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit g in golpes)
            {
                if (g.distance < 1.2f) continue;   // tu cuerpo o lo que llevas en la mano
                ShipItem item = Buscar(g.collider);
                if (item == hacha || g.collider.transform.IsChildOf(hacha.transform)) continue;   // el propio arpón (es largo)
                punto = g.point;
                primerGolpe = g.distance;
                string motivo = Valido(item);
                Logger.LogDebug("HACHA: toca '" + g.collider.name + "' a " + g.distance.ToString("F1") + " m" + (motivo != null ? " (" + motivo + ")" : ""));
                if (motivo == null)
                {
                    objetivo = item;
                    cuerpo = item.itemRigidbodyC.GetComponent<Rigidbody>();
                    golpeLocal = item.transform.InverseTransformPoint(g.point);
                }
                break;
            }

            // El arpón que no da a nada y cae al mar: a veces sale con un pez
            if (!elegida.Gira && objetivo == null && c.forward.y < -0.02f)
            {
                float dAgua = (NivelMar - c.position.y) / c.forward.y;
                if (dAgua > 1.5f && dAgua <= Alcance && dAgua < primerGolpe + 0.3f)
                {
                    punto = c.position + c.forward * dAgua;
                    float dado = Random.value * 100f;
                    pescar = dado < ProbPesca.Value;
                    Logger.LogDebug("ARPON: cae al mar a " + dAgua.ToString("F1") + " m" + (pescar ? " ... y hay pez!" : " (sin pez)"));
                }
            }

            arma = elegida;
            lanzadora = hacha;
            lanzadora.Esconder(true);
            tLanzado = Time.time;
            copia = Instantiate(elegida.Vuelo);
            foreach (Collider col in copia.GetComponentsInChildren<Collider>()) Destroy(col);
            desde = Mano(c);
            hasta = punto;
            tVuelo = 0f;
            durVuelo = Mathf.Max(0.2f, Vector3.Distance(desde, hasta) / elegida.Vel);
            vueltas = Mathf.Max(1f, Mathf.Round(durVuelo * 2.5f));   // vueltas completas: acaba con la hoja delante
            giro = 0f;
            fase = Fase.Volando;
        }

        // Devuelve null si se puede traer, o el motivo por el que no
        private string Valido(ShipItem item)
        {
            if (item == null) return "no se puede traer";
            if (!item.sold) return "es de una tienda";
            if (item.held != null || item.itemRigidbodyC == null) return "no se puede mover ahora";
            Rigidbody rb = item.itemRigidbodyC.GetComponent<Rigidbody>();
            if (rb == null || rb.isKinematic) return "esta fijo";
            if (Vector3.Distance(item.transform.position, rb.position) > 1f) return "ya va dentro de un barco";
            return null;
        }

        private ShipItem Buscar(Collider c)
        {
            ShipItem item = c.GetComponentInParent<ShipItem>();
            if (item != null) return item;
            foreach (ShipItem s in FindObjectsOfType<ShipItem>())
            {
                if (s.itemRigidbodyC == null) continue;
                Transform t = s.itemRigidbodyC.transform;
                if (c.transform == t || c.transform.IsChildOf(t)) return s;
            }
            return null;
        }

        private void ActualizarVuelo()
        {
            Camera cam = Camera.main;
            if (cam == null || (copia == null && fase != Fase.Clavada)) { Soltar("se ha perdido el hacha"); return; }
            if (lanzadora == null || lanzadora.held == null)
            {
                // Has pulsado F para coger lo que traías (el juego suelta lo que llevas en la mano):
                // guardamos el hacha y te damos el objeto
                GarfioItem h = lanzadora;
                ShipItem traido = fase == Fase.Clavada ? objetivo : null;
                Soltar("has soltado el hacha (" + (Time.time - tLanzado).ToString("F2") + " s despues de lanzarla, fase " + fase + ")");
                if (h != null) StartCoroutine(GuardarYCoger(h, traido));
                return;
            }
            Transform c = cam.transform;

            if (fase == Fase.Pescando) { DibujarCuerda(c); return; }

            if (fase == Fase.Volando || fase == Fase.Volviendo)
            {
                // Si va a por un objeto, el destino sigue al objeto (se mueve con las olas)
                if (fase == Fase.Volando && objetivo != null) hasta = objetivo.transform.TransformPoint(golpeLocal);
                if (fase == Fase.Volviendo) hasta = Mano(c);

                tVuelo += Time.deltaTime;
                float t = Mathf.Clamp01(tVuelo / durVuelo);
                Vector3 p = Vector3.Lerp(desde, hasta, t);
                p += Vector3.up * Mathf.Sin(t * Mathf.PI) * Vector3.Distance(desde, hasta) * 0.08f;   // un poco de arco
                Vector3 dir = (hasta - desde);
                if (dir.sqrMagnitude < 0.0001f) dir = c.forward;

                Quaternion rot;
                if (arma.Gira)
                {
                    // El hacha da vueltas sobre sí misma y termina justo con el filo por delante
                    Vector3 ejeGiro = Vector3.Cross(arma.HojaDir, arma.MangoDir).normalized;
                    float angulo = fase == Fase.Volando ? -(1f - t) * vueltas * 360f : (giro += Time.deltaTime * 720f);
                    rot = GiroClavada(arma, dir.normalized) * Quaternion.AngleAxis(angulo, ejeGiro);
                }
                else
                {
                    // El arpón va de punta; al volver, la cuerda tira de la argolla y viene de culo
                    rot = GiroClavada(arma, fase == Fase.Volando ? dir.normalized : -dir.normalized);
                }
                copia.transform.rotation = rot;
                // El filo (o la punta) va por la trayectoria y se mete un poco al clavarse
                copia.transform.position = p - rot * arma.FiloLocal + dir.normalized * (fase == Fase.Volando ? arma.Hundir : 0f);

                if (t >= 1f)
                {
                    if (fase == Fase.Volviendo) { Soltar("ha vuelto"); return; }
                    if (objetivo != null && cuerpo != null)
                    {
                        // Se clava: desde ahora va pegada al objeto
                        copia.transform.SetParent(objetivo.transform, true);
                        largo = Vector3.Distance(Mano(c), objetivo.transform.position);
                        inicio = Time.time;
                        fase = Fase.Clavada;
                        Logger.LogDebug(arma.Clave.ToUpper() + ": clavada en '" + objetivo.name + "'");
                    }
                    else if (pescar && SacarPez(copia.transform.TransformPoint(arma.FiloLocal)))
                    {
                        fase = Fase.Pescando;   // espera a que el pez esté listo para tirar de él
                    }
                    else
                    {
                        // Ha fallado: vuelve a tu mano
                        desde = copia.transform.position;
                        tVuelo = 0f;
                        durVuelo = Mathf.Max(0.3f, Vector3.Distance(desde, Mano(c)) / arma.Vel * 1.5f);
                        fase = Fase.Volviendo;
                    }
                }
            }
            if (fase == Fase.Clavada && !arma.Gira && copia != null && objetivo != null) ColocarArponClavado(c);
            DibujarCuerda(c);
        }

        // El arpón clavado se pone en la línea de la cuerda (el palo hacia ti). Cuando el objeto se acerca,
        // se va poniendo de pie (punta clavada hacia abajo, palo hacia arriba) para no atravesarte
        private void ColocarArponClavado(Transform c)
        {
            Vector3 golpe = objetivo.transform.TransformPoint(golpeLocal);
            Vector3 hacia = golpe - Mano(c);
            float largoArpon = (arma.FiloLocal - arma.ArgollaLocal).magnitude;
            float cerca = Mathf.Clamp01((largoArpon + 1f - hacia.magnitude) / largoArpon);   // 0 = lejos, 1 = pegado a ti
            Vector3 dir = Vector3.Slerp(hacia.normalized, Vector3.down, cerca);
            Quaternion rot = GiroClavada(arma, dir);
            copia.transform.rotation = rot;
            copia.transform.position = golpe - rot * arma.FiloLocal + dir * arma.Hundir;
        }

        // Crea el pez que toca en esta zona del mar (el mismo que saldría con la caña) clavado en la punta
        private bool SacarPez(Vector3 donde)
        {
            try
            {
                object oceano = AccessTools.Field(typeof(OceanFishes), "instance").GetValue(null);
                if (oceano == null) { Logger.LogWarning("ARPON: no encuentro los peces del mar"); return false; }
                GameObject modelo = AccessTools.Method(typeof(OceanFishes), "GetFish").Invoke(oceano, new object[] { copia.transform }) as GameObject;
                if (modelo == null) { Logger.LogDebug("ARPON: aqui no hay peces"); return false; }
                GameObject pez = Instantiate(modelo, donde, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                ShipItem si = pez.GetComponent<ShipItem>();
                if (si == null) { Destroy(pez); Logger.LogWarning("ARPON: el pez '" + modelo.name + "' no es un objeto"); return false; }
                Traverse.Create(si).Field("sold").SetValue(true);
                SaveablePrefab guardable = pez.GetComponent<SaveablePrefab>();
                MethodInfo registrar = guardable != null ? AccessTools.Method(typeof(SaveablePrefab), "RegisterToSave") : null;
                if (registrar != null && registrar.GetParameters().Length == 0) registrar.Invoke(guardable, null);
                else Logger.LogWarning("ARPON: no he podido apuntar el pez para guardarlo en la partida");
                Logger.LogDebug("ARPON: pez pescado: '" + modelo.name + "'");
                StartCoroutine(EngancharPez(si));
                return true;
            }
            catch (System.Exception e) { Logger.LogWarning("ARPON: no he podido pescar: " + (e.InnerException ?? e).Message); return false; }
        }

        // El pez necesita un momento para tener su física; después se tira de él como de cualquier objeto
        private IEnumerator EngancharPez(ShipItem pez)
        {
            // Primero esperamos un momento siempre (el arpón aún no ha pasado a "pescando" cuando esto empieza)
            yield return new WaitForFixedUpdate();
            for (int i = 0; i < 150 && pez != null && pez.itemRigidbodyC == null; i++) yield return new WaitForFixedUpdate();
            if (fase != Fase.Pescando || copia == null) yield break;
            Camera cam = Camera.main;
            if (pez == null || pez.itemRigidbodyC == null || cam == null) { Soltar("el pez se ha escapado"); yield break; }
            objetivo = pez;
            cuerpo = pez.itemRigidbodyC.GetComponent<Rigidbody>();
            golpeLocal = pez.transform.InverseTransformPoint(copia.transform.TransformPoint(arma.FiloLocal));
            copia.transform.SetParent(pez.transform, true);
            largo = Vector3.Distance(Mano(cam.transform), pez.transform.position);
            inicio = Time.time;
            fase = Fase.Clavada;
            Logger.LogDebug("ARPON: pez clavado, tira de la cuerda");
        }

        private void FixedUpdate()
        {
            if (fase != Fase.Clavada) return;
            Camera cam = Camera.main;
            if (cam == null || cuerpo == null || objetivo == null) { Soltar("se ha perdido el objeto"); return; }
            if (objetivo.held != null) { Soltar("lo has cogido"); return; }

            // Cuando el objeto entra en un barco, el juego mueve su física a una "copia" del barco
            // en otro sitio: esta conversión lleva tu mano a ese mismo sitio para tirar siempre bien
            Matrix4x4 aFisica = cuerpo.transform.localToWorldMatrix * objetivo.transform.worldToLocalMatrix;
            Vector3 mano = aFisica.MultiplyPoint3x4(Mano(cam.transform));
            Vector3 hacia = mano - cuerpo.position;
            float dist = hacia.magnitude;

            largo = Mathf.Min(largo, dist + 0.5f);
            largo = Mathf.Max(CuerdaMin, largo - Recoger * Time.fixedDeltaTime);

            if (dist <= CuerdaMin + 0.6f) inicio = Time.time;
            else if (Time.time - inicio > TiempoMax) { Soltar("ha tardado demasiado"); return; }

            float estirada = dist - largo;
            if (estirada <= 0f) return;
            Vector3 dir = hacia / dist;
            float tiro = Mathf.Min(estirada * Rigidez, TiroMax) - Vector3.Dot(cuerpo.velocity, dir) * Freno;
            if (tiro > 0f) cuerpo.AddForce(dir * tiro, ForceMode.Acceleration);
            if (estirada > 0.8f)
                cuerpo.AddForce(aFisica.MultiplyVector(Vector3.up).normalized * Mathf.Min((estirada - 0.8f) * 12f, 14f), ForceMode.Acceleration);
            cuerpo.WakeUp();
        }

        private void DibujarCuerda(Transform c)
        {
            if (cuerda == null)
            {
                GameObject go = new GameObject("HachaCuerda");
                DontDestroyOnLoad(go);
                cuerda = go.AddComponent<LineRenderer>();
                cuerda.positionCount = 2;
                cuerda.startWidth = 0.015f;
                cuerda.endWidth = 0.015f;
                cuerda.material = new Material(Shader.Find("Sprites/Default"));
                Color color = new Color(0.72f, 0.63f, 0.48f);   // como la cuerda del modelo
                cuerda.startColor = color;
                cuerda.endColor = color;
            }
            cuerda.enabled = true;
            cuerda.SetPosition(0, Mano(c));
            Vector3 fin = copia != null ? copia.transform.TransformPoint(arma.ArgollaLocal)
                : (objetivo != null ? objetivo.transform.TransformPoint(golpeLocal) : Mano(c));
            cuerda.SetPosition(1, fin);
        }

        // Guarda el hacha en el inventario (o, si está lleno, la deja a tus pies) y te pone en la mano lo que traías
        private IEnumerator GuardarYCoger(GarfioItem h, ShipItem traido)
        {
            yield return new WaitForFixedUpdate();
            if (h == null || h.held != null) yield break;
            if (!AlInventario(h)) PonerAPies(h);
            yield return new WaitForFixedUpdate();
            Camera cam = Camera.main;
            if (traido != null && traido.held == null && cam != null &&
                Vector3.Distance(cam.transform.position, traido.transform.position) < 3.5f)
            {
                AlasManos(traido);
            }
        }

        // Mete el hacha en un hueco libre del inventario, con la misma orden que usa el juego al cargar
        // la partida (SaveablePrefab.PutInInventory). Los huecos son los botones GPButtonInventorySlot.
        private bool AlInventario(GarfioItem h)
        {
            SaveablePrefab guardable = h.GetComponent<SaveablePrefab>();
            if (guardable == null) return false;
            MethodInfo poner = typeof(SaveablePrefab).GetMethod("PutInInventory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (poner == null) return false;

            Component[] huecos = FindObjectsOfType<GPButtonInventorySlot>();
            foreach (Component hueco in huecos)
            {
                System.Type t = hueco.GetType();
                int numero = -1;
                bool ocupado = false, hayCampoObjeto = false;
                foreach (FieldInfo fi in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (fi.FieldType == typeof(int) && (fi.Name == "slotIndex" || fi.Name == "slotNumber" || fi.Name == "slot" || fi.Name == "index"))
                        numero = (int)fi.GetValue(hueco);
                    if (typeof(PickupableItem).IsAssignableFrom(fi.FieldType) || typeof(ShipItem).IsAssignableFrom(fi.FieldType) || fi.FieldType == typeof(GameObject))
                    {
                        if (fi.Name.ToLower().Contains("item") || fi.Name.ToLower().Contains("current"))
                        {
                            hayCampoObjeto = true;
                            object v = fi.GetValue(hueco);
                            if (v != null && !v.Equals(null)) ocupado = true;
                        }
                    }
                }
                if (numero < 0 || !hayCampoObjeto || ocupado) continue;
                try
                {
                    object r = poner.Invoke(guardable, new object[] { numero });
                    if (r is IEnumerator) StartCoroutine((IEnumerator)r);
                    Logger.LogDebug("HACHA: guardada en el hueco " + numero + " del inventario");
                    return true;
                }
                catch (System.Exception e) { Logger.LogWarning("HACHA: no he podido guardarla: " + (e.InnerException ?? e).Message); return false; }
            }
            Logger.LogDebug("HACHA: no hay hueco libre en el inventario (o no se como mirarlo)");
            return false;
        }

        // El hacha aparece en el suelo, a tus pies (así no cae por la borda)
        private void PonerAPies(GarfioItem h)
        {
            Camera cam = Camera.main;
            if (h == null || h.held != null || h.itemRigidbodyC == null || cam == null) return;
            Rigidbody rb = h.itemRigidbodyC.GetComponent<Rigidbody>();
            if (rb == null) return;
            Vector3 pies = cam.transform.position - Vector3.up * 1.3f;
            // Por si el hacha ya está dentro del barco (su física va aparte), llevamos el punto a ese sitio
            Matrix4x4 aFisica = rb.transform.localToWorldMatrix * h.transform.worldToLocalMatrix;
            Vector3 destino = aFisica.MultiplyPoint3x4(pies);
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = destino;
            rb.transform.position = destino;
            Logger.LogDebug("HACHA: puesta a tus pies");
        }

        // Te pone en la mano un objeto, con la misma orden del juego que cuando coges algo con F
        private void AlasManos(ShipItem item)
        {
            GoPointer puntero = FindObjectOfType<GoPointer>();
            if (puntero == null) return;
            foreach (MethodInfo m in typeof(GoPointer).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (m.Name != "PickUpItem") continue;
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length != 1 || !ps[0].ParameterType.IsAssignableFrom(item.GetType())) continue;
                try { m.Invoke(puntero, new object[] { item }); Logger.LogDebug("HACHA: te doy '" + item.name + "'"); return; }
                catch (System.Exception e) { Logger.LogWarning("HACHA: no he podido darte el objeto: " + (e.InnerException ?? e).Message); }
            }
        }

        public void Soltar(string motivo)
        {
            if (fase == Fase.Nada) return;
            Logger.LogDebug("HACHA: " + motivo);
            fase = Fase.Nada;
            objetivo = null;
            cuerpo = null;
            if (copia != null) Destroy(copia);
            copia = null;
            if (lanzadora != null) lanzadora.Esconder(false);
            lanzadora = null;
            arma = null;
            if (cuerda != null) cuerda.enabled = false;
        }
    }
}

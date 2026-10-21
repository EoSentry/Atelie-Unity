using UnityEngine;

namespace Atelie
{
    /// <summary>
    /// Coloque este componente num GameObject vazio de uma cena vazia e aperte Play:
    /// ele cria câmera, mapa, editor, cursor e o painel de teste sozinho.
    /// Se você já tiver sua própria cena/UI, use só o MapEditor (veja o README).
    /// </summary>
    public class AtelieBootstrap : MonoBehaviour
    {
        [Tooltip("Sua biblioteca de assets. Vazio = usa texturas provisórias geradas por código.")]
        public AtelieLibrary library;
        // tamanho do primeiro mapa em pixels (máximo 8192)
        public int mapWidth = 3072;
        public int mapHeight = 2048;
        // liga/desliga o painel de teste (IMGUI)
        public bool showTestPanel = true;

        public MapEditor Editor { get; private set; }

        // Monta tudo: acha/cria a câmera, cria o MapEditor com o primeiro mapa, o cursor do pincel e o painel de teste.
        void Start()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject cg = new GameObject("Main Camera");
                cg.tag = "MainCamera";
                cam = cg.AddComponent<Camera>();
            }
            // a câmera ganha o componente MapCamera (zoom e arrastar)
            MapCamera mc = cam.GetComponent<MapCamera>();
            if (mc == null) mc = cam.gameObject.AddComponent<MapCamera>();

            // usa a sua biblioteca, ou a provisória (gerada por código) se não houver
            AtelieLibrary lib = library != null ? library : FallbackArt.MakeLibrary();

            GameObject go = new GameObject("MapEditor");
            Editor = go.AddComponent<MapEditor>();
            Editor.mapCamera = mc;
            Editor.Init(lib, mapWidth, mapHeight);

            BrushCursor cur = go.AddComponent<BrushCursor>();
            cur.editor = Editor; cur.mapCamera = mc;

            if (showTestPanel)
            {
                AtelieHud hud = go.AddComponent<AtelieHud>();
                hud.editor = Editor; hud.mapCamera = mc;
            }
        }
    }
}

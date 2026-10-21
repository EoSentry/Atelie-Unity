using UnityEngine;

namespace Atelie
{
    /// <summary>
    /// Câmera ortográfica do editor: arrastar com botão do meio / direito (ou Espaço + esquerdo) move,
    /// a roda do mouse dá zoom na posição do cursor.
    /// zoom = pixels de TELA por pixel de MAPA.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MapCamera : MonoBehaviour
    {
        // Pixels do mapa por unidade do mundo da Unity (100 = um mapa de 3000 px tem 30 unidades de largura).
        public const float PPU = 100f;          // pixels do mapa por unidade do mundo
        // zoom = pixels de TELA por pixel de MAPA (0.4 = o mapa aparece a 40% do tamanho real)
        public float zoom = .4f;
        public float minZoom = .03f, maxZoom = 8f;
        public bool inputEnabled = true;
        public System.Func<Vector2, bool> PointerBlocked;   // true = mouse está sobre a UI

        // a câmera Unity que este componente controla
        Camera cam;
        Vector2 lastMouse;
        bool panning;
        public bool IsPanning { get { return panning; } }

        void Awake() { Setup(); }

        // Prepara a câmera: ortográfica (sem perspectiva), fundo cinza-escuro e posição Z = -10 (olhando para o mapa).
        void Setup()
        {
            if (cam != null) return;
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.09f, .08f, .07f, 1f);
            Vector3 p = transform.position; p.z = -10f; transform.position = p;
            ApplyZoom();
        }

        // Converte o zoom no 'orthographicSize' da câmera (metade da altura visível, em unidades do mundo).
        void ApplyZoom()
        {
            zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
            cam.orthographicSize = Screen.height / (2f * zoom * PPU);
        }

        public Camera Cam { get { Setup(); return cam; } }

        // Converte uma posição da tela (pixels) em posição no mapa (pixels do mapa).
        public Vector2 ScreenToMap(Vector2 sp)
        {
            Setup();
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, -transform.position.z));
            return new Vector2(w.x * PPU, w.y * PPU);
        }

        // Centraliza a câmera num ponto do mapa.
        public void FocusMap(Vector2 mapPoint)
        {
            Vector3 p = transform.position; p.x = mapPoint.x / PPU; p.y = mapPoint.y / PPU; transform.position = p;
        }

        /// <summary>Enquadra o mapa inteiro na tela.</summary>
        // Enquadra o mapa inteiro na tela, com uma pequena margem (92%).
        public void Fit(int mapW, int mapH)
        {
            Setup();
            float zx = Screen.width / (float)mapW, zy = Screen.height / (float)mapH;
            zoom = Mathf.Clamp(Mathf.Min(zx, zy) * .92f, minZoom, maxZoom);
            ApplyZoom();
            FocusMap(new Vector2(mapW * .5f, mapH * .5f));
        }

        // Muda o zoom mantendo o ponto sob o cursor parado (zoom 'no mouse').
        public void SetZoomAt(float newZoom, Vector2 screenPos)
        {
            Setup();
            Vector2 before = ScreenToMap(screenPos);
            zoom = Mathf.Clamp(newZoom, minZoom, maxZoom);
            ApplyZoom();
            Vector2 after = ScreenToMap(screenPos);
            Vector3 p = transform.position;
            p.x += (before.x - after.x) / PPU; p.y += (before.y - after.y) / PPU;
            transform.position = p;
        }

        // Todo quadro: roda do mouse = zoom; botão do meio/direito (ou Espaço + esquerdo) = arrastar a câmera.
        void Update()
        {
            Setup();
            if (!inputEnabled) { panning = false; return; }
            Vector2 mp = Inp.MousePos;
            bool blocked = PointerBlocked != null && PointerBlocked(mp);

            float sc = Inp.Scroll;
            if (!blocked && Mathf.Abs(sc) > .001f) SetZoomAt(zoom * Mathf.Pow(1.18f, sc), mp);

            bool wantPan = !blocked && (Inp.MiddleHeld || Inp.RightHeld || (Inp.Held(Hk.Space) && Inp.LeftHeld));
            if (wantPan)
            {
                if (panning)
                {
                    Vector2 d = mp - lastMouse;
                    Vector3 p = transform.position;
                    p.x -= d.x / (zoom * PPU); p.y -= d.y / (zoom * PPU);
                    transform.position = p;
                }
                panning = true;
            }
            else if (!Inp.MiddleHeld && !Inp.RightHeld && !Inp.LeftHeld) panning = false;
            else if (!wantPan) panning = false;
            lastMouse = mp;
            ApplyZoom();
        }
    }
}

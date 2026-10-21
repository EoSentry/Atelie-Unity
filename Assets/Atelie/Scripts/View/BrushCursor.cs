using UnityEngine;

namespace Atelie
{
    /// <summary>Círculo que mostra o tamanho do pincel sob o cursor (e a área de dureza total).</summary>
    public class BrushCursor : MonoBehaviour
    {
        // referências: o editor (de onde lê tamanho do pincel e posição do mouse) e a câmera (para o zoom)
        public MapEditor editor;
        public MapCamera mapCamera;
        // três anéis: sombra (preto), anel (branco, tamanho do pincel) e dureza (branco fraco, parte 100% cheia)
        LineRenderer outer, inner, shadow;
        Material mat;
        // quantos segmentos formam o círculo
        const int Seg = 64;

        // Cria um anel (LineRenderer) com a cor dada.
        LineRenderer MakeLine(string n, Color c)
        {
            GameObject g = new GameObject(n);
            g.transform.SetParent(transform, false);
            LineRenderer l = g.AddComponent<LineRenderer>();
            l.material = mat;
            l.loop = true;
            l.useWorldSpace = true;
            l.positionCount = Seg;
            l.startColor = c; l.endColor = c;
            l.sortingOrder = 1000;
            l.numCapVertices = 0;
            return l;
        }

        // Cria os anéis. Usa o shader 'Sprites/Default' (simples e sempre incluído no jogo).
        void Start()
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) { enabled = false; return; }
            mat = new Material(sh);
            shadow = MakeLine("sombra", new Color(0, 0, 0, .55f));
            outer = MakeLine("anel", new Color(1, 1, 1, .95f));
            inner = MakeLine("dureza", new Color(1, 1, 1, .55f));
        }

        // Desenha um círculo no mapa: posiciona os pontos em volta do centro e define a espessura da linha.
        void Circle(LineRenderer l, Vector2 centerMap, float radiusMap, float widthWorld)
        {
            l.startWidth = widthWorld; l.endWidth = widthWorld;
            for (int i = 0; i < Seg; i++)
            {
                float a = i / (float)Seg * Mathf.PI * 2f;
                l.SetPosition(i, new Vector3((centerMap.x + Mathf.Cos(a) * radiusMap) / MapCamera.PPU,
                                             (centerMap.y + Mathf.Sin(a) * radiusMap) / MapCamera.PPU, 0f));
            }
        }

        // Todo quadro (depois do resto): mostra os anéis sob o mouse e esconde quando o mouse está na UI, fora do mapa ou na ferramenta Mover.
        void LateUpdate()
        {
            if (outer == null || editor == null || editor.session == null) return;
            bool blocked = editor.PointerBlocked != null && editor.PointerBlocked(Inp.MousePos);
            bool show = !blocked && editor.tool != Tool.Pan && editor.PointerInside;
            outer.enabled = show; inner.enabled = show; shadow.enabled = show;
            if (!show) return;
            float z = mapCamera != null ? mapCamera.zoom : .5f;
            float px = 1.5f / (z * MapCamera.PPU);          // 1,5 pixel de tela, em unidades do mundo
            float r = editor.brush.size * .5f;
            Vector2 c = editor.PointerMap;
            Circle(shadow, c, r + 1.5f / z, px * 1.6f);
            Circle(outer, c, r, px);
            Circle(inner, c, r * editor.brush.hardness, px * .7f);
        }
    }
}

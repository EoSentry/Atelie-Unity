using System.Collections.Generic;
using UnityEngine;

namespace Atelie
{
    /// <summary>Um bloco de desfazer: como estavam os pixels de uma região ANTES e DEPOIS de um traço.</summary>
    public sealed class TilePatch
    {
        public int x, y, w, h;               // posição e tamanho do bloco na camada
        public Color32[] before, after;      // pixels antes e depois
    }

    /// <summary>Conjunto de blocos de UMA camada que mudaram num traço (é o que o desfazer guarda).</summary>
    public sealed class RasterPatch
    {
        public PixelLayer layer;
        public List<TilePatch> tiles = new List<TilePatch>();

        /// <summary>Copia de volta os pixels: useAfter=true refaz, false desfaz.</summary>
        public void Apply(bool useAfter)
        {
            for (int t = 0; t < tiles.Count; t++)
            {
                TilePatch p = tiles[t];
                Color32[] src = useAfter ? p.after : p.before;
                // copia linha por linha para dentro da camada
                for (int row = 0; row < p.h; row++)
                    System.Array.Copy(src, row * p.w, layer.px, (p.y + row) * layer.W + p.x, p.w);
                layer.MarkDirty(new RectInt(p.x, p.y, p.w, p.h));   // avisa que precisa reenviar à GPU
            }
        }

        /// <summary>Retângulo que cobre todos os blocos (usado para atualizar o contorno da costa).</summary>
        public RectInt Bounds()
        {
            if (tiles.Count == 0) return new RectInt(0, 0, 0, 0);
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = 0, y1 = 0;
            for (int t = 0; t < tiles.Count; t++)
            {
                TilePatch p = tiles[t];
                x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y);
                x1 = Mathf.Max(x1, p.x + p.w); y1 = Mathf.Max(y1, p.y + p.h);
            }
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }
    }

    /// <summary>
    /// Uma camada de pixels (ex.: o mar, a terra). Os pixels ficam num array na memória do
    /// computador (px) para o pincel poder editar rápido, e uma Texture2D espelha esse array
    /// na placa de vídeo para ser desenhada na tela.
    /// A linha 0 é a parte de BAIXO do mapa (convenção da Unity).
    /// </summary>
    public sealed class PixelLayer
    {
        public readonly int W, H;            // largura e altura em pixels
        public readonly Color32[] px;        // os pixels (linha por linha)
        public readonly Texture2D tex;       // cópia na GPU
        public readonly string name;

        // retângulo "sujo": a parte que mudou e ainda não foi enviada à GPU
        int dx0 = int.MaxValue, dy0 = int.MaxValue, dx1 = -1, dy1 = -1;
        Texture2D scratch;                   // textura pequena temporária usada para enviar só a região suja
        Color32[] scratchBuf;

        // gravação para o desfazer: o mapa é dividido em blocos de 128x128 e só guardamos
        // uma cópia dos blocos que o traço realmente tocou (economiza muita memória)
        const int TS = 128;
        Dictionary<int, TilePatch> rec;

        public PixelLayer(int w, int h, string name)
        {
            W = w; H = h; this.name = name;
            px = new Color32[w * h];
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false);   // false = sem mipmaps
            tex.name = name;
            tex.wrapMode = TextureWrapMode.Clamp;                      // não repete nas bordas
            tex.filterMode = FilterMode.Bilinear;                      // suaviza ao dar zoom
            UploadAll();
        }

        /// <summary>Libera a memória da GPU (chamar ao trocar de mapa).</summary>
        public void Destroy()
        {
            if (tex != null) Object.Destroy(tex);
            if (scratch != null) Object.Destroy(scratch);
        }

        /// <summary>Pinta a camada inteira com uma cor.</summary>
        public void Clear(Color32 c)
        {
            for (int i = 0; i < px.Length; i++) px[i] = c;
            MarkAllDirty();
        }

        // ---------------- envio para a GPU ----------------

        /// <summary>Envia a camada INTEIRA para a GPU (lento em mapas grandes; usado só quando necessário).</summary>
        public void UploadAll()
        {
            tex.SetPixels32(px);
            tex.Apply(false);
            dx0 = int.MaxValue; dy0 = int.MaxValue; dx1 = -1; dy1 = -1;   // nada mais está sujo
        }

        public void MarkAllDirty() { dx0 = 0; dy0 = 0; dx1 = W; dy1 = H; }

        /// <summary>Marca uma região como alterada (aumenta o retângulo sujo para incluí-la).</summary>
        public void MarkDirty(RectInt r)
        {
            int x0 = Mathf.Max(0, r.xMin), y0 = Mathf.Max(0, r.yMin), x1 = Mathf.Min(W, r.xMax), y1 = Mathf.Min(H, r.yMax);
            if (x1 <= x0 || y1 <= y0) return;
            if (x0 < dx0) dx0 = x0;
            if (y0 < dy0) dy0 = y0;
            if (x1 > dx1) dx1 = x1;
            if (y1 > dy1) dy1 = y1;
        }

        public bool IsDirty { get { return dx1 > dx0 && dy1 > dy0; } }

        /// <summary>
        /// Envia para a GPU só a região que mudou. Deve ser chamado uma vez por quadro.
        /// Se a região for grande (mais da metade) ou a placa não suportar cópia parcial,
        /// envia tudo de uma vez.
        /// </summary>
        public void Flush()
        {
            if (!IsDirty) return;
            int rw = dx1 - dx0, rh = dy1 - dy0;
            bool full = rw * rh > (W * H) / 2 || !SystemInfo.copyTextureSupport.HasFlag(UnityEngine.Rendering.CopyTextureSupport.Basic);
            if (full)
            {
                UploadAll();
                return;
            }
            // 1) copia a região suja do array grande para um array pequeno
            int n = rw * rh;
            if (scratchBuf == null || scratchBuf.Length != n) scratchBuf = new Color32[n];
            for (int row = 0; row < rh; row++)
                System.Array.Copy(px, (dy0 + row) * W + dx0, scratchBuf, row * rw, rw);
            // 2) sobe o array pequeno para uma textura pequena
            if (scratch == null) scratch = new Texture2D(rw, rh, TextureFormat.RGBA32, false);
            else if (scratch.width != rw || scratch.height != rh) scratch.Reinitialize(rw, rh);
            scratch.SetPixels32(scratchBuf);
            scratch.Apply(false);
            // 3) a GPU copia a textura pequena para o lugar certo da textura grande
            Graphics.CopyTexture(scratch, 0, 0, 0, 0, rw, rh, tex, 0, 0, dx0, dy0);
            dx0 = int.MaxValue; dy0 = int.MaxValue; dx1 = -1; dy1 = -1;
        }

        // ---------------- desfazer ----------------

        /// <summary>Começa a gravar um traço (chamado quando o botão do mouse é apertado).</summary>
        public void BeginRecord() { rec = new Dictionary<int, TilePatch>(); }
        public bool Recording { get { return rec != null; } }

        /// <summary>
        /// Chamado ANTES de alterar uma região: guarda o "antes" dos blocos de 128x128 que ela toca
        /// (só na primeira vez que cada bloco é tocado durante o traço).
        /// </summary>
        public void Touch(int x, int y, int w, int h)
        {
            if (rec == null || w <= 0 || h <= 0) return;   // não está gravando
            int tx0 = x / TS, ty0 = y / TS, tx1 = (x + w - 1) / TS, ty1 = (y + h - 1) / TS;   // blocos cobertos
            int tw = (W + TS - 1) / TS;                                                       // blocos por linha
            for (int ty = ty0; ty <= ty1; ty++)
                for (int tx = tx0; tx <= tx1; tx++)
                {
                    int key = ty * tw + tx;
                    if (rec.ContainsKey(key)) continue;    // esse bloco já foi guardado
                    int px0 = tx * TS, py0 = ty * TS;
                    int pw = Mathf.Min(TS, W - px0), ph = Mathf.Min(TS, H - py0);   // blocos da borda são menores
                    TilePatch p = new TilePatch { x = px0, y = py0, w = pw, h = ph, before = new Color32[pw * ph] };
                    for (int row = 0; row < ph; row++)
                        System.Array.Copy(px, (py0 + row) * W + px0, p.before, row * pw, pw);
                    rec[key] = p;
                }
        }

        /// <summary>
        /// Termina o traço: guarda o "depois" de cada bloco tocado e descarta os que não mudaram.
        /// Devolve null se nada mudou de verdade.
        /// </summary>
        public RasterPatch EndRecord()
        {
            if (rec == null) return null;
            RasterPatch rp = new RasterPatch { layer = this };
            foreach (KeyValuePair<int, TilePatch> kv in rec)
            {
                TilePatch p = kv.Value;
                p.after = new Color32[p.w * p.h];
                bool changed = false;
                for (int row = 0; row < p.h; row++)
                {
                    System.Array.Copy(px, (p.y + row) * W + p.x, p.after, row * p.w, p.w);
                }
                // compara antes e depois: só vale a pena guardar se algum pixel mudou
                for (int i = 0; i < p.after.Length; i++)
                {
                    Color32 a = p.after[i], b = p.before[i];
                    if (a.r != b.r || a.g != b.g || a.b != b.b || a.a != b.a) { changed = true; break; }
                }
                if (changed) rp.tiles.Add(p);
            }
            rec = null;
            return rp.tiles.Count > 0 ? rp : null;
        }
    }
}

using UnityEngine;

namespace Atelie
{
    /// <summary>
    /// Modo de combinar o que o pincel pinta com o que já existe na camada:
    ///  Over     = pinta por cima (o normal)
    ///  DestOut  = apaga (deixa transparente onde o pincel passa)
    ///  Atop     = só troca a cor onde JÁ existe algo (não cria terra nova)
    ///  DestOver = pinta só nos espaços vazios, por baixo do que já existe
    /// </summary>
    public enum Comp { Over, DestOut, Atop, DestOver }

    public static class TextureUtil
    {
        /// <summary>
        /// Lê os pixels de uma textura, mesmo que ela não esteja com "Read/Write" ligado no Import Settings.
        /// Se estiver ligado é direto; senão copia a textura para uma RenderTexture e lê de lá.
        /// </summary>
        public static Color32[] ReadPixels(Texture2D t, out int w, out int h)
        {
            w = t.width; h = t.height;
            try { return t.GetPixels32(); }          // caminho rápido
            catch (UnityException) { }               // textura não legível: usa o caminho lento abaixo
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(t, rt);                    // copia a textura para a RenderTexture
            RenderTexture.active = rt;
            Texture2D tmp = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0);   // lê da RenderTexture
            tmp.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Color32[] res = tmp.GetPixels32();
            Object.Destroy(tmp);
            return res;
        }
    }

    /// <summary>
    /// "Pega cores" de uma textura que se repete (tile). Usa a posição do MUNDO, então a textura
    /// fica fixa no mapa: pintar duas vezes no mesmo lugar não muda o desenho, e traços vizinhos se emendam.
    /// </summary>
    public sealed class TexSampler
    {
        readonly Color32[] px;   // pixels da textura
        readonly int w, h;       // tamanho da textura
        readonly float k;        // quantos pixels da textura por pixel do mapa

        /// <param name="tileSize">quantos pixels do mapa a textura cobre antes de repetir</param>
        /// <param name="tint">cor que multiplica a textura (Color.white = sem mudança)</param>
        public TexSampler(Texture2D tex, float tileSize, Color tint)
        {
            px = TextureUtil.ReadPixels(tex, out w, out h);
            k = w / Mathf.Max(1f, tileSize);
            Color32 t = tint;
            // só gasta tempo aplicando a cor se ela não for branca
            if (t.r != 255 || t.g != 255 || t.b != 255)
            {
                for (int i = 0; i < px.Length; i++)
                {
                    Color32 c = px[i];
                    c.r = (byte)(c.r * t.r / 255); c.g = (byte)(c.g * t.g / 255); c.b = (byte)(c.b * t.b / 255);
                    px[i] = c;
                }
            }
        }

        /// <summary>Cor da textura no ponto (wx, wy) do mapa. Repete sem fim nas duas direções.</summary>
        public Color32 At(int wx, int wy)
        {
            int u = Mathf.FloorToInt(wx * k) % w; if (u < 0) u += w;   // coluna na textura
            int v = Mathf.FloorToInt(wy * k) % h; if (v < 0) v += h;   // linha na textura
            return px[v * w + u];
        }
    }

    /// <summary>
    /// O pincel. Cada "carimbo" é um círculo de borda quebrada por ruído preenchido com a textura
    /// escolhida. Arrastar o mouse = vários carimbos em fila. Tudo em pixels do mapa.
    /// Cada função devolve o retângulo que foi alterado.
    /// </summary>
    public static class BrushEngine
    {
        /// <summary>
        /// Calcula o quanto cada ponto do carimbo é "coberto" (0 = fora, 1 = cheio, entre = borda macia).
        /// Guarda as constantes do cálculo para não refazê-las a cada pixel.
        /// </summary>
        struct Mask
        {
            public float r;       // raio do pincel
            public float R;       // força do ruído na borda
            public float inv;     // 1 / largura da borda macia
            public float flat2;   // (raio do miolo 100% cheio)²
            public float lim;     // (raio máximo possível, contando o ruído)²

            public Mask(float r, float hard, float rough)
            {
                this.r = r;
                R = rough * .9f;
                float soft = Mathf.Max(.03f, 1f - hard);   // quanto do raio é borda macia
                inv = 1f / soft;
                float flat = Mathf.Max(0f, hard - R);      // miolo sempre cheio
                flat2 = flat * flat * r * r;
                lim = (1f + R) * (1f + R) * r * r;
            }

            /// <param name="q">distância² do centro</param>
            /// <param name="nzs">ruído já convertido para -1..1</param>
            public float Eval(float q, float nzs)
            {
                if (q > lim) return 0f;          // fora do alcance máximo
                if (q < flat2) return 1f;        // dentro do miolo: cobertura total
                // distância relativa (0 no centro, 1 na borda) deslocada pelo ruído => borda irregular
                float t = Mathf.Sqrt(q) / r + nzs * R;
                float a = (1f - t) * inv;        // transforma em cobertura de 0 a 1
                if (a <= 0f) return 0f;
                return a > 1f ? 1f : a;
            }
        }

        /// <summary>
        /// Combina UM pixel de origem (cor sr,sg,sb com opacidade sa) com o pixel de destino d,
        /// segundo o modo 'comp'. É a matemática clássica de composição de imagens.
        /// </summary>
        static void Blend(ref Color32 d, byte sr, byte sg, byte sb, float sa, Comp comp)
        {
            float da = d.a / 255f;   // opacidade do que já existe
            switch (comp)
            {
                case Comp.Over:      // origem por cima do destino
                    {
                        float oa = sa + da * (1f - sa);                          // opacidade final
                        if (oa <= .0001f) { d = new Color32(0, 0, 0, 0); return; }
                        float wd = da * (1f - sa);                               // peso do que já existia
                        d.r = (byte)((sr * sa + d.r * wd) / oa + .5f);
                        d.g = (byte)((sg * sa + d.g * wd) / oa + .5f);
                        d.b = (byte)((sb * sa + d.b * wd) / oa + .5f);
                        d.a = (byte)(oa * 255f + .5f);
                        break;
                    }
                case Comp.DestOut:   // apaga: reduz a opacidade do destino
                    d.a = (byte)(da * (1f - sa) * 255f + .5f);
                    break;
                case Comp.Atop:      // mistura a cor, mas mantém a opacidade (só recolore)
                    {
                        float k = 1f - sa;
                        d.r = (byte)(sr * sa + d.r * k + .5f);
                        d.g = (byte)(sg * sa + d.g * k + .5f);
                        d.b = (byte)(sb * sa + d.b * k + .5f);
                        break;
                    }
                case Comp.DestOver:  // origem por BAIXO do destino (preenche só os vazios)
                    {
                        float oa = da + sa * (1f - da);
                        if (oa <= .0001f) return;
                        float ws = sa * (1f - da);
                        d.r = (byte)((d.r * da + sr * ws) / oa + .5f);
                        d.g = (byte)((d.g * da + sg * ws) / oa + .5f);
                        d.b = (byte)((d.b * da + sb * ws) / oa + .5f);
                        d.a = (byte)(oa * 255f + .5f);
                        break;
                    }
            }
        }

        /// <summary>Carimbo simples. hard = dureza (0 macio … 1 duro), alpha = opacidade,
        /// rough = quanto a borda é quebrada, rsc = tamanho do detalhe do ruído.</summary>
        public static RectInt Stamp(PixelLayer L, float x, float y, float r, TexSampler tex, float hard,
                                    float alpha, Comp comp, float rough, float rsc)
        {
            return Core(L, x, y, r, tex, hard, hard, false, alpha, comp, rough, rsc);
        }

        /// <summary>
        /// Carimbo com mesclagem: sobre terra/água que já existe, a borda esfuma suavemente (sem "adesivo");
        /// sobre o vazio (a costa) ela continua nítida.
        /// solid = true (mar) ou comp Atop: só suaviza a borda.
        /// </summary>
        public static RectInt StampBlend(PixelLayer L, float x, float y, float r, TexSampler tex, float hard,
                                         float alpha, Comp comp, float rough, float rsc, float blend, bool solid)
        {
            if (!(blend > .01f)) return Stamp(L, x, y, r, tex, hard, alpha, comp, rough, rsc);   // sem mesclagem
            float h2 = Mathf.Min(hard, 1f - blend * .9f);    // dureza reduzida da parte macia
            if (solid || comp == Comp.Atop) return Stamp(L, x, y, r, tex, h2, alpha, comp, rough, rsc);
            // modo "duplo": macio sobre o que existe + nítido onde está vazio
            return Core(L, x, y, r, tex, h2, hard, true, alpha, comp, rough, rsc);
        }

        /// <summary>O carimbo de verdade. Percorre cada pixel do quadrado em volta do pincel.</summary>
        static RectInt Core(PixelLayer L, float x, float y, float r, TexSampler tex, float hardSoft, float hardSharp,
                            bool dual, float alpha, Comp comp, float rough, float rsc)
        {
            NoiseUtil.InitRough();                // garante que a tabela de ruído existe
            rough = Mathf.Clamp01(rough);
            r = Mathf.Min(r, 300f);               // limite de raio por carimbo
            if (r < .5f) return new RectInt(0, 0, 0, 0);

            // lado do quadrado que contém o círculo (aumenta com o ruído) e seu canto inferior esquerdo
            int d = Mathf.CeilToInt(r * 2f * (1f + rough * .9f)) + 4;
            int x0 = Mathf.FloorToInt(x - d * .5f), y0 = Mathf.FloorToInt(y - d * .5f);

            // recorta o quadrado para ficar dentro da camada
            int ia = Mathf.Max(0, -x0), ib = Mathf.Min(d, L.W - x0);
            int ja = Mathf.Max(0, -y0), jb = Mathf.Min(d, L.H - y0);
            if (ia >= ib || ja >= jb) return new RectInt(0, 0, 0, 0);   // totalmente fora

            L.Touch(x0 + ia, y0 + ja, ib - ia, jb - ja);   // guarda o "antes" para o desfazer

            float cx = x - x0, cy = y - y0;                // centro do pincel dentro do quadrado
            Mask m1 = new Mask(r, hardSoft, rough);        // máscara macia
            Mask m0 = new Mask(r, hardSharp, rough);       // máscara nítida (modo duplo)
            float R = rough * .9f;
            float k = NoiseUtil.NZN / (float)NoiseUtil.NZP * rsc;   // escala do ruído
            float lim = m1.lim;
            Color32[] px = L.px;
            int W = L.W;

            for (int j = ja; j < jb; j++)
            {
                int wy = y0 + j;                       // y no mundo
                float dy = j - cy, dy2 = dy * dy;
                int row = wy * W;
                for (int i = ia; i < ib; i++)
                {
                    float dx = i - cx;
                    float q = dx * dx + dy2;           // distância² até o centro
                    if (q > lim) continue;             // longe demais: pula
                    int wx = x0 + i;                   // x no mundo
                    // ruído deste ponto do mundo, convertido para -1..1 (só se a borda for quebrada)
                    float nzs = R > 0f ? (NoiseUtil.RoughAt(wx, wy, k) - .5f) * 2f : 0f;

                    if (!dual)
                    {
                        float a = m1.Eval(q, nzs);                 // cobertura deste pixel
                        if (a <= 0f) continue;
                        byte cr = 0, cg = 0, cb = 0; float ta = 1f;
                        if (tex != null) { Color32 c = tex.At(wx, wy); cr = c.r; cg = c.g; cb = c.b; ta = c.a / 255f; }
                        Blend(ref px[row + wx], cr, cg, cb, a * alpha * ta, comp);
                    }
                    else
                    {
                        float a1 = m1.Eval(q, nzs);        // cobertura macia
                        float a0 = m0.Eval(q, nzs);        // cobertura nítida
                        if (a1 <= 0f && a0 <= 0f) continue;
                        byte cr = 0, cg = 0, cb = 0; float ta = 1f;
                        if (tex != null) { Color32 c = tex.At(wx, wy); cr = c.r; cg = c.g; cb = c.b; ta = c.a / 255f; }
                        int idx = row + wx;
                        float destA = px[idx].a / 255f;
                        // 1) parte macia: só onde já existe terra (multiplica pela opacidade que existe)
                        if (a1 > 0f && destA > 0f) Blend(ref px[idx], cr, cg, cb, a1 * ta * destA * alpha, Comp.Over);
                        // 2) parte nítida: só nos espaços vazios (a costa fica bem definida)
                        if (a0 > 0f) Blend(ref px[idx], cr, cg, cb, a0 * ta * alpha, Comp.DestOver);
                    }
                }
            }
            RectInt box = new RectInt(x0 + ia, y0 + ja, ib - ia, jb - ja);
            L.MarkDirty(box);     // avisa que essa região precisa ser reenviada à GPU
            return box;
        }

        /// <summary>Menor retângulo que contém os dois retângulos dados.</summary>
        public static RectInt Union(RectInt a, RectInt b)
        {
            if (a.width <= 0 || a.height <= 0) return b;
            if (b.width <= 0 || b.height <= 0) return a;
            int x0 = Mathf.Min(a.xMin, b.xMin), y0 = Mathf.Min(a.yMin, b.yMin);
            int x1 = Mathf.Max(a.xMax, b.xMax), y1 = Mathf.Max(a.yMax, b.yMax);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Atelie
{
    /// <summary>
    /// Desenha o contorno escuro (tinta) ao redor da terra, como num mapa desenhado à mão.
    /// Fica numa camada ABAIXO da terra, então só a faixa de fora aparece.
    /// </summary>
    public static class CoastRenderer
    {
        static int[] offX, offY;   // deslocamentos (dx, dy) dos pontos vizinhos dentro de um círculo pequeno

        /// <summary>Prepara a lista de vizinhos num raio de 2,6 pixels (feito uma única vez).</summary>
        static void InitOffsets()
        {
            if (offX != null) return;
            List<int> xs = new List<int>(), ys = new List<int>();
            const float rad = 2.6f;
            int n = Mathf.CeilToInt(rad);
            for (int dy = -n; dy <= n; dy++)
                for (int dx = -n; dx <= n; dx++)
                    if (dx * dx + dy * dy <= rad * rad) { xs.Add(dx); ys.Add(dy); }
            offX = xs.ToArray(); offY = ys.ToArray();
        }

        static readonly Color32 Ink = new Color32(40, 28, 18, 255);   // cor da tinta (marrom bem escuro)

        /// <summary>
        /// Recalcula o contorno só na região alterada (+ margem). Para cada pixel olha o quanto de terra
        /// existe nos vizinhos próximos: se há terra por perto, pinta tinta ali. O resultado é um "halo"
        /// de ~2,6 px em volta da terra.
        /// </summary>
        public static void Update(PixelLayer land, PixelLayer coast, RectInt area)
        {
            InitOffsets();
            int W = land.W, H = land.H;
            // aumenta a área em 4 px para pegar o contorno que também mudou ao redor
            int x0 = Mathf.Max(0, area.xMin - 4), y0 = Mathf.Max(0, area.yMin - 4);
            int x1 = Mathf.Min(W, area.xMax + 4), y1 = Mathf.Min(H, area.yMax + 4);
            if (x1 <= x0 || y1 <= y0) return;
            Color32[] lp = land.px, cp = coast.px;
            int no = offX.Length;
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    int m = 0;   // maior opacidade de terra entre os vizinhos
                    for (int o = 0; o < no; o++)
                    {
                        int xx = x + offX[o], yy = y + offY[o];
                        if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                        int a = lp[yy * W + xx].a;
                        if (a > m) { m = a; if (m == 255) break; }   // já é o máximo, para de procurar
                    }
                    Color32 c = Ink;
                    c.a = (byte)(m * .95f);      // opacidade da tinta = proximidade da terra
                    cp[y * W + x] = c;
                }
            }
            coast.MarkDirty(new RectInt(x0, y0, x1 - x0, y1 - y0));
        }

        /// <summary>Refaz o contorno do mapa inteiro (ao abrir um arquivo, por exemplo).</summary>
        public static void RebuildAll(PixelLayer land, PixelLayer coast)
        {
            Update(land, coast, new RectInt(0, 0, land.W, land.H));
        }
    }

    /// <summary>
    /// Água rasa ao redor da terra: calcula a distância de cada ponto do mar até a terra mais próxima
    /// e pinta uma faixa de cor clara que escurece com a distância, mais linhas de ondulação.
    /// Build() não usa nada da Unity que dependa da thread principal, então pode rodar em segundo plano.
    /// </summary>
    public static class ShoalBuilder
    {
        // paletas: nome, cor perto da costa e cor longe (RGB 0-255). Cor nula = sem faixa.
        public static readonly string[] ToneNames = { "Turquesa", "Areia molhada", "Espuma branca", "Sombra escura", "Pântano", "Brasa", "Nenhuma" };
        static readonly int[][] ToneNear = {
            new[]{186,240,224}, new[]{240,226,176}, new[]{255,255,255}, new[]{40,52,60}, new[]{176,200,120}, new[]{255,206,110}, null };
        static readonly int[][] ToneFar = {
            new[]{56,176,204}, new[]{190,160,100}, new[]{214,232,244}, new[]{14,22,30}, new[]{84,124,64}, new[]{210,70,24}, null };

        /// <summary>Imagem resultante (em resolução reduzida).</summary>
        public sealed class Result
        {
            public int w, h;
            public Color32[] px;
        }

        /// <summary>
        /// Gera uma "máscara de terra" pequena (1 = terra, 0 = mar), olhando só alguns pixels da camada
        /// da terra. Isso torna o cálculo muito mais rápido. Rodar na thread principal (lê a camada).
        /// </summary>
        public static byte[] SampleMask(PixelLayer land, int w, int h)
        {
            byte[] m = new byte[w * h];
            for (int y = 0; y < h; y++)
            {
                int sy = Mathf.Min(land.H - 1, (int)((y + .5f) * land.H / h));
                for (int x = 0; x < w; x++)
                {
                    int sx = Mathf.Min(land.W - 1, (int)((x + .5f) * land.W / w));
                    m[y * w + x] = (byte)(land.px[sy * land.W + sx].a > 96 ? 1 : 0);   // opacidade > 96 conta como terra
                }
            }
            return m;
        }

        /// <summary>
        /// O cálculo principal. mask = terra/mar em baixa resolução; k = fator de redução
        /// (largura baixa / largura real); sh = configurações da água rasa.
        /// </summary>
        public static Result Build(byte[] mask, int w, int h, float k, ShoalSettings sh)
        {
            Result res = new Result { w = w, h = h, px = new Color32[w * h] };
            int tone = Mathf.Clamp(sh.tone, 0, ToneNames.Length - 1);
            int[] near = ToneNear[tone], far = ToneFar[tone];
            if (near == null || !sh.on) return res;       // "Nenhuma" ou desligado: imagem vazia

            int N = w * h;
            const float INF = 1e6f, SQ = 1.4142f;         // "infinito" e a raiz de 2 (distância na diagonal)
            float Wl = sh.width * k;                      // largura da faixa na resolução reduzida
            float cap = Wl * 1.5f + 6f;                   // distâncias maiores que isso não importam
            float[] D = new float[N];                     // D = distância até a terra
            for (int i = 0; i < N; i++) D[i] = mask[i] != 0 ? 0f : INF;

            // ---- campo de distância (transformada de distância em duas passadas) ----
            // passada 1: de cima para baixo e da esquerda para a direita
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x; float d = D[i]; if (d == 0f) continue;   // terra: distância 0
                    if (x > 0) d = Mathf.Min(d, D[i - 1] + 1f);
                    if (y > 0)
                    {
                        d = Mathf.Min(d, D[i - w] + 1f);
                        if (x > 0) d = Mathf.Min(d, D[i - w - 1] + SQ);
                        if (x < w - 1) d = Mathf.Min(d, D[i - w + 1] + SQ);
                    }
                    D[i] = d;
                }
            // passada 2: de baixo para cima e da direita para a esquerda
            for (int y = h - 1; y >= 0; y--)
                for (int x = w - 1; x >= 0; x--)
                {
                    int i = y * w + x; float d = D[i]; if (d == 0f) continue;
                    if (x < w - 1) d = Mathf.Min(d, D[i + 1] + 1f);
                    if (y < h - 1)
                    {
                        d = Mathf.Min(d, D[i + w] + 1f);
                        if (x < w - 1) d = Mathf.Min(d, D[i + w + 1] + SQ);
                        if (x > 0) d = Mathf.Min(d, D[i + w - 1] + SQ);
                    }
                    D[i] = d;
                }
            for (int i = 0; i < N; i++) if (D[i] > cap) D[i] = cap;   // limita

            // ---- suavização (2 passadas de média 3x3) para as curvas ficarem arredondadas ----
            for (int pass = 0; pass < 2; pass++)
            {
                float[] T = new float[N];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float sum = 0f; int c = 0;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int yy = y + dy; if (yy < 0 || yy >= h) continue;
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int xx = x + dx; if (xx < 0 || xx >= w) continue;
                                sum += D[yy * w + xx]; c++;
                            }
                        }
                        T[y * w + x] = sum / c;
                    }
                D = T;
            }

            // posição (fração da largura da faixa) e intensidade de cada linha de ondulação
            float[] lvF = { .38f, .7f, .98f };
            float[] lvA = { .34f, .24f, .16f };

            // ---- colore cada pixel do mar ----
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (mask[i] != 0) continue;                 // sob a terra: nada
                    float d = D[i];
                    float t = d / Wl;                            // 0 colado na costa … 1 no fim da faixa
                    float al = t < 1f ? Mathf.Pow(1f - t, 1.6f) * sh.intensity : 0f;   // some gradualmente
                    float t2 = Mathf.Min(1f, Mathf.Sqrt(Mathf.Max(0f, t)));
                    // cor vai da "perto" para a "longe" conforme a distância
                    float r = Mathf.Lerp(near[0], far[0], t2);
                    float g = Mathf.Lerp(near[1], far[1], t2);
                    float b = Mathf.Lerp(near[2], far[2], t2);
                    float outA = al;

                    if (sh.lines)
                    {
                        // linhas finas onde a distância é igual a cada nível (curvas de nível do mar)
                        for (int li = 0; li < 3; li++)
                        {
                            float L = Wl * lvF[li];
                            float ridge = 1f - Mathf.Abs(d - L) / 1.1f;            // 1 em cima da linha, 0 longe
                            if (ridge <= 0f) continue;
                            if (NoiseUtil.Hash2(x / 7, y / 7, li + 31) < .3f) continue;   // falhas aleatórias (traço irregular)
                            float la = Mathf.Clamp01(ridge * lvA[li] * 1.25f * 2f);
                            float tot = Mathf.Max(outA, la);
                            // mistura a cor clara da linha por cima da faixa
                            r = Mathf.Lerp(r, 244f, la); g = Mathf.Lerp(g, 255f, la); b = Mathf.Lerp(b, 250f, la);
                            outA = tot;
                        }
                    }
                    if (outA <= .002f) continue;                // praticamente invisível: não grava
                    res.px[i] = new Color32((byte)Mathf.Clamp(r, 0, 255), (byte)Mathf.Clamp(g, 0, 255), (byte)Mathf.Clamp(b, 0, 255), (byte)(Mathf.Clamp01(outA) * 255f));
                }
            return res;
        }
    }
}

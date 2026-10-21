using UnityEngine;

namespace Atelie
{
    /// <summary>
    /// Funções de ruído (números "aleatórios" que variam suavemente).
    /// São determinísticas: a mesma posição sempre dá o mesmo valor. Isso é importante porque
    /// o pincel usa ruído para quebrar as bordas, e dois traços vizinhos precisam se encaixar.
    /// </summary>
    public static class NoiseUtil
    {
        /// <summary>
        /// "Hash": transforma três números inteiros (x, y e uma semente) em um número entre 0 e 1.
        /// Parece aleatório, mas é sempre o mesmo para a mesma entrada.
        /// </summary>
        public static float Hash2(int x, int y, int s)
        {
            // unchecked = permite que as multiplicações "estourem" o tamanho do int (isso é desejado aqui)
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + s * 1442695041;   // mistura as 3 entradas
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;               // embaralha os bits
                h ^= (int)((uint)h >> 16);                                 // embaralha mais um pouco
                return (float)((uint)h / 4294967296.0);                    // converte para 0..1
            }
        }

        /// <summary>Resto da divisão que nunca dá negativo (para repetir o ruído em loop).</summary>
        static int Md(int a, int n) { int m = a % n; return m < 0 ? m + n : m; }

        /// <summary>
        /// Ruído de valor suave. Sorteia um valor em cada ponto de uma grade e interpola entre eles.
        /// 'per' é o período: depois de 'per' células o padrão se repete, o que permite
        /// criar texturas sem emenda (tileáveis).
        /// </summary>
        public static float PNoise(float x, float y, int per, int s)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);           // célula da grade
            float xf = x - xi, yf = y - yi;                                    // posição dentro da célula (0..1)
            float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);  // curva suave (smoothstep)
            int x0 = Md(xi, per), x1 = Md(xi + 1, per), y0 = Md(yi, per), y1 = Md(yi + 1, per);
            float a = Mathf.Lerp(Hash2(x0, y0, s), Hash2(x1, y0, s), u);      // interpola em cima
            float b = Mathf.Lerp(Hash2(x0, y1, s), Hash2(x1, y1, s), u);      // interpola embaixo
            return Mathf.Lerp(a, b, v);                                        // e entre as duas
        }

        /// <summary>
        /// Ruído fractal (fbm): soma várias camadas de PNoise, cada uma com o dobro da frequência
        /// e metade da força. Dá aquele aspecto natural de nuvem/terreno, com detalhes grandes e pequenos.
        /// oct = quantas camadas somar.
        /// </summary>
        public static float Fbm(float x, float y, int per, int oct, int s)
        {
            float a = 1f, f = 1f, sum = 0f, n = 0f;
            for (int o = 0; o < oct; o++)
            {
                sum += a * PNoise(x * f, y * f, (int)(per * f), s + o * 17);
                n += a; a *= .5f; f *= 2f;
            }
            return sum / n;   // divide pelo total para manter o resultado entre 0 e 1
        }

        // ---------- ruído fractal usado para "quebrar" a borda do pincel ----------
        public const int NZN = 256;      // lado da tabela pré-calculada (256x256)
        public const int NZP = 640;      // a cada 640 pixels do mapa o padrão se repete
        static float[] nz;               // a tabela de ruído

        /// <summary>Calcula a tabela de ruído uma única vez (na primeira vez que o pincel é usado).</summary>
        public static void InitRough()
        {
            if (nz != null) return;      // já calculada
            nz = new float[NZN * NZN];
            for (int y = 0; y < NZN; y++)
                for (int x = 0; x < NZN; x++)
                {
                    // soma 7 camadas de ruído para ter bordas com detalhes de vários tamanhos
                    float amp = 1f, f = 1f, sum = 0f, tot = 0f;
                    for (int o = 0; o < 7; o++)
                    {
                        sum += amp * PNoise((float)x / NZN * 4f * f, (float)y / NZN * 4f * f, (int)(4 * f), 123 + o * 17);
                        tot += amp; amp *= .66f; f *= 2f;
                    }
                    // aumenta o contraste (x3.2) para o recorte ficar mais marcado
                    nz[y * NZN + x] = Mathf.Clamp01((sum / tot - .5f) * 3.2f + .5f);
                }
        }

        /// <summary>
        /// Lê a tabela de ruído numa posição do mundo (x, y em pixels do mapa), interpolando entre
        /// os 4 pontos vizinhos. 'k' controla o tamanho do detalhe. Devolve 0..1.
        /// Como usa a posição do MUNDO, o recorte é sempre igual no mesmo lugar do mapa.
        /// </summary>
        public static float RoughAt(float x, float y, float k)
        {
            float u = x * k, v = y * k;
            int i = Mathf.FloorToInt(u), j = Mathf.FloorToInt(v);
            float fu = u - i, fv = v - j;
            int i0 = i & 255, i1 = (i + 1) & 255;                // "& 255" faz a tabela repetir em loop
            int j0 = (j & 255) * NZN, j1 = ((j + 1) & 255) * NZN;
            float a = Mathf.Lerp(nz[j0 + i0], nz[j0 + i1], fu);
            float b = Mathf.Lerp(nz[j1 + i0], nz[j1 + i1], fu);
            return Mathf.Lerp(a, b, fv);
        }
    }
}

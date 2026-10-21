using UnityEngine;

namespace Atelie
{
    /// <summary>
    /// Texturas MÍNIMAS geradas por código, só para o editor funcionar logo na primeira vez
    /// (sem nenhum asset seu). Depois você cria seus TerrainDef com a sua arte e esta classe não é mais usada.
    /// </summary>
    public static class FallbackArt
    {
        // Gera uma textura pequena SEM EMENDA misturando duas cores com ruído fractal (a = cor escura, b = cor clara, per = tamanho das manchas, contrast = força).
        static Texture2D Tile(int res, Color a, Color b, int per, int seed, float contrast)
        {
            Texture2D t = new Texture2D(res, res, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Repeat;
            Color32[] px = new Color32[res * res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float n1 = NoiseUtil.Fbm(x / (float)res * per, y / (float)res * per, per, 4, seed);
                    float n2 = NoiseUtil.Fbm(x / (float)res * per * 4f, y / (float)res * per * 4f, per * 4, 2, seed + 9);
                    float v = Mathf.Clamp01((n1 - .5f) * contrast + .5f);
                    Color c = Color.Lerp(a, b, v);
                    float sp = (n2 - .5f) * .12f;
                    c.r = Mathf.Clamp01(c.r + sp); c.g = Mathf.Clamp01(c.g + sp); c.b = Mathf.Clamp01(c.b + sp);
                    c.a = 1f;
                    px[y * res + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(false);
            return t;
        }

        // Cria um TerrainDef (terreno) por código, já com a textura.
        static TerrainDef Def(string id, string name, string group, bool sea, Texture2D tex)
        {
            TerrainDef d = ScriptableObject.CreateInstance<TerrainDef>();
            d.id = id; d.displayName = name; d.group = group; d.isSea = sea; d.texture = tex; d.tileSize = tex.width * 2f;
            d.name = id;
            return d;
        }

        // Monta uma biblioteca provisória: 6 terrenos e 3 mares. Cada linha = id, nome, grupo, é-mar?, textura.
        public static AtelieLibrary MakeLibrary()
        {
            AtelieLibrary lib = ScriptableObject.CreateInstance<AtelieLibrary>();
            lib.terrains.Add(Def("grass", "Grama", "Terra", false, Tile(128, new Color(.36f, .55f, .24f), new Color(.5f, .68f, .3f), 4, 11, 2.2f)));
            lib.terrains.Add(Def("grass_dark", "Floresta", "Terra", false, Tile(128, new Color(.18f, .34f, .16f), new Color(.28f, .45f, .2f), 4, 21, 2.2f)));
            lib.terrains.Add(Def("dirt", "Terra seca", "Terra", false, Tile(128, new Color(.55f, .4f, .24f), new Color(.7f, .54f, .34f), 4, 31, 2f)));
            lib.terrains.Add(Def("sand", "Areia", "Terra", false, Tile(128, new Color(.82f, .72f, .48f), new Color(.92f, .84f, .6f), 4, 41, 1.8f)));
            lib.terrains.Add(Def("rock", "Rocha", "Terra", false, Tile(128, new Color(.42f, .4f, .38f), new Color(.62f, .6f, .56f), 5, 51, 2.6f)));
            lib.terrains.Add(Def("snow", "Neve", "Terra", false, Tile(128, new Color(.84f, .88f, .94f), new Color(.97f, .98f, 1f), 4, 61, 1.8f)));
            lib.terrains.Add(Def("sea_mid", "Mar", "Mar", true, Tile(128, new Color(.2f, .42f, .6f), new Color(.3f, .55f, .72f), 3, 71, 1.6f)));
            lib.terrains.Add(Def("sea_deep", "Mar profundo", "Mar", true, Tile(128, new Color(.08f, .2f, .36f), new Color(.14f, .3f, .5f), 3, 81, 1.6f)));
            lib.terrains.Add(Def("sea_light", "Mar claro", "Mar", true, Tile(128, new Color(.3f, .62f, .74f), new Color(.46f, .78f, .84f), 3, 91, 1.6f)));
            return lib;
        }
    }
}

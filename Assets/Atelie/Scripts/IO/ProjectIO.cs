using System;
using System.IO;
using UnityEngine;

namespace Atelie
{
    /// <summary>
    /// Salvar / abrir projetos. Um projeto é uma PASTA:
    ///   map.json  (documento: objetos, rotas, textos, filtros…)
    ///   sea.png   (camada do mar)
    ///   land.png  (camada da terra, com transparência)
    /// </summary>
    public static class ProjectIO
    {
        // Pasta onde os mapas são guardados (dentro dos dados do aplicativo; no Windows: AppData/LocalLow/<empresa>/<jogo>/Mapas).
        public static string RootDir
        {
            get { return Path.Combine(Application.persistentDataPath, "Mapas"); }
        }

        // Troca caracteres proibidos em nomes de arquivo (/ \ : * ? etc.) por '_' para o nome do mapa virar nome de pasta.
        public static string SafeName(string n)
        {
            if (string.IsNullOrEmpty(n)) n = "mapa";
            foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n.Trim();
        }

        // Caminho completo da pasta de um mapa, a partir do nome.
        public static string FolderFor(string mapName) { return Path.Combine(RootDir, SafeName(mapName)); }

        // Converte uma camada de pixels em bytes de um arquivo PNG (com transparência).
        public static byte[] EncodePng(PixelLayer l)
        {
            Texture2D t = new Texture2D(l.W, l.H, TextureFormat.RGBA32, false);
            t.SetPixels32(l.px);
            t.Apply(false);
            byte[] bytes = t.EncodeToPNG();
            UnityEngine.Object.Destroy(t);
            return bytes;
        }

        // Salva o mapa: escreve map.json (dados), sea.png e land.png (as duas camadas pintadas).
        public static void Save(MapSession s, string folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "map.json"), JsonUtility.ToJson(s.doc, true));
            File.WriteAllBytes(Path.Combine(folder, "sea.png"), EncodePng(s.sea));
            File.WriteAllBytes(Path.Combine(folder, "land.png"), EncodePng(s.land));
        }

        // Lê um PNG e copia os pixels para dentro de uma camada (se o arquivo não existir, não faz nada).
        static void LoadPng(string path, PixelLayer into)
        {
            if (!File.Exists(path)) return;
            Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!t.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.Destroy(t); return; }
            Color32[] src = t.GetPixels32();
            int w = Mathf.Min(t.width, into.W), h = Mathf.Min(t.height, into.H);
            for (int y = 0; y < h; y++)
                Array.Copy(src, y * t.width, into.px, y * into.W, w);
            into.MarkAllDirty();
            UnityEngine.Object.Destroy(t);
        }

        /// <summary>Abre um projeto salvo. Recria as camadas da sessão.</summary>
        // Abre um mapa salvo: lê o map.json, recria as camadas com o tamanho certo e carrega os PNGs.
        public static bool Load(MapSession s, string folder)
        {
            string json = Path.Combine(folder, "map.json");
            if (!File.Exists(json)) return false;
            MapDocument d = JsonUtility.FromJson<MapDocument>(File.ReadAllText(json));
            if (d == null) return false;
            s.Build(d, null);
            LoadPng(Path.Combine(folder, "sea.png"), s.sea);
            LoadPng(Path.Combine(folder, "land.png"), s.land);
            s.sea.UploadAll(); s.land.UploadAll();
            s.RebuildCoast();
            s.RaiseChanged();
            return true;
        }

        // Lista as pastas de mapas salvos (as que têm um map.json dentro).
        public static string[] ListProjects()
        {
            if (!Directory.Exists(RootDir)) return new string[0];
            string[] dirs = Directory.GetDirectories(RootDir);
            System.Collections.Generic.List<string> res = new System.Collections.Generic.List<string>();
            for (int i = 0; i < dirs.Length; i++)
                if (File.Exists(Path.Combine(dirs[i], "map.json"))) res.Add(dirs[i]);
            return res.ToArray();
        }

        /// <summary>Exporta o mapa composto (sem objetos ainda) como PNG.</summary>
        // Gera uma imagem PNG do mapa pronto, juntando as camadas na ordem: mar, água rasa, contorno, terra.
        // (Objetos, rotas e filtros entram nas próximas etapas.)
        public static void ExportPng(MapSession s, string file)
        {
            int W = s.W, H = s.H;
            Color32[] outp = new Color32[W * H];
            // mar
            // começa com o mar
            Array.Copy(s.sea.px, outp, outp.Length);
            // água rasa (baixa resolução, interpolação bilinear simples)
            // a água rasa tem resolução menor que o mapa, então é esticada com interpolação bilinear (mistura dos 4 pixels vizinhos)
            if (s.doc.shoal.on)
            {
                PixelLayer sh = s.shoal;
                for (int y = 0; y < H; y++)
                {
                    float fy = (y + .5f) * sh.H / H - .5f;
                    int y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, sh.H - 1), y1 = Mathf.Min(sh.H - 1, y0 + 1);
                    float ty = Mathf.Clamp01(fy - y0);
                    for (int x = 0; x < W; x++)
                    {
                        float fx = (x + .5f) * sh.W / W - .5f;
                        int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, sh.W - 1), x1 = Mathf.Min(sh.W - 1, x0 + 1);
                        float tx = Mathf.Clamp01(fx - x0);
                        Color32 a = sh.px[y0 * sh.W + x0], b = sh.px[y0 * sh.W + x1], c = sh.px[y1 * sh.W + x0], d = sh.px[y1 * sh.W + x1];
                        float sa = Mathf.Lerp(Mathf.Lerp(a.a, b.a, tx), Mathf.Lerp(c.a, d.a, tx), ty) / 255f;
                        if (sa <= .002f) continue;
                        float sr = Mathf.Lerp(Mathf.Lerp(a.r, b.r, tx), Mathf.Lerp(c.r, d.r, tx), ty);
                        float sg = Mathf.Lerp(Mathf.Lerp(a.g, b.g, tx), Mathf.Lerp(c.g, d.g, tx), ty);
                        float sb = Mathf.Lerp(Mathf.Lerp(a.b, b.b, tx), Mathf.Lerp(c.b, d.b, tx), ty);
                        Color32 o = outp[y * W + x];
                        o.r = (byte)(sr * sa + o.r * (1f - sa)); o.g = (byte)(sg * sa + o.g * (1f - sa)); o.b = (byte)(sb * sa + o.b * (1f - sa));
                        outp[y * W + x] = o;
                    }
                }
            }
            // contorno + terra
            // depois o contorno e a terra, por cima
            OverComposite(outp, s.coast.px);
            OverComposite(outp, s.land.px);
            Texture2D t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            t.SetPixels32(outp);
            t.Apply(false);
            File.WriteAllBytes(file, t.EncodeToPNG());
            UnityEngine.Object.Destroy(t);
        }

        // Coloca a camada 'src' por cima da 'dst' respeitando a transparência (fórmula padrão de composição).
        static void OverComposite(Color32[] dst, Color32[] src)
        {
            for (int i = 0; i < dst.Length; i++)
            {
                Color32 s = src[i];
                if (s.a == 0) continue;
                float sa = s.a / 255f;
                Color32 d = dst[i];
                d.r = (byte)(s.r * sa + d.r * (1f - sa) + .5f);
                d.g = (byte)(s.g * sa + d.g * (1f - sa) + .5f);
                d.b = (byte)(s.b * sa + d.b * (1f - sa) + .5f);
                d.a = 255;
                dst[i] = d;
            }
        }
    }
}
